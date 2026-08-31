using System;

namespace AqualLifeStyle.Domain.AQGreen
{
    public enum AQGreenWeeklySalesReviewStatus
    {
        HeldForEvidence = 1,
        Confirmed = 2,
        Rejected = 3
    }

    public enum AQGreenWeeklySalesThresholdResult
    {
        Met = 1,
        NotMet = 2
    }

    public enum AQGreenWeeklySalesEvidenceSource
    {
        ManualReview = 1
    }

    public static class AQGreenWeeklySalesEligibilityRules
    {
        public const int MaximumRulesVersionLength = 64;
        public const string CurrentVersion = "AQGreenWeeklySalesEligibilityV1";

        public static bool IsSupportedVersion(string version) =>
            string.Equals(version, CurrentVersion, StringComparison.Ordinal);
    }

    public sealed class AQGreenWeeklySalesEligibilityVersionNotSupportedException
        : InvalidOperationException
    {
        public AQGreenWeeklySalesEligibilityVersionNotSupportedException(string version)
            : base($"AQGreen weekly-sales eligibility version '{version ?? "<null>"}' is unsupported.")
        {
        }
    }

    public sealed class AQGreenWeeklySalesEligibilityIntegrityException
        : InvalidOperationException
    {
        public AQGreenWeeklySalesEligibilityIntegrityException(string message)
            : base(message)
        {
        }
    }

    public sealed class AQGreenWeeklySalesEligibilityUnavailableException
        : InvalidOperationException
    {
        public AQGreenWeeklySalesEligibilityUnavailableException(string message)
            : base(message)
        {
        }
    }

    public sealed class AQGreenWeeklySalesQuantities
    {
        public int Spray { get; }
        public int OneLitre { get; }
        public int FiveLitre { get; }

        public AQGreenWeeklySalesQuantities(
            int spray,
            int oneLitre,
            int fiveLitre)
        {
            if (spray < 0) throw new ArgumentOutOfRangeException(nameof(spray));
            if (oneLitre < 0) throw new ArgumentOutOfRangeException(nameof(oneLitre));
            if (fiveLitre < 0) throw new ArgumentOutOfRangeException(nameof(fiveLitre));

            Spray = spray;
            OneLitre = oneLitre;
            FiveLitre = fiveLitre;
        }
    }

    public static class AQGreenWeeklySalesEligibilityEvaluator
    {
        public static AQGreenWeeklySalesThresholdResult Evaluate(
            string rulesVersion,
            AQGreenWeeklySalesQuantities quantities)
        {
            if (!AQGreenWeeklySalesEligibilityRules.IsSupportedVersion(rulesVersion))
                throw new AQGreenWeeklySalesEligibilityVersionNotSupportedException(
                    rulesVersion);
            if (quantities == null) throw new ArgumentNullException(nameof(quantities));

            return quantities.Spray >= 5 &&
                   quantities.OneLitre >= 5 &&
                   quantities.FiveLitre >= 5
                ? AQGreenWeeklySalesThresholdResult.Met
                : AQGreenWeeklySalesThresholdResult.NotMet;
        }
    }
}
