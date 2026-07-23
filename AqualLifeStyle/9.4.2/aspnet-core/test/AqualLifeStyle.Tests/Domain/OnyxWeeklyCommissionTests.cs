using System;
using System.Collections.Generic;
using AqualLifeStyle.Domain.Onyx;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxWeeklyCommissionTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly OnyxPlanTerms PlanTerms = OnyxPlanTerms.Create(
            "onyx-2026-07",
            EffectiveFrom,
            6120m);

        private static readonly OnyxCommissionTerms CommissionTerms =
            OnyxCommissionTerms.Create(
                "onyx-commission-2026-07",
                EffectiveFrom,
                250m);

        [Fact]
        public void FourActiveDirectRecruits_RecordNoPartialCommission()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                directRecruitCount: 4,
                PlanTerms,
                EffectiveFrom);

            var commission = Calculate(network);

            Assert.Equal(0, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(0, commission.HighestCommissionedLevel);
            Assert.Equal(0m, commission.TotalAmount);
            Assert.Empty(commission.Components);
            Assert.Equal(WeeklyCommissionPayoutStatus.NotEarned, commission.PayoutStatus);
        }

        [Fact]
        public void FiveActiveDirectRecruits_RecordOneTwoHundredFiftyRandCommission()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize,
                PlanTerms,
                EffectiveFrom);

            var commission = Calculate(network);

            Assert.Equal(1, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(1, commission.HighestCommissionedLevel);
            Assert.Equal(250m, commission.TotalAmount);
            Assert.Equal("ZAR", commission.Currency);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
            var component = Assert.Single(commission.Components);
            Assert.Equal(1, component.Level);
            Assert.Equal(250m, component.Amount);
        }

        [Fact]
        public void InactiveDirectRecruit_DoesNotCompleteLevelOne()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                directRecruitCount: 4,
                PlanTerms,
                EffectiveFrom);
            var root = network[0];
            network.Add(OnyxParticipation.StartDirectUnderRecruiter(
                1,
                20,
                root,
                7,
                PlanTerms,
                EffectiveFrom));

            var commission = Calculate(network);

            Assert.Equal(WeeklyCommissionPayoutStatus.NotEarned, commission.PayoutStatus);
            Assert.Equal(0m, commission.TotalAmount);
        }

        [Fact]
        public void ActiveCrossAreaDirectRecruit_ContributesToLevelOne()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                directRecruitCount: 4,
                PlanTerms,
                EffectiveFrom);
            network.Add(OnyxNetworkTestBuilder.CreateActiveUnderRecruiter(
                customerId: 20,
                network[0],
                PlanTerms,
                EffectiveFrom,
                tenantId: 2));

            var commission = Calculate(network);

            Assert.Equal(1, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(1, commission.HighestCommissionedLevel);
            Assert.Equal(250m, commission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
        }

        [Fact]
        public void DeeperNetwork_DoesNotCreateUnapprovedOnyxEarnings()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize,
                PlanTerms,
                EffectiveFrom);
            var directRecruit = network[1];
            for (var index = 0; index < 5; index++)
            {
                network.Add(OnyxNetworkTestBuilder.CreateActiveUnderRecruiter(
                    100 + index,
                    directRecruit,
                    PlanTerms,
                    EffectiveFrom));
            }

            var commission = Calculate(network);

            Assert.Equal(1, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(1, commission.HighestCommissionedLevel);
            Assert.Equal(250m, commission.TotalAmount);
            Assert.Single(commission.Components);
        }

        [Fact]
        public void CompleteLevelTwo_RecordsQualificationButNoUnapprovedCommission()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 2,
                PlanTerms,
                EffectiveFrom);

            var commission = Calculate(network);

            Assert.Equal(2, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(1, commission.HighestCommissionedLevel);
            Assert.Equal(250m, commission.TotalAmount);
            Assert.Single(commission.Components);
        }

        [Fact]
        public void EarnedCommission_IsReleasedAndPaidThroughIdempotentTransitions()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize,
                PlanTerms,
                EffectiveFrom);
            var commission = Calculate(network);
            var releasedAt = EffectiveFrom.AddDays(13);
            var paidAt = releasedAt.AddHours(1);

            commission.ReleaseEligiblePayout(releasedAt);
            commission.ReleaseEligiblePayout(releasedAt);
            commission.MarkPaid(paidAt, "onyx-payout-2026-07-1");
            commission.MarkPaid(paidAt, "onyx-payout-2026-07-1");

            Assert.Equal(WeeklyCommissionPayoutStatus.Paid, commission.PayoutStatus);
            Assert.Equal(releasedAt, commission.ReleasedAt);
            Assert.Equal(paidAt, commission.PaidAt);
            Assert.Equal("onyx-payout-2026-07-1", commission.PaymentReference);
        }

        [Fact]
        public void Terms_RejectAnUnapprovedCommissionLevel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CommissionTerms.GetCommissionAmount(OnyxNetworkLevel.None));
        }

        private static OnyxWeeklyCommission Calculate(
            IReadOnlyCollection<OnyxParticipation> network)
        {
            var periodStart = EffectiveFrom.AddDays(5);
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            var period = OnyxCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                CommissionTerms);
            var calculator = new OnyxWeeklyCommissionCalculator(
                new OnyxNetworkQualificationEvaluator());

            return calculator.Calculate(network is List<OnyxParticipation> list
                    ? list[0]
                    : throw new InvalidOperationException("A test network must be ordered."),
                period,
                CommissionTerms,
                network);
        }

    }
}
