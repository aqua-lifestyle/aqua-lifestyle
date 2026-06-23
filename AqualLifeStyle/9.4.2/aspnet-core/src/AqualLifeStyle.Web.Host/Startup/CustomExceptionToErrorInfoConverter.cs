using System;
using System.Collections.Generic;
using Abp.UI;
using Abp.Web.Models;
using AqualLifeStyle.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace AqualLifeStyle.Web.Host.Startup
{
    // Converts well-known exceptions into Abp ErrorInfo objects so clients receive
    // descriptive messages instead of a generic "internal error".
    // Notes:
    // - We avoid leaking raw BCL exception messages for unclassified exceptions.
    // - CorrelationId is appended to Details when available so clients can report it.
    public class CustomExceptionToErrorInfoConverter : IExceptionToErrorInfoConverter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHostEnvironment _env;

        public CustomExceptionToErrorInfoConverter(IHttpContextAccessor httpContextAccessor, IHostEnvironment env)
        {
            _httpContextAccessor = httpContextAccessor;
            _env = env;
        }

        public IExceptionToErrorInfoConverter Next { get; set; }

        private string GetCorrelationId()
        {
            try
            {
                return _httpContextAccessor?.HttpContext?.Items["CorrelationId"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private ErrorInfo WithCorrelation(ErrorInfo info)
        {
            var cid = GetCorrelationId();
            if (cid == null) return info;
            var existing = info?.Details;
            info.Details = $"CorrelationId: {cid}" + (string.IsNullOrEmpty(existing) ? string.Empty : "\n" + existing);
            return info;
        }

        public ErrorInfo Convert(Exception exception)
        {
            if (exception == null) return null;

            // User friendly exceptions (explicit business/validation errors)
            if (exception is UserFriendlyException ufe)
            {
                var details = ufe.Details ?? ufe.Message;
                if (!_env.IsDevelopment() && string.IsNullOrWhiteSpace(details)) details = null;

                return WithCorrelation(new ErrorInfo
                {
                    Message = ufe.Message ?? "A user-friendly error occurred.",
                    Details = details
                });
            }

            // Application business exceptions are explicitly user-focused and carry status codes.
            if (exception is AqualLifeStyleBusinessException businessException)
            {
                var details = businessException.ErrorCode;
                var info = new ErrorInfo
                {
                    Message = businessException.Message,
                    Details = details
                };
                return WithCorrelation(info);
            }

            // Argument / validation errors - map to a safe, generic client message
            if (exception is ArgumentException aex)
            {
                var details = _env.IsDevelopment() ? aex.Message : null;
                var info = new ErrorInfo
                {
                    Message = "Invalid request.",
                    Details = details
                };
                return WithCorrelation(info);
            }

            // Business rule violations thrown by domain entities - safe generic message
            if (exception is InvalidOperationException)
            {
                var details = _env.IsDevelopment() ? exception.Message : "The requested operation is not valid in the current state.";
                var info = new ErrorInfo
                {
                    Message = "The requested operation is not valid in the current state.",
                    Details = details
                };
                return WithCorrelation(info);
            }

            if (exception is KeyNotFoundException)
            {
                var details = _env.IsDevelopment() ? exception.Message : null;
                var info = new ErrorInfo
                {
                    Message = "The requested resource was not found.",
                    Details = details
                };
                return WithCorrelation(info);
            }

            // Not handled here: delegate to the next converter in chain
            var next = Next?.Convert(exception);
            if (next != null)
            {
                return WithCorrelation(next);
            }

            // Unknown exception: return a generic message but include correlation id
            return WithCorrelation(new ErrorInfo
            {
                Message = "An unexpected error occurred.",
                Details = _env.IsDevelopment() ? exception.Message : null
            });
        }
    }
}
