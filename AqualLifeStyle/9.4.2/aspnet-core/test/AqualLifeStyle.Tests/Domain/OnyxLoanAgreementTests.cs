using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxLoanAgreementTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly DateTime ApprovedAt =
            new(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms EntryTerms =
            EntryProgrammeTerms.Create(
                "2026-07",
                EffectiveFrom,
                600m,
                600m,
                600m,
                7);

        private static readonly OnyxLoanTerms LoanTerms =
            OnyxLoanTerms.Create(
                "2026-07",
                EffectiveFrom,
                principalAmount: 6120m,
                interestRatePercent: 30m,
                repaymentPeriodMonths: 3,
                initialWeeklyRequirementCount: 4,
                initialWeeklyMinimumAmount: 200m);

        [Fact]
        public void LoanTerms_CalculateConfirmedSevenThousandNineHundredFiftySixRandTotal()
        {
            Assert.Equal(6120m, LoanTerms.PrincipalAmount);
            Assert.Equal(30m, LoanTerms.InterestRatePercent);
            Assert.Equal(7956m, LoanTerms.TotalPayableAmount);
            Assert.Equal(3, LoanTerms.RepaymentPeriodMonths);
        }

        [Fact]
        public void LoanOffer_RequiresCompletedEntryLevelTwo()
        {
            var levelOneNetwork = BuildNetwork(maxDepth: 1);
            var root = levelOneNetwork.Single(participation => participation.CustomerId == 1);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                    root,
                    levelOneNetwork,
                    new EntryNetworkQualificationEvaluator(),
                    LoanTerms,
                    EffectiveFrom.AddDays(1)));

            var levelTwoNetwork = BuildNetwork(maxDepth: 2);
            var eligibleRoot = levelTwoNetwork.Single(
                participation => participation.CustomerId == 1);
            var agreement = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                eligibleRoot,
                levelTwoNetwork,
                new EntryNetworkQualificationEvaluator(),
                LoanTerms,
                EffectiveFrom.AddDays(1));

            Assert.Equal(
                OnyxLoanAgreementStatus.AwaitingMemberAcceptance,
                agreement.Status);
            Assert.Equal(eligibleRoot.Id, agreement.EntryParticipationId);
        }

        [Fact]
        public void AdministratorApproval_RequiresMemberAcceptanceAndStartsLoanPeriod()
        {
            var agreement = CreateOfferedAgreement();

            Assert.Throws<InvalidOperationException>(
                () => agreement.ApproveByAdministrator(99, ApprovedAt));

            agreement.AcceptByMember(
                memberUserId: 20,
                confirmation: "I accept the Onyx loan terms.",
                acceptedAt: ApprovedAt.AddMinutes(-5));
            agreement.ApproveByAdministrator(99, ApprovedAt);

            Assert.Equal(OnyxLoanAgreementStatus.Active, agreement.Status);
            Assert.Equal(ApprovedAt, agreement.EffectiveAt);
            Assert.Equal(ApprovedAt.AddMonths(3), agreement.RepaymentDeadlineAt);
            Assert.Collection(
                agreement.WeeklyRequirements.OrderBy(item => item.RequirementNumber),
                requirement => Assert.Equal(ApprovedAt.AddDays(7), requirement.DueAt),
                requirement => Assert.Equal(ApprovedAt.AddDays(14), requirement.DueAt),
                requirement => Assert.Equal(ApprovedAt.AddDays(21), requirement.DueAt),
                requirement => Assert.Equal(ApprovedAt.AddDays(28), requirement.DueAt));
        }

        [Fact]
        public void MissedWeeklyMinimum_HoldsPayoutUntilThatRequirementIsCaughtUp()
        {
            var agreement = CreateActiveAgreement();
            agreement.AssessCompliance(ApprovedAt.AddDays(8));

            var firstRequirement = agreement.WeeklyRequirements.Single(
                requirement => requirement.RequirementNumber == 1);
            Assert.Equal(
                OnyxLoanWeeklyRequirementStatus.Overdue,
                firstRequirement.Status);
            Assert.True(agreement.RequiresPayoutHold);

            var catchUpPayment = CreateConfirmedRepayment(
                200m,
                ApprovedAt.AddDays(9));
            agreement.ApplyConfirmedRepayment(catchUpPayment, weeklyRequirementNumber: 1);

            Assert.Equal(
                OnyxLoanWeeklyRequirementStatus.Satisfied,
                firstRequirement.Status);
            Assert.Equal(200m, firstRequirement.CreditedAmount);
            Assert.False(agreement.RequiresPayoutHold);
            Assert.Equal(7756m, agreement.OutstandingAmount);
        }

        [Fact]
        public void OneLateEightHundredRandPayment_DoesNotSatisfyFourWeeklyRequirements()
        {
            var agreement = CreateActiveAgreement();
            agreement.AssessCompliance(ApprovedAt.AddDays(29));

            var payment = CreateConfirmedRepayment(800m, ApprovedAt.AddDays(30));
            agreement.ApplyConfirmedRepayment(payment, weeklyRequirementNumber: 4);

            Assert.Equal(
                OnyxLoanWeeklyRequirementStatus.Satisfied,
                agreement.WeeklyRequirements.Single(item => item.RequirementNumber == 4).Status);
            Assert.All(
                agreement.WeeklyRequirements.Where(item => item.RequirementNumber < 4),
                requirement => Assert.Equal(
                    OnyxLoanWeeklyRequirementStatus.Overdue,
                    requirement.Status));
            Assert.True(agreement.RequiresPayoutHold);
        }

        [Fact]
        public void AdditionalRepayment_ReducesBalanceAndDuplicateConfirmationIsIdempotent()
        {
            var agreement = CreateActiveAgreement();
            var payment = CreateConfirmedRepayment(500m, ApprovedAt.AddDays(2));

            agreement.ApplyConfirmedRepayment(payment);
            agreement.ApplyConfirmedRepayment(payment);

            Assert.Equal(7456m, agreement.OutstandingAmount);
            var allocation = Assert.Single(agreement.Repayments);
            Assert.Null(allocation.WeeklyRequirementNumber);
            Assert.All(
                agreement.WeeklyRequirements,
                requirement => Assert.Equal(0m, requirement.CreditedAmount));
        }

        [Fact]
        public void UnsettledBalanceAfterDeadline_HoldsPayoutUntilFullySettled()
        {
            var agreement = CreateActiveAgreement();
            agreement.AssessCompliance(agreement.RepaymentDeadlineAt.Value.AddTicks(1));

            Assert.Equal(OnyxLoanAgreementStatus.Overdue, agreement.Status);
            Assert.True(agreement.RequiresPayoutHold);

            var settlement = CreateConfirmedRepayment(
                agreement.OutstandingAmount,
                agreement.RepaymentDeadlineAt.Value.AddDays(1));
            agreement.ApplyConfirmedRepayment(settlement);

            Assert.Equal(OnyxLoanAgreementStatus.Settled, agreement.Status);
            Assert.Equal(0m, agreement.OutstandingAmount);
            Assert.False(agreement.RequiresPayoutHold);
            Assert.Equal(settlement.ConfirmedAt, agreement.SettledAt);
        }

        private static OnyxLoanAgreement CreateOfferedAgreement()
        {
            var network = BuildNetwork(maxDepth: 2);
            return OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                network.Single(participation => participation.CustomerId == 1),
                network,
                new EntryNetworkQualificationEvaluator(),
                LoanTerms,
                EffectiveFrom.AddDays(1));
        }

        private static OnyxLoanAgreement CreateActiveAgreement()
        {
            var agreement = CreateOfferedAgreement();
            agreement.AcceptByMember(
                20,
                "I accept the Onyx loan terms.",
                ApprovedAt.AddMinutes(-5));
            agreement.ApproveByAdministrator(99, ApprovedAt);
            return agreement;
        }

        private static List<EntryParticipation> BuildNetwork(int maxDepth)
        {
            var root = CreateQualifiedIndependentParticipation(1);
            var participations = new List<EntryParticipation> { root };
            var currentLevel = new List<EntryParticipation> { root };
            var nextCustomerId = 2;

            for (var depth = 1; depth <= maxDepth; depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 0;
                         index < EntryNetworkQualificationEvaluator.BranchSize;
                         index++)
                    {
                        var recruit = EntryParticipation.StartUnderRecruiter(
                            1,
                            nextCustomerId++,
                            recruiter,
                            EntryTerms,
                            EffectiveFrom);
                        Activate(recruit);
                        participations.Add(recruit);
                        nextLevel.Add(recruit);
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
                EntryTerms,
                EffectiveFrom);
            Activate(participation);
            return participation;
        }

        private static void Activate(EntryParticipation participation)
        {
            var registration = CreateConfirmedPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = CreateConfirmedPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                600m,
                EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activation);
        }

        private static MemberPayment CreateConfirmedRepayment(
            decimal amount,
            DateTime confirmedAt)
        {
            return CreateConfirmedPayment(
                customerId: 1,
                MemberPaymentPurpose.OnyxLoanRepayment,
                amount,
                confirmedAt);
        }

        private static MemberPayment CreateConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            DateTime confirmedAt)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Yoco",
                $"{purpose}-{Guid.NewGuid():N}",
                EffectiveFrom);
            payment.Confirm(confirmedAt);
            return payment;
        }
    }
}
