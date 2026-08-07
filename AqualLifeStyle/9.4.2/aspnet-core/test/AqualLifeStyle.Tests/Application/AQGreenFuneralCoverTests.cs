using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AQGreenFuneralCoverEntitlementTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime TermsEffectiveFrom =
            new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        private static readonly AQGreenFuneralCoverTerms Terms =
            AQGreenFuneralCoverTerms.Create(
                "2026-08-funeral-30000",
                TermsEffectiveFrom,
                30000m);

        [Fact]
        public void GrantForJoiningCompletion_RecordsIncludedBenefitForFullPayment()
        {
            var participation = CreateJoiningCompletedParticipation(fullPayment: true);

            var entitlement = AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                participation,
                Terms,
                TermsEffectiveFrom.AddDays(1));

            entitlement.TenantId.ShouldBe(1);
            entitlement.EntryParticipationId.ShouldBe(participation.Id);
            entitlement.CustomerId.ShouldBe(participation.CustomerId);
            entitlement.FuneralCoverAmount.ShouldBe(30000m);
            entitlement.Currency.ShouldBe("ZAR");
            entitlement.TermsVersion.ShouldBe("2026-08-funeral-30000");
            entitlement.Status.ShouldBe(AQGreenFuneralCoverStatus.Included);
            entitlement.IncludedAt.ShouldBe(TermsEffectiveFrom.AddDays(1));
        }

        [Fact]
        public void GrantForJoiningCompletion_RecordsIncludedBenefitForTwoInstallments()
        {
            var participation = CreateJoiningCompletedParticipation(fullPayment: false);

            var entitlement = AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                participation,
                Terms,
                TermsEffectiveFrom.AddDays(1));

            entitlement.EntryParticipationId.ShouldBe(participation.Id);
            entitlement.FuneralCoverAmount.ShouldBe(30000m);
            entitlement.Status.ShouldBe(AQGreenFuneralCoverStatus.Included);
        }

        [Fact]
        public void GrantForJoiningCompletion_RejectsWhenJoiningObligationNotSatisfied()
        {
            var participation = CreateParticipationAwaitingJoiningPayment();

            Should.Throw<InvalidOperationException>(() =>
                AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                    participation,
                    Terms,
                    TermsEffectiveFrom.AddDays(1)));
        }

        [Fact]
        public void GrantForJoiningCompletion_RejectsBeforeTermsEffectiveDate()
        {
            var participation = CreateJoiningCompletedParticipation(fullPayment: true);

            Should.Throw<ArgumentException>(() =>
                AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                    participation,
                    Terms,
                    TermsEffectiveFrom.AddDays(-1)));
        }

        [Fact]
        public void GrantForJoiningCompletion_RejectsHistoricalSplitLifecycle()
        {
            var participation = CreateHistoricalSplitParticipation();

            Should.Throw<InvalidOperationException>(() =>
                AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                    participation,
                    Terms,
                    TermsEffectiveFrom.AddDays(1)));
        }

        private static EntryParticipation CreateJoiningCompletedParticipation(bool fullPayment)
        {
            var terms = fullPayment
                ? EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "test-1200",
                    TermsEffectiveFrom,
                    1200m,
                    600m,
                    7)
                : EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                    "test-1200-flex",
                    TermsEffectiveFrom,
                    1200m,
                    600m,
                    600m,
                    7);
            var participation = EntryParticipation.StartIndependently(
                1,
                11,
                terms,
                TermsEffectiveFrom);
            if (fullPayment)
            {
                var payment = CreateConfirmedPayment(
                    11,
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m);
                participation.ApplyConfirmedJoiningPayment(payment);
                return participation;
            }

            participation.SelectJoiningPaymentSchedule(
                AQGreenJoiningPaymentSchedule.TwoInstallments);
            var first = CreateConfirmedPayment(
                11,
                MemberPaymentPurpose.AQGreenJoining,
                600m);
            participation.ApplyConfirmedJoiningPayment(first, AQGreenJoiningPaymentStage.FirstInstallment);
            var second = CreateConfirmedPayment(
                11,
                MemberPaymentPurpose.AQGreenJoining,
                600m);
            participation.ApplyConfirmedJoiningPayment(second, AQGreenJoiningPaymentStage.SecondInstallment);
            return participation;
        }

        private static EntryParticipation CreateParticipationAwaitingJoiningPayment()
        {
            var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                "test-1200",
                TermsEffectiveFrom,
                1200m,
                600m,
                7);
            return EntryParticipation.StartIndependently(
                1,
                11,
                terms,
                TermsEffectiveFrom);
        }

        private static EntryParticipation CreateHistoricalSplitParticipation()
        {
            var terms = EntryProgrammeTerms.Create(
                "legacy",
                TermsEffectiveFrom,
                600m,
                600m,
                600m,
                7);
            return EntryParticipation.StartIndependently(
                1,
                11,
                terms,
                TermsEffectiveFrom);
        }

        private static MemberPayment CreateConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Test",
                Guid.NewGuid().ToString("N"),
                TermsEffectiveFrom);
            payment.Confirm(TermsEffectiveFrom.AddHours(1));
            return payment;
        }
    }

    public class AQGreenFuneralCoverInclusionProcessorTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime TermsEffectiveFrom =
            new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        private readonly AQGreenFuneralCoverInclusionProcessor _processor;

        public AQGreenFuneralCoverInclusionProcessorTests()
        {
            _processor = Resolve<AQGreenFuneralCoverInclusionProcessor>();
        }

        [Fact]
        public async Task EnsureIncludedAsync_IsIdempotent_AndGrantsOnce()
        {
            var participationId = await CreateJoiningCompletedParticipationAsync(
                $"funeral-{Guid.NewGuid():N}");

            var first = await _processor.EnsureIncludedAsync(
                await LoadParticipationAsync(participationId),
                TermsEffectiveFrom.AddMinutes(5));
            var second = await _processor.EnsureIncludedAsync(
                await LoadParticipationAsync(participationId),
                TermsEffectiveFrom.AddMinutes(6));

            first.Included.ShouldBeTrue();
            second.Included.ShouldBeTrue();
            first.EntitlementId.ShouldNotBeNull();
            second.EntitlementId.ShouldBe(first.EntitlementId);

            var count = await UsingDbContextAsync(1, async context =>
                await context.AQGreenFuneralCoverEntitlements.CountAsync(
                    item => item.EntryParticipationId == participationId));
            count.ShouldBe(1);
        }

        [Fact]
        public async Task EnsureIncludedAsync_SkipsWhenJoiningNotSatisfied()
        {
            var participationId = await CreateAwaitingParticipationAsync(
                $"awaiting-{Guid.NewGuid():N}");

            var result = await _processor.EnsureIncludedAsync(
                await LoadParticipationAsync(participationId),
                TermsEffectiveFrom.AddMinutes(5));

            result.Included.ShouldBeFalse();
            result.EntitlementId.ShouldBeNull();
        }

        private async Task<Guid> CreateJoiningCompletedParticipationAsync(string suffix)
        {
            var email = $"{suffix}@example.com";
            var userId = await CreateTestUserAsync(1, suffix, email);
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Funeral Cover Member",
                    new EmailAddress(email));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "test-1200",
                    TermsEffectiveFrom,
                    1200m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    TermsEffectiveFrom);
                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m,
                    "Test",
                    $"join-{suffix}",
                    TermsEffectiveFrom);
                payment.Confirm(TermsEffectiveFrom.AddHours(1));
                participation.ApplyConfirmedJoiningPayment(payment);
                context.EntryParticipations.Add(participation);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
                return participation.Id;
            });
        }

        private async Task<Guid> CreateAwaitingParticipationAsync(string suffix)
        {
            var email = $"{suffix}@example.com";
            var userId = await CreateTestUserAsync(1, suffix, email);
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Awaiting Member",
                    new EmailAddress(email));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "test-1200",
                    TermsEffectiveFrom,
                    1200m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    TermsEffectiveFrom);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return participation.Id;
            });
        }

        private Task<EntryParticipation> LoadParticipationAsync(Guid participationId)
        {
            return UsingDbContextAsync(1, async context =>
                await context.EntryParticipations.SingleAsync(
                    item => item.Id == participationId));
        }
    }

    public class AQGreenFuneralCoverTwoInstallmentPaymentTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime TermsEffectiveFrom =
            new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task FuneralCover_IsIncludedOnlyAfterSecondInstalment()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"funeral-install-{suffix}",
                $"funeral-install-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Funeral Installment Member",
                    new EmailAddress($"funeral-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                    "test-1200-flex",
                    TermsEffectiveFrom,
                    1200m,
                    600m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    TermsEffectiveFrom);
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return new { CustomerId = customer.Id, ParticipationId = participation.Id };
            });

            var processor = LocalIocManager.Resolve<ProgrammePaymentConfirmationProcessor>();

            var firstCheckout = await CreateCheckoutAsync(
                persisted.ParticipationId,
                persisted.CustomerId,
                AQGreenJoiningPaymentStage.FirstInstallment,
                $"first-{suffix}",
                600m);
            var firstResult = await processor.ProcessAQGreenJoiningCheckoutAsync(
                firstCheckout.Id,
                "Yoco",
                $"pay-first-{suffix}",
                firstCheckout.ProviderCheckoutId,
                600m,
                "ZAR",
                TermsEffectiveFrom.AddDays(2));

            var entitlementAfterFirst = await UsingDbContextAsync(1, async context =>
                await context.AQGreenFuneralCoverEntitlements.CountAsync(
                    item => item.EntryParticipationId == persisted.ParticipationId));
            entitlementAfterFirst.ShouldBe(0);

            var secondCheckout = await CreateCheckoutAsync(
                persisted.ParticipationId,
                persisted.CustomerId,
                AQGreenJoiningPaymentStage.SecondInstallment,
                $"second-{suffix}",
                600m);
            var secondResult = await processor.ProcessAQGreenJoiningCheckoutAsync(
                secondCheckout.Id,
                "Yoco",
                $"pay-second-{suffix}",
                secondCheckout.ProviderCheckoutId,
                600m,
                "ZAR",
                TermsEffectiveFrom.AddDays(3));

            secondResult.ParticipationId.ShouldBe(persisted.ParticipationId);

            var entitlements = await UsingDbContextAsync(1, async context =>
                await context.AQGreenFuneralCoverEntitlements
                    .Where(item => item.EntryParticipationId == persisted.ParticipationId)
                    .ToListAsync());
            entitlements.Count.ShouldBe(1);
            entitlements.Single().Status.ShouldBe(AQGreenFuneralCoverStatus.Included);
            entitlements.Single().FuneralCoverAmount.ShouldBe(30000m);

            var participation = await UsingDbContextAsync(1, async context =>
                await context.EntryParticipations.SingleAsync(
                    item => item.Id == persisted.ParticipationId));
            participation.IsJoiningObligationSatisfied.ShouldBeTrue();
            participation.GetConfirmedJoiningAmount().ShouldBe(1200m);
        }

        private async Task<AQGreenJoiningCheckout> CreateCheckoutAsync(
            Guid participationId,
            int customerId,
            AQGreenJoiningPaymentStage stage,
            string suffix,
            decimal amount)
        {
            var checkout = await UsingDbContextAsync(1, async context =>
            {
                var checkout = AQGreenJoiningCheckout.Create(
                    1,
                    participationId,
                    customerId,
                    AQGreenJoiningPaymentSchedule.TwoInstallments,
                    stage,
                    amount,
                    "ZAR",
                    TermsEffectiveFrom);
                checkout.RecordCheckout(
                    $"ch_{suffix}",
                    $"https://payments.example.test/ch_{suffix}",
                    TermsEffectiveFrom.AddMinutes(1));
                context.AQGreenJoiningCheckouts.Add(checkout);
                await context.SaveChangesAsync();
                return checkout;
            });
            return checkout;
        }
    }
}
