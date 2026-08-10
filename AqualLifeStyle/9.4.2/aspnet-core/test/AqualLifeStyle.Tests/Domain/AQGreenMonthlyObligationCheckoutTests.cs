using System;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AQGreenMonthlyObligationCheckoutTests
    {
        [Fact]
        public void CheckoutSnapshotsOneExactObligationAndAllocationOutcome()
        {
            var obligation = CreateObligation();
            var createdAt = new DateTime(
                2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var checkout = AQGreenMonthlyObligationCheckout.Create(
                obligation,
                createdAt);

            checkout.EntryMonthlyObligationId.ShouldBe(obligation.Id);
            checkout.EntryParticipationId.ShouldBe(obligation.EntryParticipationId);
            checkout.PeriodYear.ShouldBe(2026);
            checkout.PeriodMonth.ShouldBe(7);
            checkout.Amount.ShouldBe(600m);
            checkout.Currency.ShouldBe("ZAR");
            checkout.AllocationStatus.ShouldBe(
                AQGreenMonthlyPaymentAllocationStatus.PendingProviderConfirmation);

            checkout.RecordCheckout(
                "checkout_monthly_1",
                "https://payments.example.test/monthly/1",
                createdAt.AddMinutes(1));
            var paymentId = Guid.NewGuid();
            checkout.CompleteAllocation(paymentId, createdAt.AddMinutes(2));
            checkout.CompleteAllocation(paymentId, createdAt.AddMinutes(2));

            checkout.PaymentId.ShouldBe(paymentId);
            checkout.Status.ShouldBe(HostedPaymentCheckoutStatus.Completed);
            checkout.AllocationStatus.ShouldBe(
                AQGreenMonthlyPaymentAllocationStatus.Allocated);
        }

        [Fact]
        public void ReconciliationOutcomeCannotBeRedirectedToAllocation()
        {
            var obligation = CreateObligation();
            var createdAt = new DateTime(
                2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var checkout = AQGreenMonthlyObligationCheckout.Create(
                obligation,
                createdAt);
            checkout.RecordCheckout(
                "checkout_monthly_2",
                "https://payments.example.test/monthly/2",
                createdAt.AddMinutes(1));
            var paymentId = Guid.NewGuid();
            checkout.RequireReconciliation(
                paymentId,
                createdAt.AddMinutes(2),
                "The recorded obligation is unavailable.");

            Should.Throw<InvalidOperationException>(() =>
                checkout.CompleteAllocation(paymentId, createdAt.AddMinutes(2)));
            checkout.AllocationStatus.ShouldBe(
                AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired);
        }

        private static EntryMonthlyObligation CreateObligation()
        {
            var effectiveFrom = new DateTime(
                2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var participation = EntryParticipation.StartIndependently(
                1,
                10,
                EntryProgrammeTerms.Create(
                    "monthly-checkout-test",
                    effectiveFrom,
                    600m,
                    600m,
                    600m,
                    7),
                effectiveFrom);
            Apply(participation, MemberPaymentPurpose.EntryRegistration, "registration");
            Apply(participation, MemberPaymentPurpose.EntryActivation, "activation");
            participation.ApproveByAdministrator(1, effectiveFrom.AddMinutes(3));
            return EntryMonthlyObligation.Create(
                participation,
                2026,
                7,
                new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                "monthly-policy-v1");
        }

        private static void Apply(
            EntryParticipation participation,
            MemberPaymentPurpose purpose,
            string reference)
        {
            var initiatedAt = new DateTime(
                2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var payment = MemberPayment.CreatePending(
                1,
                participation.CustomerId,
                purpose,
                600m,
                "Test",
                reference,
                initiatedAt);
            payment.Confirm(initiatedAt.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(payment);
        }
    }
}
