using System;

namespace AqualLifeStyle.Application.Exceptions
{
    /// <summary>
    /// Base exception for AqualLifeStyle application-layer business errors.
    /// Provides a consistent way to represent business rule violations and validation errors.
    /// </summary>
    public abstract class AqualLifeStyleBusinessException : Exception
    {
        /// <summary>
        /// Business-friendly error code for client-side error handling.
        /// </summary>
        public string ErrorCode { get; protected set; }

        /// <summary>
        /// HTTP status code to return (400 Bad Request, 422 Unprocessable Entity, etc.)
        /// </summary>
        public int StatusCode { get; protected set; } = 400;

        protected AqualLifeStyleBusinessException(string message, string errorCode = null) 
            : base(message)
        {
            ErrorCode = errorCode ?? "BUSINESS_ERROR";
        }

        protected AqualLifeStyleBusinessException(string message, Exception innerException, string errorCode = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode ?? "BUSINESS_ERROR";
        }
    }

    /// <summary>
    /// Thrown when a resource (Customer, Membership, etc.) is not found.
    /// </summary>
    public class AqualLifeStyleNotFoundException : AqualLifeStyleBusinessException
    {
        public AqualLifeStyleNotFoundException(string resourceName, object identifier)
            : base($"{resourceName} with identifier '{identifier}' was not found.", "RESOURCE_NOT_FOUND")
        {
            StatusCode = 404;
        }

        public AqualLifeStyleNotFoundException(string message)
            : base(message, "RESOURCE_NOT_FOUND")
        {
            StatusCode = 404;
        }
    }

    /// <summary>
    /// Thrown when a validation rule fails (invalid input, constraint violation).
    /// </summary>
    public class AqualLifeStyleValidationException : AqualLifeStyleBusinessException
    {
        public string FieldName { get; private set; }

        public AqualLifeStyleValidationException(string fieldName, string message)
            : base(message, "VALIDATION_ERROR")
        {
            FieldName = fieldName;
            StatusCode = 422;
        }

        public AqualLifeStyleValidationException(string message)
            : base(message, "VALIDATION_ERROR")
        {
            StatusCode = 422;
        }
    }

    /// <summary>
    /// Thrown when an operation violates business rules (e.g., cannot convert already-converted enquiry).
    /// </summary>
    public class AqualLifeStyleBusinessRuleException : AqualLifeStyleBusinessException
    {
        public AqualLifeStyleBusinessRuleException(string message, string businessRuleCode = null)
            : base(message, businessRuleCode ?? "BUSINESS_RULE_VIOLATION")
        {
            StatusCode = 400;
        }

        public AqualLifeStyleBusinessRuleException(string message, Exception innerException, string businessRuleCode = null)
            : base(message, innerException, businessRuleCode ?? "BUSINESS_RULE_VIOLATION")
        {
            StatusCode = 400;
        }
    }

    /// <summary>
    /// Thrown when an entity transitions to an invalid state (e.g., activating already-active membership).
    /// </summary>
    public class AqualLifeStyleInvalidStateException : AqualLifeStyleBusinessException
    {
        public AqualLifeStyleInvalidStateException(string entityName, string currentState, string attemptedAction)
            : base($"{entityName} in state '{currentState}' cannot {attemptedAction}.", "INVALID_STATE")
        {
            StatusCode = 400;
        }

        public AqualLifeStyleInvalidStateException(string entityName, string currentState, string attemptedAction, Exception innerException)
            : base($"{entityName} in state '{currentState}' cannot {attemptedAction}.", innerException, "INVALID_STATE")
        {
            StatusCode = 400;
        }

        public AqualLifeStyleInvalidStateException(string message)
            : base(message, "INVALID_STATE")
        {
            StatusCode = 400;
        }

        public AqualLifeStyleInvalidStateException(string message, Exception innerException)
            : base(message, innerException, "INVALID_STATE")
        {
            StatusCode = 400;
        }
    }

    /// <summary>
    /// Thrown when an operation requires a precondition that is not met (e.g., inactive membership cannot be assigned).
    /// </summary>
    public class AqualLifeStylePreconditionException : AqualLifeStyleBusinessException
    {
        public AqualLifeStylePreconditionException(string message, string preconditionCode = null)
            : base(message, preconditionCode ?? "PRECONDITION_FAILED")
        {
            StatusCode = 412;
        }
    }

    /// <summary>
    /// Thrown when duplicate entity creation is attempted (e.g., duplicate email address).
    /// </summary>
    public class AqualLifeStyleDuplicateException : AqualLifeStyleBusinessException
    {
        public AqualLifeStyleDuplicateException(string resourceName, string fieldName, object value)
            : base($"{resourceName} with {fieldName} '{value}' already exists.", "DUPLICATE_RESOURCE")
        {
            StatusCode = 409;
        }

        public AqualLifeStyleDuplicateException(string message)
            : base(message, "DUPLICATE_RESOURCE")
        {
            StatusCode = 409;
        }
    }

    /// <summary>
    /// Thrown when an operation is not authorized for the current user/context.
    /// </summary>
    public class AqualLifeStyleAuthorizationException : AqualLifeStyleBusinessException
    {
        public AqualLifeStyleAuthorizationException(string message, string permissionCode = null)
            : base(message, permissionCode ?? "AUTHORIZATION_DENIED")
        {
            StatusCode = 403;
        }
    }

    /// <summary>
    /// Thrown when a dependent resource cannot be found or accessed (foreign key constraint).
    /// </summary>
    public class AqualLifeStyleDependencyException : AqualLifeStyleBusinessException
    {
        public AqualLifeStyleDependencyException(string dependentResource, string dependentIdentifier)
            : base($"Cannot complete operation: required {dependentResource} with identifier '{dependentIdentifier}' does not exist or is inaccessible.", "DEPENDENCY_NOT_FOUND")
        {
            StatusCode = 400;
        }

        public AqualLifeStyleDependencyException(string message)
            : base(message, "DEPENDENCY_ERROR")
        {
            StatusCode = 400;
        }
    }
}
