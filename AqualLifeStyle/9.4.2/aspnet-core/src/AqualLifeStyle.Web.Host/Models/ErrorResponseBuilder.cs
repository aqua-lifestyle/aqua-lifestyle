using System;
using System.Collections.Generic;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Application.Exceptions;

namespace AqualLifeStyle.Web.Host.Models
{
    public static class ErrorResponseBuilder
    {
        public static ErrorResponse Build(Exception ex)
        {
            if (ex is AbpAuthorizationException)
            {
                return new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "You do not have permission to access this resource.",
                    StatusCode = 403
                };
            }

            if (ex is AqualLifeStyleValidationException validationException)
            {
                var response = new ErrorResponse
                {
                    ErrorCode = validationException.ErrorCode,
                    Message = validationException.Message,
                    StatusCode = validationException.StatusCode
                };

                if (!string.IsNullOrWhiteSpace(validationException.FieldName))
                {
                    response.FieldErrors[validationException.FieldName] = validationException.Message;
                }

                return response;
            }

            if (ex is AqualLifeStyleBusinessException businessException)
            {
                return new ErrorResponse
                {
                    ErrorCode = businessException.ErrorCode,
                    Message = businessException.Message,
                    StatusCode = businessException.StatusCode
                };
            }

            if (ex is KeyNotFoundException)
            {
                return new ErrorResponse
                {
                    ErrorCode = "NOT_FOUND",
                    Message = "The requested resource was not found.",
                    StatusCode = 404
                };
            }

            if (ex is UserFriendlyException ufe)
            {
                return new ErrorResponse
                {
                    ErrorCode = "USER_FRIENDLY_ERROR",
                    Message = ufe.Message,
                    StatusCode = 400
                };
            }

            // BCL ArgumentException may be used for validation in domain code, but
            // it may also appear from programmer bugs. Expose a generic client message
            // here; development-only details are added by middleware.
            if (ex is ArgumentException)
            {
                return new ErrorResponse
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid request.",
                    StatusCode = 400
                };
            }

            if (ex is InvalidOperationException)
            {
                return new ErrorResponse
                {
                    ErrorCode = "INVALID_OPERATION",
                    Message = "The requested operation is not valid in the current state.",
                    StatusCode = 400
                };
            }

            if (ex is OperationCanceledException)
            {
                return new ErrorResponse
                {
                    ErrorCode = "OPERATION_CANCELLED",
                    Message = "The operation was cancelled.",
                    StatusCode = 408
                };
            }

            // Default: generic internal server error
            return new ErrorResponse
            {
                ErrorCode = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred. Please try again later.",
                StatusCode = 500
            };
        }
    }
}

