using System;

namespace AqualLifeStyle.Domain.Savings
{
    public sealed class SavingsAccountTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public int MaturityPeriodMonths { get; }
        public decimal MinimumContributionAmount { get; }
        public decimal MaturityInterestRatePercent { get; }
        public int ContributionWindowStartDay { get; }
        public int ContributionWindowEndDay { get; }
        public string Currency { get; }

        private SavingsAccountTerms(
            string version,
            DateTime effectiveFrom,
            int maturityPeriodMonths,
            decimal minimumContributionAmount,
            decimal maturityInterestRatePercent,
            int contributionWindowStartDay,
            int contributionWindowEndDay,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "A savings terms version is required.",
                    nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            if (maturityPeriodMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maturityPeriodMonths));
            }

            if (minimumContributionAmount <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumContributionAmount));
            }

            if (maturityInterestRatePercent <= 0m ||
                maturityInterestRatePercent > 100m)
            {
                throw new ArgumentOutOfRangeException(nameof(maturityInterestRatePercent));
            }

            if (contributionWindowStartDay < 1 ||
                contributionWindowStartDay > 28)
            {
                throw new ArgumentOutOfRangeException(nameof(contributionWindowStartDay));
            }

            if (contributionWindowEndDay < contributionWindowStartDay ||
                contributionWindowEndDay > 28)
            {
                throw new ArgumentOutOfRangeException(nameof(contributionWindowEndDay));
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException(
                    "A three-letter currency code is required.",
                    nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            MaturityPeriodMonths = maturityPeriodMonths;
            MinimumContributionAmount = minimumContributionAmount;
            MaturityInterestRatePercent = maturityInterestRatePercent;
            ContributionWindowStartDay = contributionWindowStartDay;
            ContributionWindowEndDay = contributionWindowEndDay;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static SavingsAccountTerms Create(
            string version,
            DateTime effectiveFrom,
            int maturityPeriodMonths,
            decimal minimumContributionAmount,
            decimal maturityInterestRatePercent,
            int contributionWindowStartDay,
            int contributionWindowEndDay,
            string currency = "ZAR")
        {
            return new SavingsAccountTerms(
                version,
                effectiveFrom,
                maturityPeriodMonths,
                minimumContributionAmount,
                maturityInterestRatePercent,
                contributionWindowStartDay,
                contributionWindowEndDay,
                currency);
        }

        public bool IsContributionWindowOpen(DateTime contributionAt)
        {
            return contributionAt.Day >= ContributionWindowStartDay &&
                   contributionAt.Day <= ContributionWindowEndDay;
        }
    }
}
