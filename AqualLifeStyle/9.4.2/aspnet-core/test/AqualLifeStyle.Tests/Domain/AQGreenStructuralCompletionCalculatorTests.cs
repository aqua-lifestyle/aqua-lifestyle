using System;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public sealed class AQGreenStructuralCompletionCalculatorTests
    {
        [Theory]
        [InlineData(1, 5)]
        [InlineData(2, 25)]
        [InlineData(3, 125)]
        public void RequiredPopulation_IsFiveWideToAQGreenLevelThree(
            int relativeDepth,
            int expectedPopulation)
        {
            AQGreenStructuralCompletionCalculator
                .GetRequiredPopulation(relativeDepth)
                .ShouldBe(expectedPopulation);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        public void IncompleteDepthOne_IsLevelZero(int depthOnePopulation)
        {
            Evaluate(depthOnePopulation, 25, 125)
                .ShouldBe(AQGreenStructuralCompletionLevel.Level0);
        }

        [Fact]
        public void CompleteDepthOne_IsLevelOne()
        {
            Evaluate(5, 0, 0)
                .ShouldBe(AQGreenStructuralCompletionLevel.Level1);
        }

        [Fact]
        public void IncompleteDepthTwo_RemainsLevelOne()
        {
            Evaluate(5, 24, 125)
                .ShouldBe(AQGreenStructuralCompletionLevel.Level1);
        }

        [Fact]
        public void CompleteDepthTwo_IsLevelTwo()
        {
            Evaluate(5, 25, 0)
                .ShouldBe(AQGreenStructuralCompletionLevel.Level2);
        }

        [Fact]
        public void IncompleteDepthThree_RemainsLevelTwo()
        {
            Evaluate(5, 25, 124)
                .ShouldBe(AQGreenStructuralCompletionLevel.Level2);
        }

        [Fact]
        public void CompleteDepthThree_IsMaximumLevelThree()
        {
            var maximumDepthRequested = 0;

            var level = AQGreenStructuralCompletionCalculator.Evaluate(depth =>
            {
                maximumDepthRequested = Math.Max(maximumDepthRequested, depth);
                return AQGreenStructuralCompletionCalculator.GetRequiredPopulation(depth);
            });

            level.ShouldBe(AQGreenStructuralCompletionLevel.Level3);
            maximumDepthRequested.ShouldBe(3);
        }

        [Fact]
        public void PopulationBeyondFiveWideCapacity_FailsClosed()
        {
            Should.Throw<InvalidOperationException>(() => Evaluate(6, 0, 0));
        }

        private static AQGreenStructuralCompletionLevel Evaluate(
            int depthOnePopulation,
            int depthTwoPopulation,
            int depthThreePopulation) =>
            AQGreenStructuralCompletionCalculator.Evaluate(depth => depth switch
            {
                1 => depthOnePopulation,
                2 => depthTwoPopulation,
                3 => depthThreePopulation,
                _ => throw new ArgumentOutOfRangeException(nameof(depth))
            });
    }
}
