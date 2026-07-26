using System;
using System.Collections.Generic;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxParticipationTests
    {
        private static readonly DateTime EffectiveFrom = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly OnyxPlanTerms Terms = OnyxPlanTerms.Create(
            "onyx-2026-07",
            EffectiveFrom,
            directEntryAmount: 6120m);
        private static readonly EntryProgrammeTerms AQGreenTerms =
            EntryProgrammeTerms.Create(
                "aqgreen-2026-07",
                EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
        private static readonly OnyxLoanTerms LoanTerms = OnyxLoanTerms.Create(
            "onyx-loan-2026-07",
            EffectiveFrom,
            principalAmount: 6120m,
            interestRatePercent: 30m,
            repaymentPeriodMonths: 3,
            initialWeeklyRequirementCount: 4,
            initialWeeklyMinimumAmount: 200m);

        [Fact]
        public void StartingDirectOnyxParticipation_DoesNotActivateSelectedMembership()
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.Equal(OnyxAdmissionRoute.DirectPayment, participation.AdmissionRoute);
            Assert.Equal(OnyxParticipationStatus.AwaitingDirectEntryPayment, participation.Status);
            Assert.Null(participation.ActivatedAt);
            Assert.Null(participation.EntryParticipationId);
            Assert.True(participation.JoinedIndependently);
        }

        [Fact]
        public void DirectOnyxParticipation_RequiresAConfirmedSixThousandOneHundredTwentyRandPayment()
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);
            var payment = MemberPayment.CreatePending(
                tenantId: 1,
                customerId: 10,
                MemberPaymentPurpose.OnyxDirectEntry,
                amount: 6120m,
                provider: "Yoco",
                externalReference: "onyx-direct-10",
                initiatedAt: EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedDirectEntryPayment(payment));

            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);

            Assert.Equal(OnyxParticipationStatus.Active, participation.Status);
            Assert.Equal(payment.Id, participation.DirectEntryPaymentId);
            Assert.Equal(payment.ConfirmedAt, participation.ActivatedAt);
            Assert.Equal("onyx-2026-07", participation.TermsVersion);
        }

        [Fact]
        public void DirectOnyxActivation_RejectsAnIncorrectPaymentAmount()
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);
            var payment = MemberPayment.CreatePending(
                tenantId: 1,
                customerId: 10,
                MemberPaymentPurpose.OnyxDirectEntry,
                amount: 600m,
                provider: "Yoco",
                externalReference: "onyx-direct-underpayment-10",
                initiatedAt: EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedDirectEntryPayment(payment));
            Assert.Equal(OnyxParticipationStatus.AwaitingDirectEntryPayment, participation.Status);
        }

        [Fact]
        public void RecordedRecruiter_MustHaveActiveOnyxParticipation()
        {
            var recruiter = OnyxParticipation.StartDirectIndependently(
                tenantId: 2,
                customerId: 20,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxParticipation.StartDirectUnderRecruiter(
                    tenantId: 1,
                    customerId: 10,
                    recruiter,
                    onyxMembershipId: 7,
                    Terms,
                    EffectiveFrom));

            Activate(recruiter, "onyx-recruiter-20");
            var participation = OnyxParticipation.StartDirectUnderRecruiter(
                tenantId: 1,
                customerId: 10,
                recruiter,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.False(participation.JoinedIndependently);
            Assert.Equal(20, participation.RecruiterCustomerId);
        }

        [Fact]
        public void AdministrativeRecruiterCorrection_PreservesOnyxHistoryAndIsIdempotent()
        {
            var originalRecruiter = OnyxParticipation.StartDirectIndependently(
                1, 20, 7, Terms, EffectiveFrom);
            var newRecruiter = OnyxParticipation.StartDirectIndependently(
                1, 30, 7, Terms, EffectiveFrom);
            Activate(originalRecruiter, "onyx-original-recruiter");
            Activate(newRecruiter, "onyx-new-recruiter");
            var participation = OnyxParticipation.StartDirectUnderRecruiter(
                1, 10, originalRecruiter, 7, Terms, EffectiveFrom);
            var correctedAt = EffectiveFrom.AddHours(1);

            participation.CorrectRecruiter(
                newRecruiter,
                999,
                "Verified against the signed joining record",
                correctedAt);
            participation.CorrectRecruiter(
                newRecruiter,
                999,
                "Repeated request",
                correctedAt.AddMinutes(1));

            var correction = Assert.Single(participation.RecruiterCorrections);
            Assert.Equal(20, correction.PreviousRecruiterCustomerId);
            Assert.Equal(30, correction.NewRecruiterCustomerId);
            Assert.Equal(999, correction.AdministratorUserId);
            Assert.Equal("Verified against the signed joining record", correction.Reason);
            Assert.Equal(30, participation.RecruiterCustomerId);
        }

        [Fact]
        public void AQGreenGraduation_StartsAnIndependentActiveOnyxNetwork()
        {
            var aqGreenParticipation = CreateActiveAQGreenParticipation();
            var loanAgreement = CreateActiveLoanAgreement(aqGreenParticipation);
            var graduatedAt = loanAgreement.EffectiveAt.Value.AddMinutes(1);

            var participation = OnyxParticipation.GraduateFromAQGreenIndependently(
                aqGreenParticipation,
                loanAgreement,
                onyxMembershipId: 7,
                Terms,
                graduatedAt);

            Assert.Equal(OnyxAdmissionRoute.EntryGraduation, participation.AdmissionRoute);
            Assert.Equal(OnyxParticipationStatus.Active, participation.Status);
            Assert.True(participation.JoinedIndependently);
            Assert.Null(participation.RecruiterCustomerId);
            Assert.Null(participation.DirectEntryPaymentId);
            Assert.Equal(aqGreenParticipation.Id, participation.EntryParticipationId);
            Assert.Equal(loanAgreement.Id, participation.LoanAgreementId);
            Assert.Equal(graduatedAt, participation.ActivatedAt);
        }

        [Fact]
        public void AQGreenGraduation_RejectsALoanForAnotherParticipation()
        {
            var aqGreenParticipation = CreateActiveAQGreenParticipation(customerId: 10);
            var otherParticipation = CreateActiveAQGreenParticipation(customerId: 20);
            var loanAgreement = CreateActiveLoanAgreement(otherParticipation);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxParticipation.GraduateFromAQGreenIndependently(
                    aqGreenParticipation,
                    loanAgreement,
                    onyxMembershipId: 7,
                    Terms,
                    loanAgreement.EffectiveAt.Value));
        }

        [Fact]
        public void AQGreenGraduation_RejectsALoanThatDoesNotMatchOnyxTerms()
        {
            var aqGreenParticipation = CreateActiveAQGreenParticipation();
            var loanAgreement = CreateActiveLoanAgreement(aqGreenParticipation);
            var differentOnyxTerms = OnyxPlanTerms.Create(
                "onyx-future",
                EffectiveFrom,
                directEntryAmount: 7000m);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxParticipation.GraduateFromAQGreenIndependently(
                    aqGreenParticipation,
                    loanAgreement,
                    onyxMembershipId: 7,
                    differentOnyxTerms,
                    loanAgreement.EffectiveAt.Value));
        }

        private static EntryParticipation CreateActiveAQGreenParticipation(
            int customerId = 10)
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId,
                AQGreenTerms,
                EffectiveFrom);
            ActivateAQGreen(participation);
            return participation;
        }

        private static OnyxLoanAgreement CreateActiveLoanAgreement(
            EntryParticipation participation)
        {
            var qualifiedNetwork = new List<EntryParticipation> { participation };
            var currentLevel = new List<EntryParticipation> { participation };
            for (var depth = 1; depth <= 2; depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 1; index <= 5; index++)
                    {
                        var recruit = CreateActiveAQGreenRecruit(
                            customerId: recruiter.CustomerId * 100 + index,
                            recruiter);
                        qualifiedNetwork.Add(recruit);
                        nextLevel.Add(recruit);
                    }
                }

                currentLevel = nextLevel;
            }

            var agreement = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                participation,
                qualifiedNetwork,
                new EntryNetworkQualificationEvaluator(),
                LoanTerms,
                EffectiveFrom.AddMinutes(3));
            agreement.AcceptByMember(
                memberUserId: participation.CustomerId,
                "I accept the Onyx loan terms.",
                EffectiveFrom.AddMinutes(4));
            agreement.ApproveByAdministrator(
                administratorUserId: 999,
                EffectiveFrom.AddMinutes(5));
            return agreement;
        }

        private static EntryParticipation CreateActiveAQGreenRecruit(
            int customerId,
            EntryParticipation recruiter)
        {
            var participation = EntryParticipation.StartUnderRecruiter(
                tenantId: 1,
                customerId,
                recruiter,
                AQGreenTerms,
                EffectiveFrom);
            ActivateAQGreen(participation);
            return participation;
        }

        private static void ActivateAQGreen(EntryParticipation participation)
        {
            participation.ApplyConfirmedActivationPayment(CreateConfirmedAQGreenPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                $"aqgreen-registration-{participation.CustomerId}",
                EffectiveFrom.AddMinutes(1)));
            participation.ApplyConfirmedActivationPayment(CreateConfirmedAQGreenPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                $"aqgreen-activation-{participation.CustomerId}",
                EffectiveFrom.AddMinutes(2)));
        }

        private static MemberPayment CreateConfirmedAQGreenPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            string reference,
            DateTime confirmedAt)
        {
            var payment = MemberPayment.CreatePending(
                tenantId: 1,
                customerId,
                purpose,
                amount: 600m,
                provider: "Yoco",
                externalReference: reference,
                initiatedAt: EffectiveFrom);
            payment.Confirm(confirmedAt);
            return payment;
        }

        private static void Activate(OnyxParticipation participation, string externalReference)
        {
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "Yoco",
                externalReference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);
        }
    }
}
