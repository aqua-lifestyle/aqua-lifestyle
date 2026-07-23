using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class OnyxPlanTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal DirectEntryAmount { get; }
        public string Currency { get; }

        private OnyxPlanTerms(
            string version,
            DateTime effectiveFrom,
            decimal directEntryAmount,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A terms version is required.", nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            if (directEntryAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(directEntryAmount), "The direct-entry amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException("A three-letter currency code is required.", nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            DirectEntryAmount = directEntryAmount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static OnyxPlanTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal directEntryAmount,
            string currency = "ZAR")
        {
            return new OnyxPlanTerms(version, effectiveFrom, directEntryAmount, currency);
        }
    }
}
