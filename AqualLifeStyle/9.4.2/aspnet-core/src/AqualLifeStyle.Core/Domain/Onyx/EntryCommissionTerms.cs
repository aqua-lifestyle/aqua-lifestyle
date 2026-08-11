using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class EntryCommissionTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal LevelOneComponentAmount { get; }
        public decimal LevelTwoComponentAmount { get; }
        public decimal LevelThreeComponentAmount { get; }
        public EntryNetworkLevel HighestAuthorisedCommissionLevel =>
            EntryNetworkLevel.Level3;
        public string Currency { get; }

        private EntryCommissionTerms(
            string version,
            DateTime effectiveFrom,
            decimal levelOneComponentAmount,
            decimal levelTwoComponentAmount,
            decimal levelThreeComponentAmount,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A commission terms version is required.", nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            EnsurePositive(levelOneComponentAmount, nameof(levelOneComponentAmount));
            EnsurePositive(levelTwoComponentAmount, nameof(levelTwoComponentAmount));
            EnsurePositive(levelThreeComponentAmount, nameof(levelThreeComponentAmount));
            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException("A three-letter currency code is required.", nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            LevelOneComponentAmount = levelOneComponentAmount;
            LevelTwoComponentAmount = levelTwoComponentAmount;
            LevelThreeComponentAmount = levelThreeComponentAmount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static EntryCommissionTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal levelOneComponentAmount,
            decimal levelTwoComponentAmount,
            decimal levelThreeComponentAmount,
            string currency = "ZAR")
        {
            return new EntryCommissionTerms(
                version,
                effectiveFrom,
                levelOneComponentAmount,
                levelTwoComponentAmount,
                levelThreeComponentAmount,
                currency);
        }

        public decimal GetComponentAmount(int level)
        {
            return level switch
            {
                1 => LevelOneComponentAmount,
                2 => LevelTwoComponentAmount,
                3 => LevelThreeComponentAmount,
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            };
        }

        public EntryNetworkLevel GetHighestCommissionedLevel(
            EntryNetworkLevel highestQualifiedNetworkLevel)
        {
            if (highestQualifiedNetworkLevel < EntryNetworkLevel.None ||
                highestQualifiedNetworkLevel > EntryNetworkLevel.Level3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(highestQualifiedNetworkLevel));
            }

            return highestQualifiedNetworkLevel <= HighestAuthorisedCommissionLevel
                ? highestQualifiedNetworkLevel
                : HighestAuthorisedCommissionLevel;
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
