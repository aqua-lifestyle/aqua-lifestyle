using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AqualLifeStyle.Domain.Onyx;
using Xunit;
using Xunit.Abstractions;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxProgrammeEngineSimulationTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly OnyxPlanTerms PlanTerms = OnyxPlanTerms.Create(
            "onyx-2026-07",
            EffectiveFrom,
            6120m);

        private static readonly OnyxCommissionTerms CommissionTerms =
            OnyxCommissionTerms.Create(
                "onyx-commission-2026-07-levels-1-5",
                EffectiveFrom,
                50m,
                20m,
                12.62m,
                5m,
                4m);

        private readonly ITestOutputHelper _output;

        public OnyxProgrammeEngineSimulationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CompleteLevelFiveNetwork_3906Participants_DegradesDeterministicallyByDepth()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 5,
                PlanTerms,
                EffectiveFrom);

            Assert.Equal(3906, network.Count);

            var depthByCustomerId = ComputeDepthByCustomerId(network);
            var byCustomerId = network.ToDictionary(
                participation => participation.CustomerId);

            var evaluator = new OnyxNetworkQualificationEvaluator();
            var calculator = new OnyxWeeklyCommissionCalculator(evaluator);
            var period = CreatePeriod();

            var expectedQualificationByDepth = new[]
            {
                5, 4, 3, 2, 1, 0
            };

            var stopwatch = Stopwatch.StartNew();
            var participantsAtDepth = new Dictionary<int, int>();
            var commissionByLevel = new Dictionary<int, decimal>();
            var aggregateCommission = 0m;
            var earnedCount = 0;

            foreach (var participation in network)
            {
                var depth = depthByCustomerId[participation.CustomerId];
                participantsAtDepth[depth] = participantsAtDepth.GetValueOrDefault(depth) + 1;

                var commission = calculator.Calculate(
                    participation,
                    period,
                    CommissionTerms,
                    network);

                var expectedQualifiedLevel = expectedQualificationByDepth[depth];
                Assert.Equal(
                    expectedQualifiedLevel,
                    commission.HighestQualifiedNetworkLevel);
                Assert.Equal(
                    expectedQualifiedLevel,
                    commission.HighestCommissionedLevel);

                var expectedCommission =
                    GetExpectedCommission(expectedQualifiedLevel);
                Assert.Equal(expectedCommission, commission.TotalAmount);
                Assert.Equal("ZAR", commission.Currency);

                if (expectedQualifiedLevel > 0)
                {
                    Assert.Equal(
                        WeeklyCommissionPayoutStatus.Earned,
                        commission.PayoutStatus);
                    earnedCount++;
                    Assert.Equal(expectedQualifiedLevel, commission.Components.Count);
                    commissionByLevel[expectedQualifiedLevel] =
                        commissionByLevel.GetValueOrDefault(expectedQualifiedLevel) + 1;
                }
                else
                {
                    Assert.Equal(
                        WeeklyCommissionPayoutStatus.NotEarned,
                        commission.PayoutStatus);
                    Assert.Empty(commission.Components);
                }

                aggregateCommission += commission.TotalAmount;
            }

            stopwatch.Stop();

            Assert.Equal(1, participantsAtDepth[0]);
            Assert.Equal(5, participantsAtDepth[1]);
            Assert.Equal(25, participantsAtDepth[2]);
            Assert.Equal(125, participantsAtDepth[3]);
            Assert.Equal(625, participantsAtDepth[4]);
            Assert.Equal(3125, participantsAtDepth[5]);

            Assert.Equal(1, commissionByLevel[5]);
            Assert.Equal(5, commissionByLevel[4]);
            Assert.Equal(25, commissionByLevel[3]);
            Assert.Equal(125, commissionByLevel[2]);
            Assert.Equal(625, commissionByLevel[1]);
            Assert.Equal(3125, 3906 - earnedCount);

            Assert.Equal(353402.50m, aggregateCommission);

            _output.WriteLine(
                $"Simulation complete: {network.Count} participants, " +
                $"{earnedCount} with earned commission, " +
                $"aggregate {aggregateCommission:C} ZAR, " +
                $"elapsed {stopwatch.ElapsedMilliseconds} ms.");
        }

        [Fact]
        public void CompleteLevelFiveNetwork_EveryParticipantCommissionIsBoundedByTheirDepth()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 5,
                PlanTerms,
                EffectiveFrom);
            var depthByCustomerId = ComputeDepthByCustomerId(network);
            var calculator = new OnyxWeeklyCommissionCalculator(
                new OnyxNetworkQualificationEvaluator());
            var period = CreatePeriod();

            foreach (var participation in network)
            {
                var commission = calculator.Calculate(
                    participation,
                    period,
                    CommissionTerms,
                    network);
                var depth = depthByCustomerId[participation.CustomerId];

                Assert.True(
                    commission.TotalAmount <= 17952.50m,
                    $"Participant at depth {depth} exceeded the maximum commission.");
                if (depth >= 5)
                {
                    Assert.Equal(0m, commission.TotalAmount);
                }
            }
        }

        private static decimal GetExpectedCommission(int qualifiedLevel)
        {
            var cumulativeByLevel = new Dictionary<int, decimal>
            {
                { 1, 250m },
                { 2, 500m },
                { 3, 1577.50m },
                { 4, 3125m },
                { 5, 12500m }
            };

            return cumulativeByLevel
                .Where(pair => pair.Key <= qualifiedLevel)
                .Sum(pair => pair.Value);
        }

        private static Dictionary<int, int> ComputeDepthByCustomerId(
            IReadOnlyCollection<OnyxParticipation> network)
        {
            var byCustomerId = network.ToDictionary(
                participation => participation.CustomerId);
            var depthByCustomerId = new Dictionary<int, int>();

            foreach (var participation in network)
            {
                var depth = 0;
                var current = participation;
                var visited = new HashSet<int>();
                while (current.RecruiterCustomerId.HasValue &&
                       visited.Add(current.CustomerId))
                {
                    depth++;
                    if (!byCustomerId.TryGetValue(
                            current.RecruiterCustomerId.Value,
                            out current))
                    {
                        throw new InvalidOperationException(
                            "Recruiter participation is missing from the network.");
                    }
                }

                depthByCustomerId[participation.CustomerId] = depth;
            }

            return depthByCustomerId;
        }

        private static OnyxCommissionPeriod CreatePeriod()
        {
            var periodStart = EffectiveFrom.AddDays(5);
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            return OnyxCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                CommissionTerms);
        }
    }
}
