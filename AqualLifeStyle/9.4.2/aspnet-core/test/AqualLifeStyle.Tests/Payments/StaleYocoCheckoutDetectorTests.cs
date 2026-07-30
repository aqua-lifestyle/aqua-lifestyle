using System;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments.Yoco;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Payments
{
    public class StaleYocoCheckoutDetectorTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime CheckoutCreatedAt =
            new(2026, 7, 28, 8, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task DetectAsync_CountsOnlyCheckoutsOlderThanTheCutoff()
        {
            await CreateAQGreenCheckoutAsync(CheckoutCreatedAt);
            await CreateAQGreenCheckoutAsync(CheckoutCreatedAt.AddHours(2));

            var snapshot = await Resolve<StaleYocoCheckoutDetector>().DetectAsync(
                CheckoutCreatedAt.AddHours(1));

            snapshot.AQGreenCount.ShouldBe(1);
            snapshot.OnyxCount.ShouldBe(0);
            snapshot.TotalCount.ShouldBe(1);
            snapshot.OldestCheckoutCreatedAt.ShouldBe(CheckoutCreatedAt);
        }

        [Fact]
        public async Task DetectAsync_WithNoStaleCheckouts_ReturnsAnEmptySnapshot()
        {
            var snapshot = await Resolve<StaleYocoCheckoutDetector>().DetectAsync(
                CheckoutCreatedAt);

            snapshot.AQGreenCount.ShouldBe(0);
            snapshot.OnyxCount.ShouldBe(0);
            snapshot.TotalCount.ShouldBe(0);
            snapshot.OldestCheckoutCreatedAt.ShouldBeNull();
        }

        [Fact]
        public async Task DetectAsync_RejectsANonUtcCutoff()
        {
            await Should.ThrowAsync<ArgumentException>(() =>
                Resolve<StaleYocoCheckoutDetector>().DetectAsync(
                    DateTime.SpecifyKind(CheckoutCreatedAt, DateTimeKind.Unspecified)));
        }

        private async Task CreateAQGreenCheckoutAsync(DateTime createdAt)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"stale-checkout-{suffix}",
                $"stale-checkout-{suffix}@example.com");

            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Stale Checkout Test",
                    new EmailAddress($"stale-checkout-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    $"stale-{suffix.Substring(0, 12)}",
                    createdAt,
                    joiningPaymentAmount: 1200m,
                    monthlyCommitmentAmount: 600m,
                    gracePeriodDays: 7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    createdAt);
                var checkout = AQGreenJoiningCheckout.Create(
                    1,
                    participation.Id,
                    customer.Id,
                    1200m,
                    "ZAR",
                    createdAt);
                checkout.RecordCheckout(
                    $"ch_stale_{suffix}",
                    $"https://payments.example.test/ch_stale_{suffix}",
                    createdAt);

                context.EntryParticipations.Add(participation);
                context.AQGreenJoiningCheckouts.Add(checkout);
                await context.SaveChangesAsync();
            });
        }
    }
}
