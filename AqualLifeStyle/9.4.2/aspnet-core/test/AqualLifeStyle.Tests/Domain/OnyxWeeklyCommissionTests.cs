using System;
using System.Collections.Generic;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
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
            var network = BuildActiveLevelOneNetwork(directRecruitCount: 4);

            var commission = Calculate(network);

            Assert.Equal(0, commission.HighestCompletedLevel);
            Assert.Equal(0m, commission.TotalAmount);
            Assert.Empty(commission.Components);
            Assert.Equal(WeeklyCommissionPayoutStatus.NotEarned, commission.PayoutStatus);
        }

        [Fact]
        public void FiveActiveDirectRecruits_RecordOneTwoHundredFiftyRandCommission()
        {
            var network = BuildActiveLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.LevelOneBranchSize);

            var commission = Calculate(network);

            Assert.Equal(1, commission.HighestCompletedLevel);
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
            var network = BuildActiveLevelOneNetwork(directRecruitCount: 4);
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
            var network = BuildActiveLevelOneNetwork(directRecruitCount: 4);
            network.Add(CreateActiveUnderRecruiter(
                customerId: 20,
                network[0],
                tenantId: 2));

            var commission = Calculate(network);

            Assert.Equal(1, commission.HighestCompletedLevel);
            Assert.Equal(250m, commission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
        }

        [Fact]
        public void DeeperNetwork_DoesNotCreateUnapprovedOnyxEarnings()
        {
            var network = BuildActiveLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.LevelOneBranchSize);
            var directRecruit = network[1];
            for (var index = 0; index < 5; index++)
            {
                network.Add(CreateActiveUnderRecruiter(100 + index, directRecruit));
            }

            var commission = Calculate(network);

            Assert.Equal(1, commission.HighestCompletedLevel);
            Assert.Equal(250m, commission.TotalAmount);
            Assert.Single(commission.Components);
        }

        [Fact]
        public void EarnedCommission_IsReleasedAndPaidThroughIdempotentTransitions()
        {
            var network = BuildActiveLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.LevelOneBranchSize);
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

        private static List<OnyxParticipation> BuildActiveLevelOneNetwork(
            int directRecruitCount)
        {
            var root = OnyxParticipation.StartDirectIndependently(
                1,
                1,
                7,
                PlanTerms,
                EffectiveFrom);
            Activate(root);
            var network = new List<OnyxParticipation> { root };

            for (var index = 0; index < directRecruitCount; index++)
            {
                network.Add(CreateActiveUnderRecruiter(index + 2, root));
            }

            return network;
        }

        private static OnyxParticipation CreateActiveUnderRecruiter(
            int customerId,
            OnyxParticipation recruiter,
            int tenantId = 1)
        {
            var participation = OnyxParticipation.StartDirectUnderRecruiter(
                tenantId,
                customerId,
                recruiter,
                7,
                PlanTerms,
                EffectiveFrom);
            Activate(participation);
            return participation;
        }

        private static void Activate(OnyxParticipation participation)
        {
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "Yoco",
                $"onyx-direct-{participation.CustomerId}",
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);
        }
    }
}
