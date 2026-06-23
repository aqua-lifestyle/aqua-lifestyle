using System;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Web.Host.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Abp.UI;
using System.Collections.Generic;

namespace AqualLifeStyle.Web.Host.Startup
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, IWebHostEnvironment env, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _env = env;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            // Ensure every request has a correlation id and it's included in logs
            var correlationId = context.TraceIdentifier ?? Guid.NewGuid().ToString();
            context.Items["CorrelationId"] = correlationId;

            using (_logger.BeginScope(new System.Collections.Generic.Dictionary<string, object> { { "CorrelationId", correlationId } }))
            {
                try
                {
                    await _next(context);
                }
                catch (Exception ex)
                {
                    // Classify operational vs programmer errors for logging
                    var isOperational = ex is UserFriendlyException || ex is AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessException || ex is ArgumentException || ex is KeyNotFoundException || ex is OperationCanceledException;
                    if (isOperational)
                    {
                        _logger.LogWarning(ex, "Operational exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);
                    }
                    else
                    {
                        _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);
                    }

                    var errorResponse = ErrorResponseBuilder.Build(ex);
                    // Attach correlation id to the client response so support can map to logs
                    errorResponse.CorrelationId = correlationId;

                    if (_env.IsDevelopment())
                    {
                        errorResponse.FieldErrors["ExceptionType"] = ex.GetType().Name;
                        errorResponse.FieldErrors["ExceptionMessage"] = ex.Message;
                        if (ex.InnerException != null)
                        {
                            errorResponse.FieldErrors["InnerException"] = ex.InnerException.Message;
                        }
                    }

                    context.Response.Clear();
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = errorResponse.StatusCode;

                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    };

                    var payload = JsonSerializer.Serialize(errorResponse, options);
                    await context.Response.WriteAsync(payload);
                }
            }
        }
    }
}

