using System;

namespace AqualLifeStyle.Domain.AQGreen
{
    public static class AQGreenStructuralCompletionCalculator
    {
        public const int BranchSize = 5;
        public const int MaximumLevel = 3;

        public static int GetRequiredPopulation(int relativeDepth)
        {
            if (relativeDepth < 1 || relativeDepth > MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(relativeDepth));

            var requiredPopulation = 1;
            for (var depth = 0; depth < relativeDepth; depth++)
            {
                requiredPopulation *= BranchSize;
            }

            return requiredPopulation;
        }

        /// <summary>
        /// In an acyclic rooted topology with at most five children per parent,
        /// the population at relative depth d cannot exceed 5^d. Therefore a
        /// qualifying count of 5^d proves that every position at that depth is
        /// occupied and qualifying. Requiring each preceding depth in order
        /// prevents deeper occupancy from compensating for a shallower gap.
        /// </summary>
        public static AQGreenStructuralCompletionLevel Evaluate(
            Func<int, int> getQualifyingPopulationAtRelativeDepth)
        {
            if (getQualifyingPopulationAtRelativeDepth == null)
                throw new ArgumentNullException(
                    nameof(getQualifyingPopulationAtRelativeDepth));

            var highestCompleteLevel = AQGreenStructuralCompletionLevel.Level0;
            for (var relativeDepth = 1;
                 relativeDepth <= MaximumLevel;
                 relativeDepth++)
            {
                var qualifyingPopulation =
                    getQualifyingPopulationAtRelativeDepth(relativeDepth);
                var requiredPopulation = GetRequiredPopulation(relativeDepth);
                if (qualifyingPopulation < 0 ||
                    qualifyingPopulation > requiredPopulation)
                {
                    throw new InvalidOperationException(
                        $"AQGreen relative depth {relativeDepth} contains " +
                        $"{qualifyingPopulation} qualifying participants; " +
                        $"the five-wide capacity is {requiredPopulation}.");
                }

                if (qualifyingPopulation != requiredPopulation)
                {
                    break;
                }

                highestCompleteLevel =
                    (AQGreenStructuralCompletionLevel)relativeDepth;
            }

            return highestCompleteLevel;
        }
    }
}
