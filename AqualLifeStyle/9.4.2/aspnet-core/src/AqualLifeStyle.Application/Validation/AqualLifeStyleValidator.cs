using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Application.Validation
{
    /// <summary>
    /// Centralized validation helper for application services.
    /// Provides standardized validation methods with exception throwing for business rules.
    /// </summary>
    public static class AqualLifeStyleValidator
    {
        /// <summary>
        /// Ensure a value is not null or throw validation exception.
        /// </summary>
        public static void NotNull(object value, string fieldName)
        {
            if (value == null)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} is required.");
            }
        }

        /// <summary>
        /// Ensure a string is not null, empty, or whitespace.
        /// </summary>
        public static void NotNullOrEmpty(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} cannot be empty.");
            }
        }

        /// <summary>
        /// Ensure a numeric value is positive (> 0).
        /// </summary>
        public static void Positive(decimal value, string fieldName)
        {
            if (value <= 0)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} must be greater than 0.");
            }
        }

        /// <summary>
        /// Ensure a numeric value is non-negative (>= 0).
        /// </summary>
        public static void NonNegative(decimal value, string fieldName)
        {
            if (value < 0)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} cannot be negative.");
            }
        }

        /// <summary>
        /// Ensure an ID is valid (> 0).
        /// </summary>
        public static void ValidId(int id, string fieldName = "ID")
        {
            if (id <= 0)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} must be valid.");
            }
        }

        /// <summary>
        /// Ensure a value is within a specified range.
        /// </summary>
        public static void InRange(int value, int min, int max, string fieldName)
        {
            if (value < min || value > max)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, 
                    $"{fieldName} must be between {min} and {max}.");
            }
        }

        /// <summary>
        /// Ensure a percentage value is between 0 and 100.
        /// </summary>
        public static void ValidPercentage(decimal value, string fieldName)
        {
            InRange((int)value, 0, 100, fieldName);
        }

        /// <summary>
        /// Ensure an email address is valid (basic format check).
        /// </summary>
        public static void ValidEmail(string email, string fieldName = "Email")
        {
            NotNullOrEmpty(email, fieldName);

            bool isValidFormat;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                isValidFormat = addr.Address == email;
            }
            catch (FormatException)
            {
                isValidFormat = false;
            }
            catch (ArgumentException)
            {
                isValidFormat = false;
            }

            if (!isValidFormat)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} format is invalid.");
            }
        }

        /// <summary>
        /// Ensure a collection is not empty.
        /// </summary>
        public static void NotEmpty<T>(IList<T> collection, string fieldName)
        {
            if (collection == null || collection.Count == 0)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, $"{fieldName} cannot be empty.");
            }
        }

        /// <summary>
        /// Ensure a string has a minimum length.
        /// </summary>
        public static void MinLength(string value, int minLength, string fieldName)
        {
            if (string.IsNullOrEmpty(value) || value.Length < minLength)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, 
                    $"{fieldName} must be at least {minLength} characters.");
            }
        }

        /// <summary>
        /// Ensure a string has a maximum length.
        /// </summary>
        public static void MaxLength(string value, int maxLength, string fieldName)
        {
            if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, 
                    $"{fieldName} cannot exceed {maxLength} characters.");
            }
        }

        /// <summary>
        /// Throw a validation error with custom message.
        /// </summary>
        public static void Fail(string message, string fieldName = null)
        {
            if (fieldName != null)
            {
                throw new Exceptions.AqualLifeStyleValidationException(fieldName, message);
            }
            else
            {
                throw new Exceptions.AqualLifeStyleValidationException(message);
            }
        }

        /// <summary>
        /// Throw a business rule violation exception.
        /// </summary>
        public static void BusinessRule(bool condition, string message, string businessRuleCode = null)
        {
            if (!condition)
            {
                throw new Exceptions.AqualLifeStyleBusinessRuleException(message, businessRuleCode);
            }
        }

        /// <summary>
        /// Throw a precondition failed exception.
        /// </summary>
        public static void Precondition(bool condition, string message, string preconditionCode = null)
        {
            if (!condition)
            {
                throw new Exceptions.AqualLifeStylePreconditionException(message, preconditionCode);
            }
        }

        /// <summary>
        /// Throw an invalid state exception.
        /// </summary>
        public static void InvalidState(string entityName, string currentState, string attemptedAction)
        {
            throw new Exceptions.AqualLifeStyleInvalidStateException(entityName, currentState, attemptedAction);
        }
    }
}
