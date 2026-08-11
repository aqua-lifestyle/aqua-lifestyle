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
        private static readonly EntryProgrammeTerms FlexiblePaymentTerms =
            EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                "aqgreen-2026-08-flexible-1200",
                EffectiveFrom,
                joiningPaymentAmount: 1200m,
                joiningInstallmentAmount: 600m,
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
                tenantId: 1,
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
            Assert.Throws<InvalidOperationException>(() =>
                EntryParticipation.StartUnderRecruiter(
                    tenantId: 1,
                    customerId: 10,
                    inactiveRecruiter,
                    Terms,
                    EffectiveFrom));

            inactiveRecruiter.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
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
        public void ActiveRecruiter_FromAnotherTenant_IsRejected()
        {
            var recruiter = EntryParticipation.StartIndependently(
                tenantId: 2,
                customerId: 20,
                Terms,
                EffectiveFrom);
            ApplyBothActivationPayments(recruiter);
            recruiter.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                EntryParticipation.StartUnderRecruiter(
                    tenantId: 1,
                    customerId: 10,
                    recruiter,
                    Terms,
                    EffectiveFrom));

            Assert.Contains("same Tenant", exception.Message);
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
        public void EntryParticipant_ActivatesAfterTwoConfirmedPaymentsAndAdministrativeApproval()
        {
            var participation = StartParticipation();
            ApplyBothActivationPayments(participation);

            Assert.Equal(
                EntryParticipationStatus.PaymentConfirmedAwaitingApproval,
                participation.Status);
            Assert.True(participation.IsAwaitingAdministrativeApproval);
            Assert.False(participation.IsQualifiedForNetwork);
            Assert.Null(participation.ActivatedAt);
            Assert.NotNull(participation.RegistrationPaymentId);
            Assert.NotNull(participation.ActivationPaymentId);

            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));

            Assert.Equal(EntryParticipationStatus.Active, participation.Status);
            Assert.True(participation.IsQualifiedForNetwork);
            Assert.NotNull(participation.ActivatedAt);
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
        public void AQGreenParticipant_ActivatesAfterOneConfirmedPaymentAndAdministrativeApproval()
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

            Assert.Equal(
                EntryParticipationStatus.PaymentConfirmedAwaitingApproval,
                participation.Status);
            Assert.True(participation.IsAwaitingAdministrativeApproval);
            Assert.False(participation.IsQualifiedForNetwork);
            Assert.Equal(payment.Id, participation.JoiningPaymentId);
            Assert.Null(participation.RegistrationPaymentId);
            Assert.Null(participation.ActivationPaymentId);
            Assert.Equal(1200m, participation.JoiningPaymentAmount);

            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));

            Assert.Equal(EntryParticipationStatus.Active, participation.Status);
            Assert.True(participation.IsQualifiedForNetwork);
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
        public void AQGreenSinglePayment_AllowsTheFullPaymentSchedule()
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                SinglePaymentTerms,
                EffectiveFrom);

            participation.SelectJoiningPaymentSchedule(
                AQGreenJoiningPaymentSchedule.Full);

            Assert.Equal(
                AQGreenJoiningPaymentSchedule.Full,
                participation.JoiningPaymentSchedule);
            Assert.Equal(1200m, participation.GetNextJoiningPaymentAmount());
            Assert.Equal(
                AQGreenJoiningPaymentStage.Full,
                participation.GetNextJoiningPaymentStage());
        }

        [Fact]
        public void AQGreenSinglePayment_RejectsTheInstallmentSchedule()
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                SinglePaymentTerms,
                EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.TwoInstallments));
            Assert.Null(participation.JoiningPaymentSchedule);
        }

        [Fact]
        public void FlexibleAQGreen_ActivatesAfterOneVerifiedFullPaymentAndApproval()
        {
            var participation = StartFlexibleParticipation();
            participation.SelectJoiningPaymentSchedule(AQGreenJoiningPaymentSchedule.Full);
            var payment = CreateAQGreenJoiningPayment(1200m, "aqgreen-full");
            payment.Confirm(EffectiveFrom.AddMinutes(1));

            participation.ApplyConfirmedJoiningPayment(
                payment,
                AQGreenJoiningPaymentStage.Full);
            participation.ApplyConfirmedJoiningPayment(
                payment,
                AQGreenJoiningPaymentStage.Full);

            Assert.Equal(
                EntryParticipationStatus.PaymentConfirmedAwaitingApproval,
                participation.Status);
            Assert.Equal(1200m, participation.GetConfirmedJoiningAmount());
            Assert.Equal(0m, participation.GetOutstandingJoiningAmount());
            Assert.Equal(payment.Id, participation.JoiningPaymentId);

            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));

            Assert.Equal(EntryParticipationStatus.Active, participation.Status);
        }

        [Fact]
        public void FlexibleAQGreen_RequiresTwoDistinctVerifiedInstalments()
        {
            var participation = StartFlexibleParticipation();
            participation.SelectJoiningPaymentSchedule(
                AQGreenJoiningPaymentSchedule.TwoInstallments);
            var first = CreateAQGreenJoiningPayment(600m, "aqgreen-first");
            first.Confirm(EffectiveFrom.AddMinutes(1));

            participation.ApplyConfirmedJoiningPayment(
                first,
                AQGreenJoiningPaymentStage.FirstInstallment);

            Assert.Equal(
                EntryParticipationStatus.AwaitingActivationPayment,
                participation.Status);
            Assert.False(participation.IsQualifiedForNetwork);
            Assert.Equal(600m, participation.GetConfirmedJoiningAmount());
            Assert.Throws<InvalidOperationException>(() =>
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.Full));

            var second = CreateAQGreenJoiningPayment(600m, "aqgreen-second");
            second.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedJoiningPayment(
                second,
                AQGreenJoiningPaymentStage.SecondInstallment);

            Assert.Equal(
                EntryParticipationStatus.PaymentConfirmedAwaitingApproval,
                participation.Status);
            Assert.Equal(1200m, participation.GetConfirmedJoiningAmount());
            Assert.NotEqual(
                participation.RegistrationPaymentId,
                participation.ActivationPaymentId);

            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));

            Assert.Equal(EntryParticipationStatus.Active, participation.Status);
        }

        [Fact]
        public void FlexibleAQGreen_DoesNotAcceptMonthlyPaymentForJoining()
        {
            var participation = StartFlexibleParticipation();
            participation.SelectJoiningPaymentSchedule(AQGreenJoiningPaymentSchedule.Full);
            var monthly = CreatePayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryMonthlyCommitment,
                "monthly-not-joining");
            monthly.Confirm(EffectiveFrom.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedJoiningPayment(
                    monthly,
                    AQGreenJoiningPaymentStage.Full));
            Assert.Equal(0m, participation.GetConfirmedJoiningAmount());
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
                tenantId: 1,
                customerId: 30,
                Terms,
                EffectiveFrom);
            ApplyBothActivationPayments(recruiter);
            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
            recruiter.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));

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

        [Fact]
        public void AdministrativeRecruiterCorrection_FromAnotherTenant_IsRejected()
        {
            var participation = StartParticipation();
            ApplyBothActivationPayments(participation);
            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
            var recruiter = EntryParticipation.StartIndependently(
                tenantId: 2,
                customerId: 30,
                Terms,
                EffectiveFrom);
            ApplyBothActivationPayments(recruiter);
            recruiter.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                participation.CorrectRecruiter(
                    recruiter,
                    administratorUserId: 99,
                    reason: "Attempted cross-Tenant correction.",
                    correctedAt: EffectiveFrom.AddDays(1)));

            Assert.Contains("same Tenant", exception.Message);
            Assert.Null(participation.RecruiterCustomerId);
            Assert.Empty(participation.RecruiterCorrections);
        }

        private static EntryParticipation StartParticipation()
        {
            return EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                Terms,
                EffectiveFrom);
        }

        private static EntryParticipation StartFlexibleParticipation() =>
            EntryParticipation.StartIndependently(
                tenantId: 1,
                customerId: 10,
                FlexiblePaymentTerms,
                EffectiveFrom);

        private static MemberPayment CreateAQGreenJoiningPayment(
            decimal amount,
            string externalReference) =>
            MemberPayment.CreatePending(
                tenantId: 1,
                customerId: 10,
                MemberPaymentPurpose.AQGreenJoining,
                amount,
                "Yoco",
                externalReference,
                EffectiveFrom);

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
