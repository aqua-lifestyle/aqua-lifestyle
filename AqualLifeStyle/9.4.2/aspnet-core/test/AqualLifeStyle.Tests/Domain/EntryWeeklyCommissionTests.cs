using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EntryWeeklyCommissionTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms ProgrammeTerms =
            EntryProgrammeTerms.Create(
                "2026-07",
                EffectiveFrom,
                600m,
                600m,
                600m,
                7);

        private static readonly EntryCommissionTerms CommissionTerms =
            EntryCommissionTerms.Create(
                "2026-07",
                EffectiveFrom,
                150m,
                250m,
                1250m);

        [Fact]
        public void IncompleteLevel_RecordsNoPartialCommission()
        {
            var network = BuildNetwork(maxDepth: 1, incompleteRecruiterId: 1);
            var commission = Calculate(network);

            Assert.Equal(0, commission.HighestCompletedLevel);
            Assert.Empty(commission.Components);
            Assert.Equal(0m, commission.TotalAmount);
            Assert.Equal(EntryCommissionPayoutStatus.NotEarned, commission.PayoutStatus);
        }

        [Fact]
        public void CompleteLevels_RecordSeparateCumulativeComponents()
        {
            var network = BuildNetwork(maxDepth: 3);
            var commission = Calculate(network);

            Assert.Equal(3, commission.HighestCompletedLevel);
            Assert.Collection(
                commission.Components.OrderBy(component => component.Level),
                levelOne =>
                {
                    Assert.Equal(1, levelOne.Level);
                    Assert.Equal(150m, levelOne.Amount);
                },
                levelTwo =>
                {
                    Assert.Equal(2, levelTwo.Level);
                    Assert.Equal(250m, levelTwo.Amount);
                },
                levelThree =>
                {
                    Assert.Equal(3, levelThree.Level);
                    Assert.Equal(1250m, levelThree.Amount);
                });
            Assert.Equal(1650m, commission.TotalAmount);
            Assert.Equal(EntryCommissionPayoutStatus.Earned, commission.PayoutStatus);
        }

        [Fact]
        public void OverdueOwnObligation_HoldsPayoutWithoutChangingNetworkQualification()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1));
            obligation.AssessStatus(EffectiveFrom.AddDays(9));

            var commission = Calculate(network, new[] { obligation });

            Assert.Equal(1, commission.HighestCompletedLevel);
            Assert.Equal(150m, commission.TotalAmount);
            Assert.Equal(EntryCommissionPayoutStatus.Held, commission.PayoutStatus);
            Assert.Equal("Entry monthly commitment is overdue.", commission.HoldReason);
            Assert.True(root.IsQualifiedForNetwork);
        }

        [Fact]
        public void HeldPayout_IsReleasedAndPaidThroughExplicitIdempotentTransitions()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1));
            obligation.AssessStatus(EffectiveFrom.AddDays(9));
            var commission = Calculate(network, new[] { obligation });
            var releasedAt = EffectiveFrom.AddDays(15);

            commission.ReleaseHeldPayoutAfterComplianceRestored(
                releasedAt,
                "The outstanding monthly commitment was paid.");
            commission.ReleaseHeldPayoutAfterComplianceRestored(
                releasedAt,
                "The outstanding monthly commitment was paid.");
            commission.MarkPaid(releasedAt.AddHours(1), "payout-2026-07-1");
            commission.MarkPaid(releasedAt.AddHours(1), "payout-2026-07-1");

            Assert.Equal(EntryCommissionPayoutStatus.Paid, commission.PayoutStatus);
            Assert.Equal(releasedAt, commission.ReleasedAt);
            Assert.Equal("The outstanding monthly commitment was paid.", commission.ReleaseReason);
            Assert.Equal("payout-2026-07-1", commission.PaymentReference);
            Assert.Equal(150m, commission.TotalAmount);
            Assert.Single(commission.Components);
        }

        [Fact]
        public void CommissionPeriod_CannotBeCalculatedBeforeItCloses()
        {
            var periodStart = EffectiveFrom.AddDays(5);
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);

            Assert.Throws<ArgumentException>(() =>
                EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    periodStart,
                    periodEnd,
                    "Africa/Johannesburg",
                    periodEnd,
                    CommissionTerms));
        }

        private static EntryWeeklyCommission Calculate(
            IReadOnlyCollection<EntryParticipation> network,
            IEnumerable<EntryMonthlyObligation> obligations = null)
        {
            var periodStart = EffectiveFrom.AddDays(5);
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            var period = EntryCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                CommissionTerms);
            var calculator = new EntryWeeklyCommissionCalculator(
                new EntryNetworkQualificationEvaluator());

            return calculator.Calculate(
                network.Single(participation => participation.CustomerId == 1),
                period,
                CommissionTerms,
                network,
                obligations ?? Array.Empty<EntryMonthlyObligation>());
        }

        private static List<EntryParticipation> BuildNetwork(
            int maxDepth,
            int? incompleteRecruiterId = null)
        {
            var root = CreateQualifiedIndependentParticipation(customerId: 1);
            var participations = new List<EntryParticipation> { root };
            var currentLevel = new List<EntryParticipation> { root };
            var nextCustomerId = 2;

            for (var depth = 1; depth <= maxDepth; depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    var recruitCount = recruiter.CustomerId == incompleteRecruiterId
                        ? EntryNetworkQualificationEvaluator.BranchSize - 1
                        : EntryNetworkQualificationEvaluator.BranchSize;
                    for (var index = 0; index < recruitCount; index++)
                    {
                        var recruit = CreateQualifiedParticipation(
                            nextCustomerId,
                            recruiter);
                        participations.Add(recruit);
                        nextLevel.Add(recruit);
                        nextCustomerId++;
                    }
                }

                currentLevel = nextLevel;
            }

            return participations;
        }

        private static EntryParticipation CreateQualifiedIndependentParticipation(
            int customerId)
        {
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                ProgrammeTerms,
                EffectiveFrom);
            Activate(participation);
            return participation;
        }

        private static EntryParticipation CreateQualifiedParticipation(
            int customerId,
            EntryParticipation recruiter)
        {
            var participation = EntryParticipation.StartUnderRecruiter(
                1,
                customerId,
                recruiter,
                ProgrammeTerms,
                EffectiveFrom);
            Activate(participation);
            return participation;
        }

        private static void Activate(EntryParticipation participation)
        {
            var registration = CreatePayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                $"registration-{participation.CustomerId}");
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = CreatePayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                $"activation-{participation.CustomerId}");
            participation.ApplyConfirmedActivationPayment(activation);
        }

        private static MemberPayment CreatePayment(
            int customerId,
            MemberPaymentPurpose purpose,
            string externalReference)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                600m,
                "Yoco",
                externalReference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            return payment;
        }
    }
}
