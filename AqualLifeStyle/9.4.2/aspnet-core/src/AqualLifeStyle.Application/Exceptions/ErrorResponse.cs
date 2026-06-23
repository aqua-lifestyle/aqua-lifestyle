using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Application.Exceptions
{
    /// <summary>
    /// Standard error response returned to clients when business exceptions occur.
    /// Provides consistent error format across the API.
    /// </summary>
    public class ErrorResponse
    {
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
        public Dictionary<string, string> FieldErrors { get; set; }
        public DateTime Timestamp { get; set; }

        public ErrorResponse()
        {
            Timestamp = DateTime.UtcNow;
            FieldErrors = new Dictionary<string, string>();
        }

        public static ErrorResponse FromException(AqualLifeStyleBusinessException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = ex.ErrorCode,
                Message = ex.Message,
                StatusCode = ex.StatusCode,
                FieldErrors = GetFieldErrors(ex)
            };
        }

        public static ErrorResponse FromException(Exception ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An unexpected error occurred. Please try again later.",
                StatusCode = 500,
                FieldErrors = new Dictionary<string, string>()
            };
        }

        private static Dictionary<string, string> GetFieldErrors(AqualLifeStyleBusinessException ex)
        {
            var errors = new Dictionary<string, string>();

            if (ex is AqualLifeStyleValidationException validationEx && !string.IsNullOrEmpty(validationEx.FieldName))
            {
                errors[validationEx.FieldName] = validationEx.Message;
            }

            return errors;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] {Message} (HTTP {StatusCode})";
        }
    }

    /// <summary>
    /// Helper for converting exceptions to error responses.
    /// Used by exception handling middleware/filters.
    /// </summary>
    public static class ErrorResponseBuilder
    {
        public static ErrorResponse Build(Exception ex)
        {
            if (ex is AqualLifeStyleBusinessException businessEx)
            {
                return ErrorResponse.FromException(businessEx);
            }

            if (ex is ArgumentException argEx)
            {
                return new ErrorResponse
                {
                    ErrorCode = "ARGUMENT_ERROR",
                    Message = argEx.Message,
                    StatusCode = 400
                };
            }

            if (ex is InvalidOperationException invOpEx)
            {
                return new ErrorResponse
                {
                    ErrorCode = "INVALID_OPERATION",
                    Message = invOpEx.Message,
                    StatusCode = 400
                };
            }

            // Default for unexpected exceptions
            return ErrorResponse.FromException(ex);
        }
    }
}
