using System;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Versioned terms describing the R30,000 funeral-cover benefit that is
    /// included once a Club Member completes the R1,200 AQGreen joining
    /// obligation. Activation or enrolment (insurer sign-up, waiting period,
    /// policy effective date) is a separate, unresolved product decision and is
    /// deliberately not part of these terms.
    /// </summary>
    public sealed class AQGreenFuneralCoverTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal FuneralCoverAmount { get; }
        public string Currency { get; }

        private AQGreenFuneralCoverTerms(
            string version,
            DateTime effectiveFrom,
            decimal funeralCoverAmount,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "An AQGreen funeral-cover terms version is required.",
                    nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException(
                    "An effective date is required.",
                    nameof(effectiveFrom));
            }

            if (funeralCoverAmount <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(funeralCoverAmount),
                    "The funeral-cover benefit must have a positive amount.");
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            {
                throw new ArgumentException(
                    "A three-letter ISO currency code is required.",
                    nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            FuneralCoverAmount = funeralCoverAmount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static AQGreenFuneralCoverTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal funeralCoverAmount,
            string currency = "ZAR")
        {
            return new AQGreenFuneralCoverTerms(
                version,
                effectiveFrom,
                funeralCoverAmount,
                currency);
        }
    }
}
