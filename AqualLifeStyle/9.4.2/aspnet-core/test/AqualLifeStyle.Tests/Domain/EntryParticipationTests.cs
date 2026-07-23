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

        [Fact]
        public void Start_RequiresARecruiter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EntryParticipation.Start(1, customerId: 10, recruiterCustomerId: 0, Terms, EffectiveFrom));
            Assert.Throws<InvalidOperationException>(() =>
                EntryParticipation.Start(1, customerId: 10, recruiterCustomerId: 10, Terms, EffectiveFrom));
        }

        [Fact]
        public void EntryParticipant_DoesNotQualifyBeforeBothPaymentsAreConfirmed()
        {
            var participation = StartParticipation();
            var registrationPayment = CreatePayment(
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
                MemberPaymentPurpose.EntryActivation,
                "entry-activation-10");

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedActivationPayment(activationPayment));

            activationPayment.Confirm(EffectiveFrom.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedActivationPayment(activationPayment));
        }

        [Fact]
        public void ReapplyingTheSameConfirmedPayment_IsIdempotent()
        {
            var participation = StartParticipation();
            var payment = CreatePayment(
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

            participation.CorrectRecruiter(
                newRecruiterCustomerId: 30,
                administratorUserId: 99,
                reason: "Corrected a registration capture error.",
                correctedAt: EffectiveFrom.AddDays(1));

            var correction = Assert.Single(participation.RecruiterCorrections);
            Assert.Equal(20, correction.PreviousRecruiterCustomerId);
            Assert.Equal(30, correction.NewRecruiterCustomerId);
            Assert.Equal(99, correction.AdministratorUserId);
            Assert.Equal("Corrected a registration capture error.", correction.Reason);
            Assert.Equal(30, participation.RecruiterCustomerId);
            Assert.True(participation.IsQualifiedForNetwork);
        }

        private static EntryParticipation StartParticipation()
        {
            return EntryParticipation.Start(
                tenantId: 1,
                customerId: 10,
                recruiterCustomerId: 20,
                Terms,
                EffectiveFrom);
        }

        private static void ApplyBothActivationPayments(EntryParticipation participation)
        {
            var registrationPayment = CreatePayment(
                MemberPaymentPurpose.EntryRegistration,
                $"registration-{participation.CustomerId}");
            registrationPayment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registrationPayment);

            var activationPayment = CreatePayment(
                MemberPaymentPurpose.EntryActivation,
                $"activation-{participation.CustomerId}");
            activationPayment.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activationPayment);
        }

        private static MemberPayment CreatePayment(
            MemberPaymentPurpose purpose,
            string externalReference)
        {
            return MemberPayment.CreatePending(
                tenantId: 1,
                customerId: 10,
                purpose,
                amount: 600m,
                provider: "Yoco",
                externalReference,
                initiatedAt: EffectiveFrom);
        }
    }
}
