using System;

namespace AqualLifeStyle.Domain.Common
{
    /// <summary>
    /// Immutable monetary value in a single currency (ZAR for this domain).
    /// Value object: equality is by amount + currency.
    /// </summary>
    public sealed class Money : IEquatable<Money>
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public static readonly Money Zero = new Money(0m, "ZAR");

        private Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        }

        public static Money Of(decimal amount, string currency = "ZAR") => new Money(amount, currency);

        public Money Add(Money other)
        {
            if (other.Currency != Currency)
            {
                throw new InvalidOperationException($"Cannot add {other.Currency} to {Currency}.");
            }

            return new Money(Amount + other.Amount, Currency);
        }

        public Money Subtract(Money other)
        {
            if (other.Currency != Currency)
            {
                throw new InvalidOperationException($"Cannot subtract {other.Currency} from {Currency}.");
            }

            return new Money(Amount - other.Amount, Currency);
        }

        public Money Multiply(decimal factor) => new Money(Amount * factor, Currency);

        public bool Equals(Money other)
            => other != null && Amount == other.Amount && Currency == other.Currency;

        public override bool Equals(object obj) => Equals(obj as Money);

        public override int GetHashCode() => HashCode.Combine(Amount, Currency);

        public override string ToString() => $"{Currency} {Amount:0.00}";
    }
}
