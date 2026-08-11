using System;
using System.Collections.Generic;
using System.Linq;
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
                "onyx-commission-2026-07-levels-1-5",
                EffectiveFrom,
                50m,
                20m,
                12.62m,
                5m,
                4m);

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
        public void ActiveCrossTenantDirectRecruit_IsRejected()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                directRecruitCount: 4,
                PlanTerms,
                EffectiveFrom);
            var otherTenantRecruiter =
                OnyxNetworkTestBuilder.CreateActiveIndependentParticipant(
                20,
                PlanTerms,
                EffectiveFrom,
                tenantId: 2);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxParticipation.StartDirectUnderRecruiter(
                    1,
                    21,
                    otherTenantRecruiter,
                    7,
                    PlanTerms,
                    EffectiveFrom));
        }

        [Fact]
        public void MixedTenantNetworkInput_FailsClosed()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                directRecruitCount: 4,
                PlanTerms,
                EffectiveFrom);
            network.Add(OnyxNetworkTestBuilder.CreateActiveIndependentParticipant(
                20,
                PlanTerms,
                EffectiveFrom,
                tenantId: 2));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                EffectiveProgrammeNetwork.BuildOnyx(
                    expectedTenantId: 1,
                    network,
                    EffectiveFrom.AddDays(7)));

            Assert.Contains("outside Tenant 1", exception.Message);
        }

        [Fact]
        public void MoreThanFiveActiveDirectRecruits_UsesTheEarliestFive()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize + 1,
                PlanTerms,
                EffectiveFrom);

            var commission = Calculate(network);

            Assert.Equal((int)OnyxNetworkLevel.Level1, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(250m, commission.TotalAmount);
        }

        [Fact]
        public void CutoffNetwork_UsesPlacementBeforeAPostCutoffCorrection()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize,
                PlanTerms,
                EffectiveFrom);
            var originalRecruiter = network[0];
            var correctedParticipation = network[1];
            var newRecruiter = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 100,
                onyxMembershipId: 7,
                PlanTerms,
                EffectiveFrom);
            var payment = AqualLifeStyle.Domain.Payments.MemberPayment.CreatePending(
                1,
                100,
                AqualLifeStyle.Domain.Payments.MemberPaymentPurpose.OnyxDirectEntry,
                PlanTerms.DirectEntryAmount,
                "Test",
                "new-recruiter",
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            newRecruiter.ApplyConfirmedDirectEntryPayment(payment);
            newRecruiter.ApproveByAdministrator(1, EffectiveFrom.AddMinutes(2));
            network.Add(newRecruiter);
            var cutoff = EffectiveFrom.AddDays(7);
            correctedParticipation.CorrectRecruiter(
                newRecruiter,
                administratorUserId: 1,
                reason: "Correct placement",
                correctedAt: cutoff.AddMinutes(1));

            var cutoffNetwork = EffectiveProgrammeNetwork.BuildOnyx(1, network, cutoff);
            var evaluator = new OnyxNetworkQualificationEvaluator();

            Assert.Equal(
                OnyxNetworkLevel.Level1,
                evaluator.Evaluate(originalRecruiter.CustomerId, cutoffNetwork));
            Assert.Equal(
                OnyxNetworkLevel.None,
                evaluator.Evaluate(newRecruiter.CustomerId, cutoffNetwork));
        }

        [Fact]
        public void EarliestFive_DoNotUseALaterMoreFavourableBranch()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize + 1,
                PlanTerms,
                EffectiveFrom);
            var directRecruits = network.Skip(1)
                .OrderBy(participation => participation.Id)
                .ToList();
            var recruitersWithCompleteDownlines = directRecruits
                .Take(4)
                .Append(directRecruits[5]);
            var nextCustomerId = 100;
            foreach (var recruiter in recruitersWithCompleteDownlines)
            {
                for (var index = 0;
                     index < OnyxNetworkQualificationEvaluator.BranchSize;
                     index++)
                {
                    network.Add(OnyxNetworkTestBuilder.CreateActiveUnderRecruiter(
                        nextCustomerId++,
                        recruiter,
                        PlanTerms,
                        EffectiveFrom));
                }
            }

            var effectiveNetwork = EffectiveProgrammeNetwork.BuildOnyx(
                1,
                network,
                EffectiveFrom.AddDays(7));
            var level = new OnyxNetworkQualificationEvaluator().Evaluate(
                network[0].CustomerId,
                effectiveNetwork);

            Assert.Equal(OnyxNetworkLevel.Level1, level);
        }

        [Fact]
        public void CurrentNetwork_OrdersByCurrentPlacementEffectiveTime()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize,
                PlanTerms,
                EffectiveFrom.AddDays(1));
            var root = network[0];
            var otherRecruiter = OnyxParticipation.StartDirectIndependently(
                1,
                90,
                7,
                PlanTerms,
                EffectiveFrom);
            var recruiterPayment = AqualLifeStyle.Domain.Payments.MemberPayment.CreatePending(
                1,
                90,
                AqualLifeStyle.Domain.Payments.MemberPaymentPurpose.OnyxDirectEntry,
                PlanTerms.DirectEntryAmount,
                "Test",
                "current-order-recruiter",
                EffectiveFrom);
            recruiterPayment.Confirm(EffectiveFrom.AddMinutes(1));
            otherRecruiter.ApplyConfirmedDirectEntryPayment(recruiterPayment);
            otherRecruiter.ApproveByAdministrator(1, EffectiveFrom.AddMinutes(2));
            var movedRecruit = OnyxNetworkTestBuilder.CreateActiveUnderRecruiter(
                91,
                otherRecruiter,
                PlanTerms,
                EffectiveFrom);
            movedRecruit.CorrectRecruiter(
                root,
                1,
                "Move after the original five placements",
                EffectiveFrom.AddDays(2));
            network.Add(otherRecruiter);
            network.Add(movedRecruit);

            var nextCustomerId = 100;
            foreach (var recruiter in network.Skip(1).Take(4).Append(movedRecruit))
            {
                for (var index = 0;
                     index < OnyxNetworkQualificationEvaluator.BranchSize;
                     index++)
                {
                    network.Add(OnyxNetworkTestBuilder.CreateActiveUnderRecruiter(
                        nextCustomerId++,
                        recruiter,
                        PlanTerms,
                        EffectiveFrom.AddDays(2)));
                }
            }

            var level = new OnyxNetworkQualificationEvaluator().Evaluate(root, network);

            Assert.Equal(OnyxNetworkLevel.Level1, level);
        }

        [Fact]
        public void IncompleteLevelTwo_DoesNotEarnAPartialLevelTwoComponent()
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
        public void CompleteLevelTwo_RecordsSeparateCumulativeComponents()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 2,
                PlanTerms,
                EffectiveFrom);

            var commission = Calculate(network);

            Assert.Equal(2, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(2, commission.HighestCommissionedLevel);
            Assert.Equal(750m, commission.TotalAmount);
            Assert.Collection(
                commission.Components,
                levelOne =>
                {
                    Assert.Equal(1, levelOne.Level);
                    Assert.Equal(250m, levelOne.Amount);
                },
                levelTwo =>
                {
                    Assert.Equal(2, levelTwo.Level);
                    Assert.Equal(500m, levelTwo.Amount);
                });
        }

        [Fact]
        public void QualifiedNetwork_EarnsAgainInEachSubsequentClosedCycle()
        {
            var network = OnyxNetworkTestBuilder.BuildLevelOneNetwork(
                OnyxNetworkQualificationEvaluator.BranchSize,
                PlanTerms,
                EffectiveFrom);
            var firstPeriod = CreatePeriod(EffectiveFrom.AddDays(5));
            var secondPeriod = CreatePeriod(EffectiveFrom.AddDays(12));

            var firstCommission = Calculate(network, firstPeriod);
            var secondCommission = Calculate(network, secondPeriod);

            Assert.Equal(250m, firstCommission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned,
                firstCommission.PayoutStatus);
            Assert.Equal(250m, secondCommission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned,
                secondCommission.PayoutStatus);
            Assert.NotEqual(firstCommission.CommissionPeriodId,
                secondCommission.CommissionPeriodId);
        }

        [Fact]
        public void SamePeriodReevaluation_YieldsIdenticalResultWithoutDuplicateComponents()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 2,
                PlanTerms,
                EffectiveFrom);
            var period = CreatePeriod(EffectiveFrom.AddDays(5));

            var first = Calculate(network, period);
            var repeated = Calculate(network, period);

            Assert.Equal(first.TotalAmount, repeated.TotalAmount);
            Assert.Equal(first.Components.Count, repeated.Components.Count);
            Assert.Equal(first.HighestCommissionedLevel,
                repeated.HighestCommissionedLevel);
        }

        [Fact]
        public void CompleteLevelFive_UsesExactConfirmedRatesAndAuditableComponents()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 5,
                PlanTerms,
                EffectiveFrom);

            var commission = Calculate(network);

            Assert.Equal(5, commission.HighestQualifiedNetworkLevel);
            Assert.Equal(5, commission.HighestCommissionedLevel);
            Assert.Equal(17952.50m, commission.TotalAmount);
            Assert.Collection(
                commission.Components,
                component => AssertComponent(component, 1, 250m),
                component => AssertComponent(component, 2, 500m),
                component => AssertComponent(component, 3, 1577.50m),
                component => AssertComponent(component, 4, 3125m),
                component => AssertComponent(component, 5, 12500m));
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
        public void Terms_UseTheExactConfirmedPerPersonRates()
        {
            Assert.Equal(50m, CommissionTerms.GetPerPersonRate(OnyxNetworkLevel.Level1));
            Assert.Equal(20m, CommissionTerms.GetPerPersonRate(OnyxNetworkLevel.Level2));
            Assert.Equal(12.62m, CommissionTerms.GetPerPersonRate(OnyxNetworkLevel.Level3));
            Assert.Equal(5m, CommissionTerms.GetPerPersonRate(OnyxNetworkLevel.Level4));
            Assert.Equal(4m, CommissionTerms.GetPerPersonRate(OnyxNetworkLevel.Level5));
        }

        [Fact]
        public void Terms_RejectANonCommissionLevel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CommissionTerms.GetLevelComponentAmount(OnyxNetworkLevel.None));
        }

        private static void AssertComponent(
            OnyxCommissionComponent component,
            int expectedLevel,
            decimal expectedAmount)
        {
            Assert.Equal(expectedLevel, component.Level);
            Assert.Equal(expectedAmount, component.Amount);
        }

        private static OnyxWeeklyCommission Calculate(
            IReadOnlyCollection<OnyxParticipation> network)
        {
            return Calculate(network, CreatePeriod(EffectiveFrom.AddDays(5)));
        }

        private static OnyxCommissionPeriod CreatePeriod(DateTime periodStart)
        {
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            return OnyxCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1), CommissionTerms);
        }

        private static OnyxWeeklyCommission Calculate(
            IReadOnlyCollection<OnyxParticipation> network,
            OnyxCommissionPeriod period)
        {
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
