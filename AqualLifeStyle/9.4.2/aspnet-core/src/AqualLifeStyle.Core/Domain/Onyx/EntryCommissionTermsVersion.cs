using System;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// An immutable, effective-dated AQGreen (Entry) commission terms version.
    /// Versions are host-level and append-only: once persisted, a version is
    /// never updated or deleted, so a closed commission cycle can always be
    /// traced back to the exact version that produced it. A version may only
    /// become effective at a canonical Friday 00:00 Africa/Johannesburg cycle
    /// boundary, never halfway through a cycle.
    /// </summary>
    public sealed class EntryCommissionTermsVersion
        : CreationAuditedAggregateRoot<Guid>
    {
        public const int MaxVersionLength = 64;

        public string Version { get; private set; }
        public DateTime EffectiveAt { get; private set; }
        public decimal LevelOneComponentAmount { get; private set; }
        public decimal LevelTwoComponentAmount { get; private set; }
        public decimal LevelThreeComponentAmount { get; private set; }
        public string Currency { get; private set; }

        protected EntryCommissionTermsVersion()
        {
        }

        private EntryCommissionTermsVersion(
            string version,
            DateTime effectiveAt,
            decimal levelOneComponentAmount,
            decimal levelTwoComponentAmount,
            decimal levelThreeComponentAmount,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "A commission terms version identifier is required.",
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
                    "Commission terms may only become effective at a Friday 00:00 Africa/Johannesburg cycle boundary.",
                    nameof(effectiveAt));
            }

            EnsurePositive(
                levelOneComponentAmount,
                nameof(levelOneComponentAmount));
            EnsurePositive(
                levelTwoComponentAmount,
                nameof(levelTwoComponentAmount));
            EnsurePositive(
                levelThreeComponentAmount,
                nameof(levelThreeComponentAmount));
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
            LevelOneComponentAmount = levelOneComponentAmount;
            LevelTwoComponentAmount = levelTwoComponentAmount;
            LevelThreeComponentAmount = levelThreeComponentAmount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static EntryCommissionTermsVersion Create(
            string version,
            DateTime effectiveAt,
            decimal levelOneComponentAmount,
            decimal levelTwoComponentAmount,
            decimal levelThreeComponentAmount,
            string currency = "ZAR")
        {
            return new EntryCommissionTermsVersion(
                version,
                effectiveAt,
                levelOneComponentAmount,
                levelTwoComponentAmount,
                levelThreeComponentAmount,
                currency);
        }

        public EntryCommissionTerms ToTerms()
        {
            return EntryCommissionTerms.Create(
                Version,
                EffectiveAt,
                LevelOneComponentAmount,
                LevelTwoComponentAmount,
                LevelThreeComponentAmount,
                Currency);
        }

        private static void EnsurePositive(decimal amount, string parameterName)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Commission component amounts must be greater than zero.");
            }
        }
    }
}
