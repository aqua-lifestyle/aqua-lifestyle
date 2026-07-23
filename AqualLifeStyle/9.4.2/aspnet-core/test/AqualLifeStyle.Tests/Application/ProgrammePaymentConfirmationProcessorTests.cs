using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class ProgrammePaymentConfirmationProcessorTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task VerifiedConfirmations_ActivateEntryAndOnyxIdempotently()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"payment-member-{suffix}",
                $"payment-member-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Payment Test Member",
                    new EmailAddress($"payment-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-{suffix}",
                    "Onyx direct-entry plan",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var entry = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    EntryProgrammeTerms.Create(
                        "2026-07",
                        EffectiveFrom,
                        600m,
                        600m,
                        600m,
                        7),
                    EffectiveFrom);
                var onyx = OnyxParticipation.StartDirectIndependently(
                    1,
                    customer.Id,
                    membership.Id,
                    OnyxPlanTerms.Create("2026-07", EffectiveFrom, 6120m),
                    EffectiveFrom);
                context.EntryParticipations.Add(entry);
                context.OnyxParticipations.Add(onyx);
                await context.SaveChangesAsync();

                return new
                {
                    CustomerId = customer.Id,
                    EntryId = entry.Id,
                    OnyxId = onyx.Id
                };
            });

            var processor = LocalIocManager.Resolve<ProgrammePaymentConfirmationProcessor>();
            Assert.False(typeof(IApplicationService).IsAssignableFrom(processor.GetType()));

            var registrationReference = $"entry-registration-{suffix}";
            var registration = CreateConfirmation(
                persisted.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                registrationReference);

            var firstRegistration = await processor.ProcessAsync(registration);
            var repeatedRegistration = await processor.ProcessAsync(registration);
            var activation = await processor.ProcessAsync(CreateConfirmation(
                persisted.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                600m,
                $"entry-activation-{suffix}"));
            var onyx = await processor.ProcessAsync(CreateConfirmation(
                persisted.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                $"onyx-direct-{suffix}"));

            Assert.False(firstRegistration.WasAlreadyProcessed);
            Assert.True(repeatedRegistration.WasAlreadyProcessed);
            Assert.Equal(firstRegistration.PaymentId, repeatedRegistration.PaymentId);
            Assert.Equal(persisted.EntryId, activation.ParticipationId);
            Assert.Equal(ProgrammeParticipationKind.Entry, activation.ParticipationKind);
            Assert.Equal(persisted.OnyxId, onyx.ParticipationId);
            Assert.Equal(ProgrammeParticipationKind.Onyx, onyx.ParticipationKind);

            await UsingDbContextAsync(1, async context =>
            {
                var entry = await context.EntryParticipations
                    .SingleAsync(participation => participation.Id == persisted.EntryId);
                var onyxParticipation = await context.OnyxParticipations
                    .SingleAsync(participation => participation.Id == persisted.OnyxId);
                var payments = await context.MemberPayments
                    .Where(payment => payment.CustomerId == persisted.CustomerId)
                    .ToListAsync();

                Assert.Equal(EntryParticipationStatus.Active, entry.Status);
                Assert.Equal(OnyxParticipationStatus.Active, onyxParticipation.Status);
                Assert.Equal(3, payments.Count);
                Assert.All(payments, payment => Assert.Equal(MemberPaymentStatus.Confirmed, payment.Status));
                Assert.All(payments, payment => Assert.NotEqual(default, payment.CreationTime));
                Assert.All(payments, payment => Assert.True(payment.CreatorUserId.HasValue));
                Assert.All(payments, payment => Assert.True(payment.ConfirmedAt.HasValue));
                Assert.All(payments, payment => Assert.False(string.IsNullOrWhiteSpace(payment.ExternalReference)));
            });

            var mismatchedReuse = CreateConfirmation(
                persisted.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                601m,
                registrationReference);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessAsync(mismatchedReuse));
        }

        private static ConfirmedProgrammePayment CreateConfirmation(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string externalReference)
        {
            return new ConfirmedProgrammePayment(
                tenantId: 1,
                customerId,
                purpose,
                amount,
                currency: "zar",
                provider: "yoco",
                externalReference,
                initiatedAt: EffectiveFrom,
                confirmedAt: EffectiveFrom.AddMinutes(1));
        }
    }
}
