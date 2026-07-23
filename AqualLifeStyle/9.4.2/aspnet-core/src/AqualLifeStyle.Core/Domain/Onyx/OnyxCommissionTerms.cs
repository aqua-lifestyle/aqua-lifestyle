using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class OnyxCommissionTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal LevelOneCommissionAmount { get; }
        public string Currency { get; }

        private OnyxCommissionTerms(
            string version,
            DateTime effectiveFrom,
            decimal levelOneCommissionAmount,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "An Onyx commission terms version is required.",
                    nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            if (levelOneCommissionAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelOneCommissionAmount),
                    "The Level 1 commission must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException(
                    "A three-letter currency code is required.",
                    nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            LevelOneCommissionAmount = levelOneCommissionAmount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static OnyxCommissionTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal levelOneCommissionAmount,
            string currency = "ZAR")
        {
            return new OnyxCommissionTerms(
                version,
                effectiveFrom,
                levelOneCommissionAmount,
                currency);
        }

        public decimal GetCommissionAmount(OnyxNetworkLevel level)
        {
            return level == OnyxNetworkLevel.Level1
                ? LevelOneCommissionAmount
                : throw new ArgumentOutOfRangeException(
                    nameof(level),
                    "Only the approved Onyx Level 1 commission is supported.");
        }
    }
}
