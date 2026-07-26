using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class OnyxCommissionTerms
    {
        public string Version { get; }
        public DateTime EffectiveFrom { get; }
        public decimal LevelOnePerPersonRate { get; }
        public decimal LevelTwoPerPersonRate { get; }
        public decimal LevelThreePerPersonRate { get; }
        public decimal LevelFourPerPersonRate { get; }
        public decimal LevelFivePerPersonRate { get; }
        public string Currency { get; }

        private OnyxCommissionTerms(
            string version,
            DateTime effectiveFrom,
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
                    "An Onyx commission terms version is required.",
                    nameof(version));
            }

            if (effectiveFrom == default)
            {
                throw new ArgumentException("An effective date is required.", nameof(effectiveFrom));
            }

            EnsurePositiveRate(levelOnePerPersonRate, nameof(levelOnePerPersonRate), 1);
            EnsurePositiveRate(levelTwoPerPersonRate, nameof(levelTwoPerPersonRate), 2);
            EnsurePositiveRate(levelThreePerPersonRate, nameof(levelThreePerPersonRate), 3);
            EnsurePositiveRate(levelFourPerPersonRate, nameof(levelFourPerPersonRate), 4);
            EnsurePositiveRate(levelFivePerPersonRate, nameof(levelFivePerPersonRate), 5);

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException(
                    "A three-letter currency code is required.",
                    nameof(currency));
            }

            Version = version.Trim();
            EffectiveFrom = effectiveFrom;
            LevelOnePerPersonRate = levelOnePerPersonRate;
            LevelTwoPerPersonRate = levelTwoPerPersonRate;
            LevelThreePerPersonRate = levelThreePerPersonRate;
            LevelFourPerPersonRate = levelFourPerPersonRate;
            LevelFivePerPersonRate = levelFivePerPersonRate;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static OnyxCommissionTerms Create(
            string version,
            DateTime effectiveFrom,
            decimal levelOnePerPersonRate,
            decimal levelTwoPerPersonRate,
            decimal levelThreePerPersonRate,
            decimal levelFourPerPersonRate,
            decimal levelFivePerPersonRate,
            string currency = "ZAR")
        {
            return new OnyxCommissionTerms(
                version,
                effectiveFrom,
                levelOnePerPersonRate,
                levelTwoPerPersonRate,
                levelThreePerPersonRate,
                levelFourPerPersonRate,
                levelFivePerPersonRate,
                currency);
        }

        public decimal GetPerPersonRate(OnyxNetworkLevel level)
        {
            return level switch
            {
                OnyxNetworkLevel.Level1 => LevelOnePerPersonRate,
                OnyxNetworkLevel.Level2 => LevelTwoPerPersonRate,
                OnyxNetworkLevel.Level3 => LevelThreePerPersonRate,
                OnyxNetworkLevel.Level4 => LevelFourPerPersonRate,
                OnyxNetworkLevel.Level5 => LevelFivePerPersonRate,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(level),
                    "An Onyx commission rate is available only for Levels 1 through 5.")
            };
        }

        public decimal GetLevelComponentAmount(OnyxNetworkLevel level)
        {
            return GetPerPersonRate(level) *
                OnyxNetworkQualificationEvaluator.GetRequiredPopulation(level);
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
