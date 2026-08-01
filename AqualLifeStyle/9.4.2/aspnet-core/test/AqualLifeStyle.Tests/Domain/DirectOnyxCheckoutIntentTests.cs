using System;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class DirectOnyxCheckoutIntentTests
    {
        [Fact]
        public void Complete_SetsPaymentAndParticipation()
        {
            var intent = DirectOnyxCheckoutIntent.Create(
                tenantId: 1,
                customerId: 10,
                recruiterCustomerId: null,
                inviteCode: null,
                onyxMembershipId: 5,
                OnyxPlanTerms.Create("2026-07", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), 6120m),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            var paymentId = Guid.NewGuid();
            var participationId = Guid.NewGuid();
            var completedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

            intent.Complete(paymentId, participationId, completedAt);

            intent.PaymentId.ShouldBe(paymentId);
            intent.ParticipationId.ShouldBe(participationId);
            intent.Status.ShouldBe(HostedPaymentCheckoutStatus.Completed);
            intent.CompletedAt.ShouldBe(completedAt);
        }

        [Fact]
        public void Complete_RejectsEmptyParticipationId()
        {
            var intent = DirectOnyxCheckoutIntent.Create(
                tenantId: 1,
                customerId: 10,
                recruiterCustomerId: null,
                inviteCode: null,
                onyxMembershipId: 5,
                OnyxPlanTerms.Create("2026-07", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), 6120m),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            Should.Throw<ArgumentException>(() =>
                intent.Complete(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow));
        }

        [Fact]
        public void Complete_IdempotentWhenSameIdempotentCall()
        {
            var intent = DirectOnyxCheckoutIntent.Create(
                tenantId: 1,
                customerId: 10,
                recruiterCustomerId: null,
                inviteCode: null,
                onyxMembershipId: 5,
                OnyxPlanTerms.Create("2026-07", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), 6120m),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            var paymentId = Guid.NewGuid();
            var participationId = Guid.NewGuid();
            var completedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

            intent.Complete(paymentId, participationId, completedAt);
            intent.Complete(paymentId, participationId, completedAt);

            intent.PaymentId.ShouldBe(paymentId);
            intent.ParticipationId.ShouldBe(participationId);
        }

        [Fact]
        public void Complete_RejectsConflictingParticipationIdOnRepeat()
        {
            var intent = DirectOnyxCheckoutIntent.Create(
                tenantId: 1,
                customerId: 10,
                recruiterCustomerId: null,
                inviteCode: null,
                onyxMembershipId: 5,
                OnyxPlanTerms.Create("2026-07", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), 6120m),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            var paymentId = Guid.NewGuid();
            var participationId1 = Guid.NewGuid();
            var participationId2 = Guid.NewGuid();
            var completedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

            intent.Complete(paymentId, participationId1, completedAt);
            Should.Throw<InvalidOperationException>(() =>
                intent.Complete(paymentId, participationId2, completedAt));
        }

        [Fact]
        public void ProviderFailure_IsTerminalAndRejectsLateCompletion()
        {
            var intent = DirectOnyxCheckoutIntent.Create(
                tenantId: 1,
                customerId: 10,
                recruiterCustomerId: null,
                inviteCode: null,
                onyxMembershipId: 5,
                OnyxPlanTerms.Create("2026-07", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), 6120m),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
            intent.RecordCheckout(
                "checkout_failed",
                "https://payments.example.test/checkout_failed",
                new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc));

            intent.RecordProviderFailure(
                new DateTime(2026, 7, 1, 2, 0, 0, DateTimeKind.Utc),
                "Signed provider failure");

            intent.Status.ShouldBe(HostedPaymentCheckoutStatus.Failed);
            intent.TerminalEvidence.ShouldBe("Signed provider failure");
            Should.Throw<InvalidOperationException>(() => intent.Complete(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc)));
        }
    }
}
