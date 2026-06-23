using System;
using System.Text.RegularExpressions;

namespace AqualLifeStyle.Domain.Common
{
    public sealed class EmailAddress : IEquatable<EmailAddress>
    {
        private static readonly Regex EmailRegex = new(
            @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public string Value { get; private set; }

        private EmailAddress()
        {
            Value = string.Empty;
        }

        public EmailAddress(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or empty.", nameof(email));

            var trimmed = email.Trim();
            if (!EmailRegex.IsMatch(trimmed))
                throw new ArgumentException("Email is not in a valid format.", nameof(email));

            Value = trimmed;
        }

        public override string ToString() => Value;

        public bool Equals(EmailAddress other) => other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => obj is EmailAddress other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public static implicit operator string(EmailAddress email) => email.Value;
    }
}
