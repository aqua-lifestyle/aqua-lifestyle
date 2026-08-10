using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AQGreenMonthlyObligationPaymentTests : AqualLifeStyleTestBase
    {
        private readonly IClubMemberEntryMonthlyObligationAppService _memberService;
        private readonly ProgrammePaymentConfirmationProcessor _confirmationProcessor;
        private readonly YocoPaymentNotificationProcessor _notificationProcessor;

        public AQGreenMonthlyObligationPaymentTests()
        {
            _memberService = Resolve<IClubMemberEntryMonthlyObligationAppService>();
            _confirmationProcessor = Resolve<ProgrammePaymentConfirmationProcessor>();
            _notificationProcessor = Resolve<YocoPaymentNotificationProcessor>();
        }

        [Fact]
        public async Task MemberCreatesOneCheckoutForTheExactOwnedObligation()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);

            var first = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });
            var repeated = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });

            first.CheckoutId.ShouldBe(repeated.CheckoutId);
            first.ObligationId.ShouldBe(scenario.JulyObligationId);
            first.PeriodYear.ShouldBe(2026);
            first.PeriodMonth.ShouldBe(7);
            first.Amount.ShouldBe(600m);
            first.Currency.ShouldBe("ZAR");
            first.CheckoutUrl.ShouldStartWith("https://payments.example.test/");
            await UsingDbContextAsync(1, async context =>
            {
                var checkout = await context.AQGreenMonthlyObligationCheckouts.SingleAsync();
                checkout.EntryMonthlyObligationId.ShouldBe(scenario.JulyObligationId);
                checkout.EntryParticipationId.ShouldBe(scenario.ParticipationId);
                checkout.PeriodMonth.ShouldBe(7);
                checkout.PaymentId.ShouldBeNull();
                (await context.MemberPayments.CountAsync(payment =>
                    payment.Purpose == MemberPaymentPurpose.EntryMonthlyCommitment))
                    .ShouldBe(0);
            });
        }

        [Fact]
        public async Task MemberRetryRecoversCheckoutWhoseProviderDetailsWereNotRecorded()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);
            var checkoutId = await UsingDbContextAsync(1, async context =>
            {
                var obligation = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JulyObligationId);
                var checkout = AQGreenMonthlyObligationCheckout.Create(
                    obligation,
                    DateTime.UtcNow);
                context.AQGreenMonthlyObligationCheckouts.Add(checkout);
                await context.SaveChangesAsync();
                return checkout.Id;
            });

            var recovered = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });

            recovered.CheckoutId.ShouldBe(checkoutId);
            recovered.CheckoutUrl.ShouldStartWith("https://payments.example.test/");
            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.AQGreenMonthlyObligationCheckouts
                    .SingleAsync();
                persisted.Status.ShouldBe(HostedPaymentCheckoutStatus.AwaitingPayment);
                persisted.ProviderCheckoutId.ShouldBe($"checkout_{checkoutId:N}");
            });
        }

        [Fact]
        public async Task MemberCannotCreateCheckoutForAnotherMemberOrArea()
        {
            var owner = await CreateScenarioAsync(1);
            var otherMember = await CreateScenarioAsync(1);
            var otherTenantId = await UsingDbContextAsync(null, async context =>
            {
                var suffix = Guid.NewGuid().ToString("N");
                var tenant = new Tenant($"Other{suffix}", $"Other {suffix}");
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
                return tenant.Id;
            });
            var otherArea = await CreateScenarioAsync(otherTenantId);
            SetCurrentUser(owner.UserId, 1);

            await Should.ThrowAsync<UserFriendlyException>(() =>
                _memberService.CreateCheckoutAsync(
                    new CreateEntryMonthlyObligationCheckoutInput
                    {
                        ObligationId = otherMember.JulyObligationId
                    }));
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _memberService.CreateCheckoutAsync(
                    new CreateEntryMonthlyObligationCheckoutInput
                    {
                        ObligationId = otherArea.JulyObligationId
                    }));

            await UsingDbContextAsync(null, async context =>
                (await context.AQGreenMonthlyObligationCheckouts
                    .IgnoreQueryFilters()
                    .CountAsync()).ShouldBe(0));
        }

        [Fact]
        public async Task InvalidPaidAndUnauthenticatedCheckoutRequestsAreRejected()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);

            await Should.ThrowAsync<UserFriendlyException>(() =>
                _memberService.CreateCheckoutAsync(
                    new CreateEntryMonthlyObligationCheckoutInput
                    {
                        ObligationId = Guid.NewGuid()
                    }));

            await SettleObligationAsync(
                scenario.JulyObligationId,
                "already-paid-before-checkout");
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _memberService.CreateCheckoutAsync(
                    new CreateEntryMonthlyObligationCheckoutInput
                    {
                        ObligationId = scenario.JulyObligationId
                    }));

            AbpSession.UserId = null;
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _memberService.CreateCheckoutAsync(
                    new CreateEntryMonthlyObligationCheckoutInput
                    {
                        ObligationId = scenario.JuneObligationId
                    }));
        }

        [Fact]
        public async Task ProviderConfirmationSettlesOnlyTheSelectedMonthAndReplayIsIdempotent()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);
            var checkout = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });
            var providerCheckoutId = $"checkout_{checkout.CheckoutId:N}";
            var notification = Notification(
                providerCheckoutId,
                "monthly-event-exact",
                "monthly-payment-exact");

            await _notificationProcessor.ProcessAsync(notification);
            await _notificationProcessor.ProcessAsync(notification);

            await UsingDbContextAsync(1, async context =>
            {
                var june = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JuneObligationId);
                var july = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JulyObligationId);
                var persistedCheckout = await context.AQGreenMonthlyObligationCheckouts
                    .SingleAsync(item => item.Id == checkout.CheckoutId);
                june.Status.ShouldNotBe(EntryMonthlyObligationStatus.Paid);
                june.PaymentId.ShouldBeNull();
                july.Status.ShouldBe(EntryMonthlyObligationStatus.Paid);
                july.PaymentId.ShouldNotBeNull();
                persistedCheckout.PaymentId.ShouldBe(july.PaymentId);
                persistedCheckout.AllocationStatus.ShouldBe(
                    AQGreenMonthlyPaymentAllocationStatus.Allocated);
                (await context.MemberPayments.CountAsync(payment =>
                    payment.Purpose == MemberPaymentPurpose.EntryMonthlyCommitment))
                    .ShouldBe(1);
                (await context.YocoWebhookReceipts.CountAsync()).ShouldBe(1);
            });
        }

        [Fact]
        public async Task ConflictingMerchantCheckoutReferenceIsRejected()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);
            var checkout = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });
            var notification = Notification(
                $"checkout_{checkout.CheckoutId:N}",
                "monthly-event-conflicting-reference",
                "monthly-payment-conflicting-reference",
                Guid.NewGuid());

            await Should.ThrowAsync<YocoWebhookValidationException>(() =>
                _notificationProcessor.ProcessAsync(notification));

            await UsingDbContextAsync(1, async context =>
            {
                var obligation = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JulyObligationId);
                obligation.PaymentId.ShouldBeNull();
                (await context.MemberPayments.CountAsync(payment =>
                    payment.Purpose == MemberPaymentPurpose.EntryMonthlyCommitment))
                    .ShouldBe(0);
                (await context.YocoWebhookReceipts.CountAsync()).ShouldBe(0);
            });
        }

        [Fact]
        public async Task PaymentAlreadyAllocatedToAnotherMonthIsRetainedForReconciliation()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);
            var julyCheckout = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });
            var juneCheckout = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JuneObligationId
                });
            var confirmedAt = DateTime.UtcNow.AddMinutes(2);
            var first = await _confirmationProcessor
                .ProcessAQGreenMonthlyObligationCheckoutAsync(
                    julyCheckout.CheckoutId,
                    "Yoco",
                    "monthly-payment-reused",
                    $"checkout_{julyCheckout.CheckoutId:N}",
                    600m,
                    "ZAR",
                    confirmedAt);

            var second = await _confirmationProcessor
                .ProcessAQGreenMonthlyObligationCheckoutAsync(
                    juneCheckout.CheckoutId,
                    "Yoco",
                    "monthly-payment-reused",
                    $"checkout_{juneCheckout.CheckoutId:N}",
                    600m,
                    "ZAR",
                    confirmedAt);

            second.PaymentId.ShouldBe(first.PaymentId);
            second.AllocationReconciliationRequired.ShouldBeTrue();
            await UsingDbContextAsync(1, async context =>
            {
                var june = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JuneObligationId);
                var july = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JulyObligationId);
                var reconciliation = await context.AQGreenMonthlyObligationCheckouts
                    .SingleAsync(item => item.Id == juneCheckout.CheckoutId);
                june.PaymentId.ShouldBeNull();
                july.PaymentId.ShouldBe(first.PaymentId);
                reconciliation.PaymentId.ShouldBe(first.PaymentId);
                reconciliation.AllocationStatus.ShouldBe(
                    AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired);
                reconciliation.AllocationEvidence.ShouldContain(
                    "already associated with another");
                (await context.MemberPayments.CountAsync(payment =>
                    payment.ExternalReference == "monthly-payment-reused"))
                    .ShouldBe(1);
            });
        }

        [Fact]
        public async Task ConfirmationForSubsequentlySettledObligationIsRetainedForReconciliation()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);
            var checkout = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });
            var originalPaymentId = await SettleObligationAsync(
                scenario.JulyObligationId,
                "settled-after-checkout");

            var result = await _confirmationProcessor
                .ProcessAQGreenMonthlyObligationCheckoutAsync(
                    checkout.CheckoutId,
                    "Yoco",
                    "unusual-extra-payment",
                    $"checkout_{checkout.CheckoutId:N}",
                    600m,
                    "ZAR",
                    DateTime.UtcNow.AddMinutes(2));

            result.AllocationReconciliationRequired.ShouldBeTrue();
            await UsingDbContextAsync(1, async context =>
            {
                var obligation = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JulyObligationId);
                var persistedCheckout = await context.AQGreenMonthlyObligationCheckouts
                    .SingleAsync(item => item.Id == checkout.CheckoutId);
                obligation.PaymentId.ShouldBe(originalPaymentId);
                persistedCheckout.PaymentId.ShouldNotBe(originalPaymentId);
                persistedCheckout.AllocationStatus.ShouldBe(
                    AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired);
                persistedCheckout.AllocationEvidence.ShouldContain("already settled");
                (await context.MemberPayments.CountAsync(payment =>
                    payment.Purpose == MemberPaymentPurpose.EntryMonthlyCommitment))
                    .ShouldBe(2);
            });
        }

        [Fact]
        public async Task DeletedCheckoutTargetRetainsPaymentWithoutSelectingAnotherMonth()
        {
            var scenario = await CreateScenarioAsync(1);
            SetCurrentUser(scenario.UserId, 1);
            var checkout = await _memberService.CreateCheckoutAsync(
                new CreateEntryMonthlyObligationCheckoutInput
                {
                    ObligationId = scenario.JulyObligationId
                });
            await UsingDbContextAsync(1, async context =>
            {
                var obligation = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JulyObligationId);
                obligation.IsDeleted = true;
                obligation.DeletionTime = DateTime.UtcNow;
                await context.SaveChangesAsync();
            });

            var result = await _confirmationProcessor
                .ProcessAQGreenMonthlyObligationCheckoutAsync(
                    checkout.CheckoutId,
                    "Yoco",
                    "deleted-target-payment",
                    $"checkout_{checkout.CheckoutId:N}",
                    600m,
                    "ZAR",
                    DateTime.UtcNow.AddMinutes(2));

            result.AllocationReconciliationRequired.ShouldBeTrue();
            await UsingDbContextAsync(1, async context =>
            {
                var june = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == scenario.JuneObligationId);
                var persistedCheckout = await context.AQGreenMonthlyObligationCheckouts
                    .SingleAsync(item => item.Id == checkout.CheckoutId);
                june.PaymentId.ShouldBeNull();
                persistedCheckout.PaymentId.ShouldNotBeNull();
                persistedCheckout.AllocationStatus.ShouldBe(
                    AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired);
                (await context.MemberPayments.CountAsync(payment =>
                    payment.ExternalReference == "deleted-target-payment"))
                    .ShouldBe(1);
            });
        }

        private async Task<Scenario> CreateScenarioAsync(int tenantId)
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = await CreateTestUserAsync(
                tenantId,
                $"monthly-{suffix}",
                $"monthly-{suffix}@example.com");
            return await UsingDbContextAsync(tenantId, async context =>
            {
                var customer = Customer.Create(
                    tenantId,
                    userId,
                    $"Monthly Member {suffix}",
                    new EmailAddress($"monthly-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                var effectiveFrom = new DateTime(
                    2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
                var participation = EntryParticipation.StartIndependently(
                    tenantId,
                    customer.Id,
                    EntryProgrammeTerms.Create(
                        $"monthly-terms-{suffix}",
                        effectiveFrom,
                        600m,
                        600m,
                        600m,
                        7),
                    effectiveFrom);
                var registration = ConfirmedPayment(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    $"registration-{suffix}",
                    effectiveFrom);
                participation.ApplyConfirmedActivationPayment(registration);
                var activation = ConfirmedPayment(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    $"activation-{suffix}",
                    effectiveFrom.AddMinutes(2));
                participation.ApplyConfirmedActivationPayment(activation);
                participation.ApproveByAdministrator(1L, effectiveFrom.AddMinutes(4));
                var policyVersion = $"monthly-policy-{suffix}";
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        policyVersion,
                        10,
                        EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(
                            2026,
                            6)));
                var june = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    6,
                    new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                    policyVersion);
                var july = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    7,
                    new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                    policyVersion);
                context.MemberPayments.AddRange(registration, activation);
                context.EntryParticipations.Add(participation);
                // Deliberately reverse chronological insertion order.
                context.EntryMonthlyObligations.AddRange(july, june);
                await context.SaveChangesAsync();
                return new Scenario(
                    userId,
                    participation.Id,
                    june.Id,
                    july.Id);
            });
        }

        private async Task<Guid> SettleObligationAsync(
            Guid obligationId,
            string reference)
        {
            return await UsingDbContextAsync(1, async context =>
            {
                var obligation = await context.EntryMonthlyObligations.SingleAsync(
                    item => item.Id == obligationId);
                var payment = ConfirmedPayment(
                    obligation.TenantId,
                    obligation.CustomerId,
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    reference,
                    DateTime.UtcNow);
                obligation.ApplyConfirmedPayment(payment);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
                return payment.Id;
            });
        }

        private static MemberPayment ConfirmedPayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            string reference,
            DateTime initiatedAt)
        {
            var payment = MemberPayment.CreatePending(
                tenantId,
                customerId,
                purpose,
                600m,
                "Test",
                reference,
                initiatedAt);
            payment.Confirm(initiatedAt.AddMinutes(1));
            return payment;
        }

        private static VerifiedYocoPaymentNotification Notification(
            string providerCheckoutId,
            string eventId,
            string paymentId,
            Guid? merchantCheckoutId = null) =>
            new VerifiedYocoPaymentNotification
            {
                EventId = eventId,
                EventType = "payment.succeeded",
                PaymentId = paymentId,
                AmountInCents = 60000,
                Currency = "ZAR",
                Mode = "test",
                ConfirmedAt = DateTimeOffset.UtcNow.AddMinutes(2),
                PayloadHash = new string('a', 64),
                Metadata = new Dictionary<string, JsonElement>
                {
                    [YocoCheckoutMetadata.ProviderCheckoutId] =
                        JsonSerializer.SerializeToElement(providerCheckoutId),
                    [YocoCheckoutMetadata.Purpose] =
                        JsonSerializer.SerializeToElement(
                            YocoCheckoutMetadata.AQGreenMonthlyObligationPurpose),
                    [YocoCheckoutMetadata.AQGreenMonthlyObligationCheckoutId] =
                        JsonSerializer.SerializeToElement(
                            (merchantCheckoutId ?? Guid.ParseExact(
                                providerCheckoutId.Substring("checkout_".Length),
                                "N")).ToString("N"))
                }
            };

        private sealed record Scenario(
            long UserId,
            Guid ParticipationId,
            Guid JuneObligationId,
            Guid JulyObligationId);
    }
}
