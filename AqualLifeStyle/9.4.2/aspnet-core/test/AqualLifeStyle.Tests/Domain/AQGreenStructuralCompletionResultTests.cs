using System;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public sealed class AQGreenStructuralCompletionResultTests
    {
        private static readonly DateTime Cutoff =
            new(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void QualifyingCountsPreserveDeeperOccupancyWithoutBypassingPrerequisite()
        {
            var result = Result(
                AQGreenStructuralCompletionLevel.Level0,
                depth1: 4,
                depth2: 18,
                depth3: 27);

            result.QualifyingDepth1Count.ShouldBe(4);
            result.QualifyingDepth2Count.ShouldBe(18);
            result.QualifyingDepth3Count.ShouldBe(27);
            result.GetQualifyingCountAtRelativeDepth(2).ShouldBe(18);
        }

        [Fact]
        public void CompletionLevelMustAgreeWithQualifyingEvidence()
        {
            Should.Throw<AQGreenPlacementTopologyIntegrityException>(() =>
                Result(
                    AQGreenStructuralCompletionLevel.Level1,
                    depth1: 4,
                    depth2: 25,
                    depth3: 125));
        }

        [Fact]
        public void UnknownCompletionLevelFailsAsIntegrity_NotLevelZero()
        {
            var exception = Should.Throw<AQGreenPlacementTopologyIntegrityException>(() =>
                Result((AQGreenStructuralCompletionLevel)99, 0, 0, 0));

            exception.Message.ShouldContain("unsupported completion level 99");
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(6, 0, 0)]
        [InlineData(5, 26, 0)]
        [InlineData(5, 25, 126)]
        public void QualifyingCountsOutsidePlacementCapacityFailIntegrity(
            int depth1,
            int depth2,
            int depth3)
        {
            Should.Throw<AQGreenPlacementTopologyIntegrityException>(() =>
                Result(
                    AQGreenStructuralCompletionLevel.Level0,
                    depth1,
                    depth2,
                    depth3));
        }

        private static AQGreenStructuralCompletionResult Result(
            AQGreenStructuralCompletionLevel level,
            int depth1,
            int depth2,
            int depth3) =>
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                level,
                depth1,
                depth2,
                depth3,
                Cutoff,
                AQGreenPlacementRules.CurrentVersion);
    }
}
