using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class OnyxTravelBenefitTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public OnyxNetworkLevel RequiredNetworkLevel { get; }
        public int WaitingPeriodMonths { get; }
        public decimal MemberTripContributionPercent { get; }

        private OnyxTravelBenefitTerms(
            string version,
            DateTime effectiveFrom,
            OnyxNetworkLevel requiredNetworkLevel,
            int waitingPeriodMonths,
            decimal memberTripContributionPercent)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "An Onyx travel benefit terms version is required.",
                    nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            if (requiredNetworkLevel != OnyxNetworkLevel.Level3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredNetworkLevel),
                    "The confirmed Onyx travel benefit starts at Level 3.");
            }

            if (waitingPeriodMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waitingPeriodMonths));
            }

            if (memberTripContributionPercent <= 0m ||
                memberTripContributionPercent >= 100m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(memberTripContributionPercent),
                    "The Club Member trip contribution must be between zero and 100 percent.");
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            RequiredNetworkLevel = requiredNetworkLevel;
            WaitingPeriodMonths = waitingPeriodMonths;
            MemberTripContributionPercent = memberTripContributionPercent;
        }

        public static OnyxTravelBenefitTerms Create(
            string version,
            DateTime effectiveFrom,
            OnyxNetworkLevel requiredNetworkLevel,
            int waitingPeriodMonths,
            decimal memberTripContributionPercent)
        {
            return new OnyxTravelBenefitTerms(
                version,
                effectiveFrom,
                requiredNetworkLevel,
                waitingPeriodMonths,
                memberTripContributionPercent);
        }
    }
}
