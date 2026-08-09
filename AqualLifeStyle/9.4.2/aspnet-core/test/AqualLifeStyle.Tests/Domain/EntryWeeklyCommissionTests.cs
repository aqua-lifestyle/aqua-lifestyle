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

        private static readonly OnyxLoanTerms LoanTerms =
            OnyxLoanTerms.Create(
                "2026-07",
                EffectiveFrom,
                6120m,
                30m,
                3,
                4,
                200m);

        [Fact]
        public void IncompleteLevel_RecordsNoPartialCommission()
        {
            var network = BuildNetwork(maxDepth: 1, incompleteRecruiterId: 1);
            var commission = Calculate(network);

            Assert.Equal(0, commission.HighestCompletedLevel);
            Assert.Empty(commission.Components);
            Assert.Equal(0m, commission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.NotEarned, commission.PayoutStatus);
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
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
        }

        [Fact]
        public void QualifiedNetwork_EarnsAgainInEachSubsequentClosedCycle()
        {
            var network = BuildNetwork(maxDepth: 1);
            var firstPeriod = CreatePeriod(EffectiveFrom.AddDays(5));
            var secondPeriod = CreatePeriod(EffectiveFrom.AddDays(12));

            var firstCommission = CalculateForPeriod(network, firstPeriod);
            var secondCommission = CalculateForPeriod(network, secondPeriod);

            Assert.Equal(1, firstCommission.HighestCompletedLevel);
            Assert.Single(firstCommission.Components);
            Assert.Equal(1, firstCommission.Components.Single().Level);
            Assert.Equal(150m, firstCommission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, firstCommission.PayoutStatus);

            Assert.Equal(1, secondCommission.HighestCompletedLevel);
            Assert.Single(secondCommission.Components);
            Assert.Equal(1, secondCommission.Components.Single().Level);
            Assert.Equal(150m, secondCommission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, secondCommission.PayoutStatus);

            Assert.NotEqual(firstCommission.Id, secondCommission.Id);
            Assert.NotEqual(firstCommission.CommissionPeriodId, secondCommission.CommissionPeriodId);
        }

        [Fact]
        public void SamePeriodReevaluation_YieldsIdenticalResult_NoDuplicateComponents()
        {
            var network = BuildNetwork(maxDepth: 3);
            var period = CreatePeriod(EffectiveFrom.AddDays(5));

            var first = CalculateForPeriod(network, period);
            var repeated = CalculateForPeriod(network, period);

            Assert.Equal(first.HighestCompletedLevel, repeated.HighestCompletedLevel);
            Assert.Equal(first.TotalAmount, repeated.TotalAmount);
            Assert.Equal(first.Components.Count, repeated.Components.Count);
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
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
            obligation.AssessStatus(EffectiveFrom.AddDays(9));

            var commission = Calculate(network, new[] { obligation });

            Assert.Equal(1, commission.HighestCompletedLevel);
            Assert.Equal(150m, commission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, commission.PayoutStatus);
            Assert.Equal("AQGreen monthly commitment is overdue.", commission.HoldReason);
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
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
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

            Assert.Equal(WeeklyCommissionPayoutStatus.Paid, commission.PayoutStatus);
            Assert.Equal(releasedAt, commission.ReleasedAt);
            Assert.Equal("The outstanding monthly commitment was paid.", commission.ReleaseReason);
            Assert.Equal("payout-2026-07-1", commission.PaymentReference);
            Assert.Equal(150m, commission.TotalAmount);
            Assert.Single(commission.Components);
        }

        [Fact]
        public void OverdueOwnLoan_HoldsPayoutWithoutChangingNetworkQualification()
        {
            var network = BuildNetwork(maxDepth: 2);
            var root = network.Single(participation => participation.CustomerId == 1);
            var loan = CreateActiveLoan(network);
            loan.AssessCompliance(loan.EffectiveAt.Value.AddDays(8));

            var commission = Calculate(
                network,
                loans: new[] { loan });

            Assert.Equal(2, commission.HighestCompletedLevel);
            Assert.Equal(400m, commission.TotalAmount);
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, commission.PayoutStatus);
            Assert.Equal("Onyx loan repayment is overdue.", commission.HoldReason);
            Assert.True(root.IsQualifiedForNetwork);
        }

        [Fact]
        public void EarnedPayout_CanBeHeldAndIdempotentlyReleasedAfterLoanCatchUp()
        {
            var network = BuildNetwork(maxDepth: 2);
            var loan = CreateActiveLoan(network);
            var commission = Calculate(network);
            loan.AssessCompliance(loan.EffectiveAt.Value.AddDays(8));

            commission.HoldPayout("Onyx loan repayment is overdue.");
            commission.HoldPayout("Onyx loan repayment is overdue.");

            var repayment = MemberPayment.CreatePending(
                1,
                loan.CustomerId,
                MemberPaymentPurpose.OnyxLoanRepayment,
                200m,
                "Yoco",
                "loan-catch-up-1",
                loan.EffectiveAt.Value.AddDays(9));
            repayment.Confirm(loan.EffectiveAt.Value.AddDays(9).AddMinutes(1));
            loan.ApplyConfirmedRepayment(repayment, weeklyRequirementNumber: 1);

            Assert.False(loan.RequiresPayoutHold);

            var releasedAt = EffectiveFrom.AddDays(14);
            commission.ReleaseHeldPayoutAfterComplianceRestored(
                releasedAt,
                "The overdue Onyx loan instalment was paid.");
            commission.ReleaseHeldPayoutAfterComplianceRestored(
                releasedAt,
                "The overdue Onyx loan instalment was paid.");

            Assert.Equal(WeeklyCommissionPayoutStatus.Released, commission.PayoutStatus);
            Assert.Equal(400m, commission.TotalAmount);
            Assert.Equal(2, commission.Components.Count);
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

        [Fact]
        public void EligibleAtCutoff_BecomingOverdueAfterCutoff_KeepsHistoricalCycleEarned()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(6),
                "due-policy-v1");
            var commission = Calculate(network, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
            Assert.Null(commission.HoldReason);

            obligation.AssessStatus(EffectiveFrom.AddDays(14));
            Assert.Equal(EntryMonthlyObligationStatus.Overdue, obligation.Status);

            var replayed = Calculate(network, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, replayed.PayoutStatus);
            Assert.Null(replayed.HoldReason);
            Assert.Equal(commission.TotalAmount, replayed.TotalAmount);
        }

        [Fact]
        public void OverdueAtCutoff_PaidAfterCutoff_HistoricalCycleRemainsHeld()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
            var cutoff = CreatePeriod(EffectiveFrom.AddDays(5)).PeriodEnd;
            var commission = Calculate(network, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, commission.PayoutStatus);

            var payment = CreateConfirmedMonthlyPayment(
                root.CustomerId,
                "cutoff-hold-after",
                EffectiveFrom.AddDays(13));
            obligation.ApplyConfirmedPayment(payment);
            Assert.Equal(EntryMonthlyObligationStatus.Paid, obligation.Status);

            var replayed = Calculate(network, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, replayed.PayoutStatus);
            Assert.Equal("AQGreen monthly commitment is overdue.", replayed.HoldReason);
            Assert.True(payment.ConfirmedAt > cutoff);
        }

        [Fact]
        public void ObligationPaidBeforeCutoff_HistoricalCycleEligible()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
            var payment = CreateConfirmedMonthlyPayment(
                root.CustomerId,
                "paid-before-cutoff",
                EffectiveFrom.AddDays(7));
            obligation.ApplyConfirmedPayment(payment);

            var commission = Calculate(network, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
            Assert.Null(commission.HoldReason);
        }

        [Fact]
        public void ObligationPaidExactlyAtCutoff_HistoricalCycleEligible_InclusiveBoundary()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
            var period = CreatePeriod(EffectiveFrom.AddDays(5));
            var payment = CreateConfirmedMonthlyPayment(
                root.CustomerId,
                "paid-at-cutoff",
                period.PeriodEnd);
            obligation.ApplyConfirmedPayment(payment);

            var commission = CalculateForPeriod(network, period, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
        }

        [Fact]
        public void GracePeriodEndingExactlyAtCutoff_IsNotOverdue_ForThatCycle()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var period = CreatePeriod(EffectiveFrom.AddDays(5));
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                period.PeriodEnd.AddDays(-7),
                "due-policy-v1");
            Assert.Equal(period.PeriodEnd, obligation.GracePeriodEndsAt);

            var commission = CalculateForPeriod(network, period, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
        }

        [Fact]
        public void GracePeriodEndingBeforeCutoff_IsOverdue_ForThatCycle()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var period = CreatePeriod(EffectiveFrom.AddDays(5));
            var obligation = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                period.PeriodEnd.AddDays(-8),
                "due-policy-v1");

            var commission = CalculateForPeriod(network, period, new[] { obligation });
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, commission.PayoutStatus);
            Assert.Equal("AQGreen monthly commitment is overdue.", commission.HoldReason);
        }

        [Fact]
        public void ObligationStanding_IsIndependentOfAssessmentState_NoCurrentStateFallback()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Single(participation => participation.CustomerId == 1);
            var unassessed = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
            var assessed = EntryMonthlyObligation.Create(
                root,
                2026,
                7,
                EffectiveFrom.AddDays(1),
                "due-policy-v1");
            assessed.AssessStatus(EffectiveFrom.AddDays(14));

            var fromUnassessed = Calculate(network, new[] { unassessed });
            var fromAssessed = Calculate(network, new[] { assessed });
            Assert.Equal(EntryMonthlyObligationStatus.Due, unassessed.Status);
            Assert.Equal(EntryMonthlyObligationStatus.Overdue, assessed.Status);
            Assert.Equal(fromAssessed.PayoutStatus, fromUnassessed.PayoutStatus);
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, fromUnassessed.PayoutStatus);
            Assert.Equal(fromAssessed.HoldReason, fromUnassessed.HoldReason);
        }

        [Fact]
        public void LoanNotEffectiveAtCutoff_HoldsNoHistoricalCommission()
        {
            var network = BuildNetwork(maxDepth: 2);
            var loan = CreateLoanApprovedAt(network, EffectiveFrom.AddDays(13));
            loan.AssessCompliance(EffectiveFrom.AddDays(21));
            Assert.True(loan.RequiresPayoutHold);

            var commission = Calculate(network, loans: new[] { loan });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
            Assert.Null(commission.HoldReason);
        }

        [Fact]
        public void LoanRequirementOverdueAtCutoff_RepaidAfterCutoff_HistoricalCycleRemainsHeld()
        {
            var network = BuildNetwork(maxDepth: 2);
            var root = network.Single(participation => participation.CustomerId == 1);
            var loan = CreateActiveLoan(network);
            var cutoff = CreatePeriod(EffectiveFrom.AddDays(5)).PeriodEnd;
            var commission = Calculate(network, loans: new[] { loan });
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, commission.PayoutStatus);

            var repayment = CreateConfirmedLoanRepayment(
                root.CustomerId,
                "loan-repaid-after-cutoff",
                EffectiveFrom.AddDays(13));
            loan.ApplyConfirmedRepayment(repayment, weeklyRequirementNumber: 1);
            Assert.False(loan.RequiresPayoutHold);

            var replayed = Calculate(network, loans: new[] { loan });
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, replayed.PayoutStatus);
            Assert.Equal("Onyx loan repayment is overdue.", replayed.HoldReason);
            Assert.True(repayment.ConfirmedAt > cutoff);
        }

        [Fact]
        public void LoanRequirementRepaidBeforeCutoff_HistoricalCycleEligible()
        {
            var network = BuildNetwork(maxDepth: 2);
            var root = network.Single(participation => participation.CustomerId == 1);
            var loan = CreateActiveLoan(network);
            var repayment = CreateConfirmedLoanRepayment(
                root.CustomerId,
                "loan-repaid-before-cutoff",
                EffectiveFrom.AddDays(6));
            loan.ApplyConfirmedRepayment(repayment, weeklyRequirementNumber: 1);

            var commission = Calculate(network, loans: new[] { loan });
            Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
            Assert.Null(commission.HoldReason);
        }

        [Fact]
        public void LoanAgreementOverdueAtCutoff_HoldsPayout_WhenRequirementsSatisfiedButDeadlinePassed()
        {
            var network = BuildNetwork(maxDepth: 2);
            var root = network.Single(participation => participation.CustomerId == 1);
            var loan = CreateActiveLoan(network);
            for (var number = 1; number <= loan.WeeklyRequirements.Count; number++)
            {
                var repayment = CreateConfirmedLoanRepayment(
                    root.CustomerId,
                    $"loan-full-repayment-{number}",
                    EffectiveFrom.AddDays(20 + number));
                loan.ApplyConfirmedRepayment(repayment, weeklyRequirementNumber: number);
            }
            Assert.False(loan.RequiresPayoutHold);

            var period = CreatePeriod(EffectiveFrom.AddDays(95));
            var commission = CalculateForPeriod(network, period, loans: new[] { loan });
            Assert.Equal(WeeklyCommissionPayoutStatus.Held, commission.PayoutStatus);
            Assert.Equal("Onyx loan repayment is overdue.", commission.HoldReason);
        }

        private static MemberPayment CreateConfirmedMonthlyPayment(
            int customerId,
            string externalReference,
            DateTime confirmedAt)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.EntryMonthlyCommitment,
                600m,
                "Yoco",
                externalReference,
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            return payment;
        }

        private static MemberPayment CreateConfirmedLoanRepayment(
            int customerId,
            string externalReference,
            DateTime confirmedAt)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.OnyxLoanRepayment,
                200m,
                "Yoco",
                externalReference,
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            return payment;
        }

        private static EntryWeeklyCommission Calculate(
            IReadOnlyCollection<EntryParticipation> network,
            IEnumerable<EntryMonthlyObligation> obligations = null,
            IEnumerable<OnyxLoanAgreement> loans = null)
        {
            var period = CreatePeriod(EffectiveFrom.AddDays(5));
            return CalculateForPeriod(network, period, obligations, loans);
        }

        private static EntryCommissionPeriod CreatePeriod(DateTime periodStart)
        {
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            return EntryCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                CommissionTerms);
        }

        private static EntryWeeklyCommission CalculateForPeriod(
            IReadOnlyCollection<EntryParticipation> network,
            EntryCommissionPeriod period,
            IEnumerable<EntryMonthlyObligation> obligations = null,
            IEnumerable<OnyxLoanAgreement> loans = null)
        {
            var calculator = new EntryWeeklyCommissionCalculator(
                new EntryNetworkQualificationEvaluator());

            return calculator.Calculate(
                network.Single(participation => participation.CustomerId == 1),
                period,
                CommissionTerms,
                network,
                obligations ?? Array.Empty<EntryMonthlyObligation>(),
                loans ?? Array.Empty<OnyxLoanAgreement>());
        }

        private static OnyxLoanAgreement CreateActiveLoan(
            IReadOnlyCollection<EntryParticipation> network)
        {
            var loan = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                network.Single(participation => participation.CustomerId == 1),
                network,
                new EntryNetworkQualificationEvaluator(),
                LoanTerms,
                EffectiveFrom);
            loan.AcceptByMember(
                20,
                "I accept the Onyx loan terms.",
                EffectiveFrom.AddHours(1));
            loan.ApproveByAdministrator(99, EffectiveFrom.AddHours(2));
            return loan;
        }

        private static OnyxLoanAgreement CreateLoanApprovedAt(
            IReadOnlyCollection<EntryParticipation> network,
            DateTime approvedAt)
        {
            var loan = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                network.Single(participation => participation.CustomerId == 1),
                network,
                new EntryNetworkQualificationEvaluator(),
                LoanTerms,
                EffectiveFrom);
            loan.AcceptByMember(
                20,
                "I accept the Onyx loan terms.",
                approvedAt.AddDays(-1));
            loan.ApproveByAdministrator(99, approvedAt);
            return loan;
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
            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
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
