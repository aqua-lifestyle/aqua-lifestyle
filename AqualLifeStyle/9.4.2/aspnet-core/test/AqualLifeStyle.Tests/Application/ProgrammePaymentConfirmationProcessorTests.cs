using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class ProgrammePaymentConfirmationProcessorTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly DateTime AQGreenSinglePaymentEffectiveFrom =
            new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task AQGreenJoiningCheckout_ActivatesAfterOneVerifiedTwelveHundredRandPayment()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"aqgreen-payment-member-{suffix}",
                $"aqgreen-payment-member-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "AQGreen Payment Test Member",
                    new EmailAddress($"aqgreen-payment-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "aqgreen-2026-07-single-1200",
                    AQGreenSinglePaymentEffectiveFrom,
                    joiningPaymentAmount: 1200m,
                    monthlyCommitmentAmount: 600m,
                    gracePeriodDays: 7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    AQGreenSinglePaymentEffectiveFrom);
                var checkout = AQGreenJoiningCheckout.Create(
                    1,
                    participation.Id,
                    customer.Id,
                    participation.JoiningPaymentAmount,
                    participation.Currency,
                    AQGreenSinglePaymentEffectiveFrom);
                context.EntryParticipations.Add(participation);
                context.AQGreenJoiningCheckouts.Add(checkout);
                await context.SaveChangesAsync();

                return new
                {
                    CustomerId = customer.Id,
                    ParticipationId = participation.Id,
                    CheckoutId = checkout.Id
                };
            });

            var processor = LocalIocManager.Resolve<ProgrammePaymentConfirmationProcessor>();
            Assert.False(typeof(IApplicationService).IsAssignableFrom(processor.GetType()));

            var checkout = await UsingDbContextAsync(1, async context =>
                await context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.Id == persisted.CheckoutId));

            using (var uow = LocalIocManager.Resolve<IUnitOfWorkManager>().Begin(
                new UnitOfWorkOptions { IsTransactional = true }))
            using (LocalIocManager.Resolve<IUnitOfWorkManager>().Current.SetTenantId(1))
            {
                var checkoutRepo = LocalIocManager.Resolve<Abp.Domain.Repositories.IRepository<AQGreenJoiningCheckout, Guid>>();
                checkout = await checkoutRepo.GetAsync(checkout.Id);
                checkout.RecordCheckout(
                    $"checkout_{checkout.Id:N}",
                    $"https://payments.example.test/checkout/{checkout.Id:N}",
                    AQGreenSinglePaymentEffectiveFrom);
                await LocalIocManager.Resolve<IUnitOfWorkManager>().Current.SaveChangesAsync();
                await uow.CompleteAsync();
            }

            var paymentReference = $"aqgreen-unit-{suffix}";
            var first = await processor.ProcessAQGreenJoiningCheckoutAsync(
                persisted.CheckoutId,
                "Yoco",
                paymentReference,
                checkout.ProviderCheckoutId,
                1200m,
                "ZAR",
                AQGreenSinglePaymentEffectiveFrom.AddMinutes(1));
            var repeated = await processor.ProcessAQGreenJoiningCheckoutAsync(
                persisted.CheckoutId,
                "Yoco",
                paymentReference,
                checkout.ProviderCheckoutId,
                1200m,
                "ZAR",
                AQGreenSinglePaymentEffectiveFrom.AddMinutes(1));

            Assert.False(first.WasAlreadyProcessed);
            Assert.True(repeated.WasAlreadyProcessed);
            Assert.Equal(first.PaymentId, repeated.PaymentId);
            Assert.Equal(persisted.ParticipationId, repeated.ParticipationId);
            Assert.Equal(ProgrammeParticipationKind.Entry, first.ParticipationKind);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                processor.ProcessAQGreenJoiningCheckoutAsync(
                    persisted.CheckoutId,
                    "Yoco",
                    $"wrong-checkout-{Guid.NewGuid():N}",
                    "checkout_that_does_not_match",
                    1200m,
                    "ZAR",
                    AQGreenSinglePaymentEffectiveFrom.AddMinutes(1)));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                processor.ProcessAQGreenJoiningCheckoutAsync(
                    persisted.CheckoutId,
                    "Yoco",
                    $"wrong-amount-{Guid.NewGuid():N}",
                    checkout.ProviderCheckoutId,
                    600m,
                    "ZAR",
                    AQGreenSinglePaymentEffectiveFrom.AddMinutes(1)));

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == persisted.ParticipationId);
                participation.Status.ShouldBe(EntryParticipationStatus.Active);
                participation.JoiningPaymentId.ShouldBe(first.PaymentId);
                var persistedCheckout = await context.AQGreenJoiningCheckouts
                    .SingleAsync(item => item.Id == persisted.CheckoutId);
                persistedCheckout.Status.ShouldBe(HostedPaymentCheckoutStatus.Completed);
                persistedCheckout.PaymentId.ShouldBe(first.PaymentId);
                var payments = await context.MemberPayments
                    .Where(item => item.CustomerId == persisted.CustomerId)
                    .ToListAsync();
                payments.Count.ShouldBe(1);
                payments.Single().Purpose.ShouldBe(MemberPaymentPurpose.AQGreenJoining);
                payments.Single().Status.ShouldBe(MemberPaymentStatus.Confirmed);
            });
        }

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
                var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);

                Assert.Equal(EntryParticipationStatus.Active, entry.Status);
                Assert.Equal(OnyxParticipationStatus.Active, onyxParticipation.Status);
                Assert.Equal(AquaUserRole.Member, user.Role);
                Assert.Contains(
                    await context.Roles
                        .Where(role => context.UserRoles
                            .Where(userRole => userRole.UserId == userId)
                            .Select(userRole => userRole.RoleId)
                            .Contains(role.Id))
                        .Select(role => role.Name)
                        .ToListAsync(),
                    roleName => roleName == AquaUserRole.Member.ToString());
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

        [Fact]
        public async Task ActiveParticipation_DoesNotDemoteAnExistingBusinessRole()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"facilitator-{suffix}",
                $"facilitator-{suffix}@example.com");

            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
                user.SetRole(AquaUserRole.Facilitator);
                var customer = Customer.Create(
                    1,
                    userId,
                    "Existing Facilitator",
                    new EmailAddress($"facilitator-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-{suffix}",
                    "Onyx direct-entry plan",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();
                context.OnyxParticipations.Add(
                    OnyxParticipation.StartDirectIndependently(
                        1,
                        customer.Id,
                        membership.Id,
                        OnyxPlanTerms.Create("2026-07", EffectiveFrom, 6120m),
                        EffectiveFrom));
                await context.SaveChangesAsync();
                return customer.Id;
            });

            await LocalIocManager.Resolve<ProgrammePaymentConfirmationProcessor>()
                .ProcessAsync(CreateConfirmation(
                    customerId,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    $"onyx-facilitator-{suffix}"));

            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
                Assert.Equal(AquaUserRole.Facilitator, user.Role);
            });
        }

        [Fact]
        public async Task EarlyAQGreenWebhook_WhenCheckoutNotRecorded_ReturnsTransientException()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"aqgreen-early-{suffix}",
                $"aqgreen-early-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "AQGreen Early Webhook Test",
                    new EmailAddress($"aqgreen-early-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "aqgreen-2026-07-single-1200",
                    AQGreenSinglePaymentEffectiveFrom,
                    joiningPaymentAmount: 1200m,
                    monthlyCommitmentAmount: 600m,
                    gracePeriodDays: 7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    AQGreenSinglePaymentEffectiveFrom);
                var checkout = AQGreenJoiningCheckout.Create(
                    1,
                    participation.Id,
                    customer.Id,
                    1200m,
                    "ZAR",
                    AQGreenSinglePaymentEffectiveFrom);
                context.EntryParticipations.Add(participation);
                context.AQGreenJoiningCheckouts.Add(checkout);
                await context.SaveChangesAsync();

                return new
                {
                    CustomerId = customer.Id,
                    CheckoutId = checkout.Id
                };
            });

            var processor = LocalIocManager.Resolve<ProgrammePaymentConfirmationProcessor>();

            var ex = await Assert.ThrowsAsync<YocoWebhookTransientException>(
                () => processor.ProcessAQGreenJoiningCheckoutAsync(
                    persisted.CheckoutId,
                    "Yoco",
                    $"early-{suffix}",
                    "checkout_early",
                    1200m,
                    "ZAR",
                    AQGreenSinglePaymentEffectiveFrom.AddMinutes(1)));
            ex.Message.ShouldContain("not yet ready");
        }

        [Fact]
        public async Task ConcurrentDirectOnyxWebhooks_CompleteIdempotently()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"onyx-concurrent-{suffix}",
                $"onyx-concurrent-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Onyx Concurrent Test",
                    new EmailAddress($"onyx-concurrent-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-{suffix}",
                    "Onyx direct-entry plan",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var intent = DirectOnyxCheckoutIntent.Create(
                    1,
                    customer.Id,
                    null,
                    null,
                    membership.Id,
                    OnyxPlanTerms.Create("2026-07", EffectiveFrom, 6120m),
                    EffectiveFrom);
                intent.RecordCheckout(
                    "checkout_concurrent",
                    "https://payments.example.test/checkout",
                    EffectiveFrom);
                context.DirectOnyxCheckoutIntents.Add(intent);
                await context.SaveChangesAsync();

                return new
                {
                    CustomerId = customer.Id,
                    IntentId = intent.Id
                };
            });

            var processor = LocalIocManager.Resolve<ProgrammePaymentConfirmationProcessor>();
            var paymentReference = $"onyx-concurrent-{suffix}";

            async Task<ProgrammePaymentConfirmationResult> ProcessAsync()
            {
                using var uow = LocalIocManager.Resolve<IUnitOfWorkManager>().Begin(
                    new UnitOfWorkOptions { IsTransactional = true });
                using (LocalIocManager.Resolve<IUnitOfWorkManager>().Current.SetTenantId(1))
                {
                    var result = await processor.ProcessDirectOnyxCheckoutAsync(
                        persisted.IntentId,
                        "Yoco",
                        paymentReference,
                        "checkout_concurrent",
                        6120m,
                        "ZAR",
                        EffectiveFrom.AddMinutes(1));
                    await LocalIocManager.Resolve<IUnitOfWorkManager>().Current.SaveChangesAsync();
                    await uow.CompleteAsync();
                    return result;
                }
            }

            var result1 = await ProcessAsync();
            var result2 = await ProcessAsync();

            result1.PaymentId.ShouldBe(result2.PaymentId);
            result1.ParticipationId.ShouldBe(result2.ParticipationId);
            result1.ParticipationKind.ShouldBe(ProgrammeParticipationKind.Onyx);

            await UsingDbContextAsync(1, async context =>
            {
                var payment = await context.MemberPayments
                    .SingleAsync(p => p.ExternalReference == paymentReference);
                payment.Status.ShouldBe(MemberPaymentStatus.Confirmed);

                var participation = await context.OnyxParticipations
                    .SingleAsync(p => p.CustomerId == persisted.CustomerId);
                participation.Status.ShouldBe(OnyxParticipationStatus.Active);
                participation.DirectEntryPaymentId.ShouldBe(payment.Id);
            });
        }

        [Fact]
        public async Task YocoNotification_RoutesAQGreenByDocumentedCheckoutIdAndRecordsOneReceipt()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"aqgreen-webhook-{suffix}",
                $"aqgreen-webhook-{suffix}@example.com");
            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "AQGreen Webhook Test",
                    new EmailAddress($"aqgreen-webhook-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    EntryProgrammeTerms.CreateSingleJoiningPayment(
                        "aqgreen-2026-07-single-1200",
                        AQGreenSinglePaymentEffectiveFrom,
                        1200m,
                        600m,
                        7),
                    AQGreenSinglePaymentEffectiveFrom);
                var checkout = AQGreenJoiningCheckout.Create(
                    1,
                    participation.Id,
                    customer.Id,
                    1200m,
                    "ZAR",
                    AQGreenSinglePaymentEffectiveFrom);
                var providerCheckoutId = $"ch_aqgreen_{suffix}";
                checkout.RecordCheckout(
                    providerCheckoutId,
                    $"https://payments.example.test/{providerCheckoutId}",
                    AQGreenSinglePaymentEffectiveFrom);
                context.EntryParticipations.Add(participation);
                context.AQGreenJoiningCheckouts.Add(checkout);
                await context.SaveChangesAsync();
                return new
                {
                    CheckoutId = checkout.Id,
                    ParticipationId = participation.Id,
                    ProviderCheckoutId = providerCheckoutId
                };
            });

            var notification = CreateNotification(
                $"evt_{suffix}",
                $"pay_{suffix}",
                persisted.ProviderCheckoutId,
                120000);
            var processor = Resolve<YocoPaymentNotificationProcessor>();

            await processor.ProcessAsync(notification);
            await processor.ProcessAsync(notification);

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.Id == persisted.ParticipationId);
                participation.Status.ShouldBe(EntryParticipationStatus.Active);
                (await context.MemberPayments.CountAsync(payment =>
                    payment.ExternalReference == notification.PaymentId)).ShouldBe(1);
                var receipt = await context.YocoWebhookReceipts.SingleAsync();
                receipt.EventId.ShouldBe(notification.EventId);
                receipt.ProviderCheckoutId.ShouldBe(persisted.ProviderCheckoutId);
                receipt.Programme.ShouldBe(YocoCheckoutProgramme.AQGreen);
                receipt.CheckoutReferenceId.ShouldBe(persisted.CheckoutId);
            });

            var conflictingReplay = CreateNotification(
                notification.EventId,
                notification.PaymentId,
                persisted.ProviderCheckoutId,
                120000);
            conflictingReplay.PayloadHash = new string('B', 64);
            await Should.ThrowAsync<YocoWebhookValidationException>(() =>
                processor.ProcessAsync(conflictingReplay));
        }

        [Fact]
        public async Task YocoNotification_RoutesOnyxByDocumentedCheckoutIdOnly()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"onyx-webhook-{suffix}",
                $"onyx-webhook-{suffix}@example.com");
            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Onyx Webhook Test",
                    new EmailAddress($"onyx-webhook-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-webhook-{suffix}",
                    "Onyx webhook test plan",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var checkout = DirectOnyxCheckoutIntent.Create(
                    1,
                    customer.Id,
                    null,
                    null,
                    membership.Id,
                    OnyxPlanTerms.Create("2026-07", EffectiveFrom, 6120m),
                    EffectiveFrom);
                var providerCheckoutId = $"ch_onyx_{suffix}";
                checkout.RecordCheckout(
                    providerCheckoutId,
                    $"https://payments.example.test/{providerCheckoutId}",
                    EffectiveFrom);
                context.DirectOnyxCheckoutIntents.Add(checkout);
                await context.SaveChangesAsync();
                return new
                {
                    CheckoutId = checkout.Id,
                    CustomerId = customer.Id,
                    ProviderCheckoutId = providerCheckoutId
                };
            });

            var notification = CreateNotification(
                $"evt_{suffix}",
                $"pay_{suffix}",
                persisted.ProviderCheckoutId,
                612000);

            await Resolve<YocoPaymentNotificationProcessor>().ProcessAsync(notification);

            await UsingDbContextAsync(1, async context =>
            {
                (await context.OnyxParticipations.SingleAsync(participation =>
                    participation.CustomerId == persisted.CustomerId)).Status.ShouldBe(
                    OnyxParticipationStatus.Active);
                var receipt = await context.YocoWebhookReceipts.SingleAsync();
                receipt.Programme.ShouldBe(YocoCheckoutProgramme.Onyx);
                receipt.CheckoutReferenceId.ShouldBe(persisted.CheckoutId);
            });
        }

        [Fact]
        public async Task YocoNotification_UnknownCheckoutRemainsRetryableWithoutReceipt()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var notification = CreateNotification(
                $"evt_{suffix}",
                $"pay_{suffix}",
                $"ch_unknown_{suffix}",
                120000);

            await Should.ThrowAsync<YocoWebhookTransientException>(() =>
                Resolve<YocoPaymentNotificationProcessor>().ProcessAsync(notification));

            await UsingDbContextAsync(1, async context =>
                (await context.YocoWebhookReceipts.AnyAsync()).ShouldBeFalse());
        }

        private static VerifiedYocoPaymentNotification CreateNotification(
            string eventId,
            string paymentId,
            string providerCheckoutId,
            int amountInCents) => new()
            {
                EventId = eventId,
                EventType = "payment.succeeded",
                PaymentId = paymentId,
                AmountInCents = amountInCents,
                Currency = "ZAR",
                Mode = "test",
                ConfirmedAt = new DateTimeOffset(
                    AQGreenSinglePaymentEffectiveFrom.AddMinutes(1),
                    TimeSpan.Zero),
                PayloadHash = new string('A', 64),
                Metadata = new Dictionary<string, JsonElement>
                {
                    [YocoCheckoutMetadata.ProviderCheckoutId] =
                        JsonSerializer.SerializeToElement(providerCheckoutId)
                }
            };

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
