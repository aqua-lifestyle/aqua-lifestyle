using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class OnyxLoanTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal PrincipalAmount { get; }
        public decimal InterestRatePercent { get; }
        public decimal TotalPayableAmount { get; }
        public int RepaymentPeriodMonths { get; }
        public int InitialWeeklyRequirementCount { get; }
        public decimal InitialWeeklyMinimumAmount { get; }
        public string Currency { get; }

        private OnyxLoanTerms(
            string version,
            DateTime effectiveFrom,
            decimal principalAmount,
            decimal interestRatePercent,
            int repaymentPeriodMonths,
            int initialWeeklyRequirementCount,
            decimal initialWeeklyMinimumAmount,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A loan terms version is required.", nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            if (principalAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(principalAmount));
            }

            if (interestRatePercent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interestRatePercent));
            }

            if (repaymentPeriodMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repaymentPeriodMonths));
            }

            if (initialWeeklyRequirementCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialWeeklyRequirementCount));
            }

            if (initialWeeklyMinimumAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialWeeklyMinimumAmount));
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException("A three-letter currency code is required.", nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            PrincipalAmount = principalAmount;
            InterestRatePercent = interestRatePercent;
            TotalPayableAmount = decimal.Round(
                principalAmount * (1m + interestRatePercent / 100m),
                2,
                MidpointRounding.AwayFromZero);
            RepaymentPeriodMonths = repaymentPeriodMonths;
            InitialWeeklyRequirementCount = initialWeeklyRequirementCount;
            InitialWeeklyMinimumAmount = initialWeeklyMinimumAmount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static OnyxLoanTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal principalAmount,
            decimal interestRatePercent,
            int repaymentPeriodMonths,
            int initialWeeklyRequirementCount,
            decimal initialWeeklyMinimumAmount,
            string currency = "ZAR")
        {
            return new OnyxLoanTerms(
                version,
                effectiveFrom,
                principalAmount,
                interestRatePercent,
                repaymentPeriodMonths,
                initialWeeklyRequirementCount,
                initialWeeklyMinimumAmount,
                currency);
        }
    }
}
