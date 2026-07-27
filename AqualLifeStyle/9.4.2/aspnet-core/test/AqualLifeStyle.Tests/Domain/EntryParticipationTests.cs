using System;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EntryParticipationTests
    {
        private static readonly DateTime EffectiveFrom = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly EntryProgrammeTerms Terms = EntryProgrammeTerms.Create(
            "entry-2026-07",
            EffectiveFrom,
            registrationPaymentAmount: 600m,
            activationPaymentAmount: 600m,
            monthlyCommitmentAmount: 600m,
            gracePeriodDays: 7);
        private static readonly EntryProgrammeTerms SinglePaymentTerms =
            EntryProgrammeTerms.CreateSingleJoiningPayment(
                "aqgreen-2026-07-single-1200",
                EffectiveFrom,
                joiningPaymentAmount: 1200m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

        [Fact]
        public void Customer_CanStartEntryIndependently()
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                Terms,
                EffectiveFrom);

            Assert.True(participation.JoinedIndependently);
            Assert.Null(participation.RecruiterCustomerId);
        }

        [Fact]
        public void RecordedRecruiter_MustHaveActiveEntryParticipation()
        {
            var inactiveRecruiter = EntryParticipation.StartIndependently(
                tenantId: 2,
                customerId: 20,
                Terms,
                EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                EntryParticipation.StartUnderRecruiter(
                    tenantId: 1,
                    customerId: 10,
                    inactiveRecruiter,
                    Terms,
                    EffectiveFrom));

            ApplyBothActivationPayments(inactiveRecruiter);
            var participation = EntryParticipation.StartUnderRecruiter(
                tenantId: 1,
                customerId: 10,
                inactiveRecruiter,
                Terms,
                EffectiveFrom);

            Assert.False(participation.JoinedIndependently);
            Assert.Equal(20, participation.RecruiterCustomerId);
        }

        [Fact]
        public void EntryParticipant_DoesNotQualifyBeforeBothPaymentsAreConfirmed()
        {
            var participation = StartParticipation();
            var registrationPayment = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                "entry-registration-10");
            registrationPayment.Confirm(EffectiveFrom.AddMinutes(1));

            participation.ApplyConfirmedActivationPayment(registrationPayment);

            Assert.Equal(EntryParticipationStatus.AwaitingActivationPayment, participation.Status);
            Assert.False(participation.IsQualifiedForNetwork);
            Assert.Null(participation.ActivatedAt);
        }

        [Fact]
        public void EntryParticipant_QualifiesAfterTwoConfirmedSixHundredRandPayments()
        {
            var participation = StartParticipation();
            ApplyBothActivationPayments(participation);

            Assert.Equal(EntryParticipationStatus.Active, participation.Status);
            Assert.True(participation.IsQualifiedForNetwork);
            Assert.NotNull(participation.RegistrationPaymentId);
            Assert.NotNull(participation.ActivationPaymentId);
            Assert.Equal(600m, participation.MonthlyCommitmentAmount);
            Assert.Equal(7, participation.GracePeriodDays);
            Assert.Equal("entry-2026-07", participation.TermsVersion);
        }

        [Fact]
        public void EntryActivation_RejectsUnconfirmedOrOutOfOrderPayments()
        {
            var participation = StartParticipation();
            var activationPayment = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                "entry-activation-10");

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedActivationPayment(activationPayment));

            activationPayment.Confirm(EffectiveFrom.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedActivationPayment(activationPayment));
        }

        [Fact]
        public void AQGreenParticipant_ActivatesAfterOneConfirmedTwelveHundredRandPayment()
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                SinglePaymentTerms,
                EffectiveFrom);
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.AQGreenJoining,
                amount: 1200m,
                provider: "Yoco",
                externalReference: "aqgreen-joining-10",
                initiatedAt: EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));

            participation.ApplyConfirmedJoiningPayment(payment);
            participation.ApplyConfirmedJoiningPayment(payment);

            Assert.Equal(EntryParticipationStatus.Active, participation.Status);
            Assert.True(participation.IsQualifiedForNetwork);
            Assert.Equal(payment.Id, participation.JoiningPaymentId);
            Assert.Null(participation.RegistrationPaymentId);
            Assert.Null(participation.ActivationPaymentId);
            Assert.Equal(1200m, participation.JoiningPaymentAmount);
        }

        [Fact]
        public void AQGreenSinglePayment_RejectsAConfirmedSixHundredRandPayment()
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                SinglePaymentTerms,
                EffectiveFrom);
            var payment = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.AQGreenJoining,
                "aqgreen-underpayment-10");
            payment.Confirm(EffectiveFrom.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedJoiningPayment(payment));
            Assert.Equal(
                EntryParticipationStatus.AwaitingJoiningPayment,
                participation.Status);
            Assert.Null(participation.JoiningPaymentId);
        }

        [Fact]
        public void ReapplyingTheSameConfirmedPayment_IsIdempotent()
        {
            var participation = StartParticipation();
            var payment = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                "entry-registration-idempotent");
            payment.Confirm(EffectiveFrom.AddMinutes(1));

            participation.ApplyConfirmedActivationPayment(payment);
            participation.ApplyConfirmedActivationPayment(payment);

            Assert.Equal(payment.Id, participation.RegistrationPaymentId);
            Assert.Equal(EntryParticipationStatus.AwaitingActivationPayment, participation.Status);
        }

        [Fact]
        public void AdministrativeRecruiterCorrection_PreservesAnAuditRecord()
        {
            var participation = StartParticipation();
            ApplyBothActivationPayments(participation);

            var recruiter = EntryParticipation.StartIndependently(
                tenantId: 2,
                customerId: 30,
                Terms,
                EffectiveFrom);
            ApplyBothActivationPayments(recruiter);

            participation.CorrectRecruiter(
                recruiter,
                administratorUserId: 99,
                reason: "Corrected a registration capture error.",
                correctedAt: EffectiveFrom.AddDays(1));

            var correction = Assert.Single(participation.RecruiterCorrections);
            Assert.Null(correction.PreviousRecruiterCustomerId);
            Assert.Equal(30, correction.NewRecruiterCustomerId);
            Assert.Equal(99, correction.AdministratorUserId);
            Assert.Equal("Corrected a registration capture error.", correction.Reason);
            Assert.Equal(30, participation.RecruiterCustomerId);
            Assert.True(participation.IsQualifiedForNetwork);
        }

        private static EntryParticipation StartParticipation()
        {
            return EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                Terms,
                EffectiveFrom);
        }

        private static void ApplyBothActivationPayments(EntryParticipation participation)
        {
            var registrationPayment = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                $"registration-{participation.CustomerId}");
            registrationPayment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registrationPayment);

            var activationPayment = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                $"activation-{participation.CustomerId}");
            activationPayment.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activationPayment);
        }

        private static MemberPayment CreatePayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            string externalReference)
        {
            return MemberPayment.CreatePending(
                tenantId,
                customerId,
                purpose,
                amount: 600m,
                provider: "Yoco",
                externalReference,
                initiatedAt: EffectiveFrom);
        }
    }
}
