using System;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// An immutable, effective-dated Onyx commission terms version. Versions
    /// are host-level and append-only: once persisted, a version is never
    /// updated or deleted, so a closed commission cycle can always be traced
    /// back to the exact version that produced it. A version may only become
    /// effective at a canonical Friday 00:00 Africa/Johannesburg cycle
    /// boundary, never halfway through a cycle.
    /// </summary>
    public sealed class OnyxCommissionTermsVersion
        : CreationAuditedAggregateRoot<Guid>
    {
        public const int MaxVersionLength = 64;

        public string Version { get; private set; }
        public DateTime EffectiveAt { get; private set; }
        public decimal LevelOnePerPersonRate { get; private set; }
        public decimal LevelTwoPerPersonRate { get; private set; }
        public decimal LevelThreePerPersonRate { get; private set; }
        public decimal LevelFourPerPersonRate { get; private set; }
        public decimal LevelFivePerPersonRate { get; private set; }
        public string Currency { get; private set; }

        protected OnyxCommissionTermsVersion()
        {
        }

        private OnyxCommissionTermsVersion(
            string version,
            DateTime effectiveAt,
            decimal levelOnePerPersonRate,
            decimal levelTwoPerPersonRate,
            decimal levelThreePerPersonRate,
            decimal levelFourPerPersonRate,
            decimal levelFivePerPersonRate,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "An Onyx commission terms version identifier is required.",
                    nameof(version));
            }

            if (effectiveAt == default)
            {
                throw new ArgumentException(
                    "An effective boundary is required.",
                    nameof(effectiveAt));
            }

            if (!CommissionCycleBoundary.IsCanonicalCycleBoundary(effectiveAt))
            {
                throw new ArgumentException(
                    "Onyx commission terms may only become effective at a Friday 00:00 Africa/Johannesburg cycle boundary.",
                    nameof(effectiveAt));
            }

            EnsurePositiveRate(
                levelOnePerPersonRate,
                nameof(levelOnePerPersonRate),
                1);
            EnsurePositiveRate(
                levelTwoPerPersonRate,
                nameof(levelTwoPerPersonRate),
                2);
            EnsurePositiveRate(
                levelThreePerPersonRate,
                nameof(levelThreePerPersonRate),
                3);
            EnsurePositiveRate(
                levelFourPerPersonRate,
                nameof(levelFourPerPersonRate),
                4);
            EnsurePositiveRate(
                levelFivePerPersonRate,
                nameof(levelFivePerPersonRate),
                5);
            if (string.IsNullOrWhiteSpace(currency) ||
                currency.Trim().Length != 3)
            {
                throw new ArgumentException(
                    "A three-letter currency code is required.",
                    nameof(currency));
            }

            Id = Guid.NewGuid();
            Version = version.Trim();
            EffectiveAt = effectiveAt;
            LevelOnePerPersonRate = levelOnePerPersonRate;
            LevelTwoPerPersonRate = levelTwoPerPersonRate;
            LevelThreePerPersonRate = levelThreePerPersonRate;
            LevelFourPerPersonRate = levelFourPerPersonRate;
            LevelFivePerPersonRate = levelFivePerPersonRate;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static OnyxCommissionTermsVersion Create(
            string version,
            DateTime effectiveAt,
            decimal levelOnePerPersonRate,
            decimal levelTwoPerPersonRate,
            decimal levelThreePerPersonRate,
            decimal levelFourPerPersonRate,
            decimal levelFivePerPersonRate,
            string currency = "ZAR")
        {
            return new OnyxCommissionTermsVersion(
                version,
                effectiveAt,
                levelOnePerPersonRate,
                levelTwoPerPersonRate,
                levelThreePerPersonRate,
                levelFourPerPersonRate,
                levelFivePerPersonRate,
                currency);
        }

        public OnyxCommissionTerms ToTerms()
        {
            return OnyxCommissionTerms.Create(
                Version,
                EffectiveAt,
                LevelOnePerPersonRate,
                LevelTwoPerPersonRate,
                LevelThreePerPersonRate,
                LevelFourPerPersonRate,
                LevelFivePerPersonRate,
                Currency);
        }

        private static void EnsurePositiveRate(
            decimal rate,
            string parameterName,
            int level)
        {
            if (rate <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"The Level {level} per-person commission rate must be greater than zero.");
            }
        }
    }
}
