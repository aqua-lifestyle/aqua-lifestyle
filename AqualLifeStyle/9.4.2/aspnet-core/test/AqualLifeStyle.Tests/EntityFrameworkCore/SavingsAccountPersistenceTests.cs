using System;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Savings;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class SavingsAccountPersistenceTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly SavingsAccountTerms Terms =
            SavingsAccountTerms.Create(
                "savings-2026-07",
                EffectiveFrom,
                12,
                100m,
                20m,
                1,
                15);

        [Fact]
        public async Task MaturedSavingsAccountAndContributionLedger_RoundTrip()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"saver-{suffix}",
                $"saver-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Saving Club Member",
                    new EmailAddress($"saving-member-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var account = SavingsAccount.Open(
                    1,
                    customer.Id,
                    EffectiveFrom,
                    Terms);
                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.SavingsContribution,
                    500m,
                    "Yoco",
                    $"savings-contribution-{suffix}",
                    EffectiveFrom.AddDays(9));
                payment.Confirm(EffectiveFrom.AddDays(9).AddMinutes(1));
                account.ApplyConfirmedContribution(payment);
                account.Mature(account.MaturesAt);

                context.MemberPayments.Add(payment);
                context.SavingsAccounts.Add(account);
                await context.SaveChangesAsync();

                return new
                {
                    AccountId = account.Id,
                    PaymentId = payment.Id,
                    CustomerId = customer.Id
                };
            });

            await UsingDbContextAsync(1, async context =>
            {
                var account = await context.SavingsAccounts
                    .Include(item => item.Contributions)
                    .SingleAsync(item => item.Id == persisted.AccountId);

                Assert.Equal(SavingsAccountStatus.Matured, account.Status);
                Assert.Equal(500m, account.MaturityPrincipalAmount);
                Assert.Equal(100m, account.MaturityInterestAmount);
                Assert.Equal(600m, account.MaturityPayoutAmount);
                var contribution = Assert.Single(account.Contributions);
                Assert.Equal(20m, contribution.InterestRatePercent);
                Assert.Equal(100m, contribution.InterestAmount);
            });

            using var duplicateContext =
                LocalIocManager.Resolve<AqualLifeStyleDbContext>();
            var usedPayment = await duplicateContext.MemberPayments
                .SingleAsync(item => item.Id == persisted.PaymentId);
            var duplicateAccount = SavingsAccount.Open(
                1,
                persisted.CustomerId,
                EffectiveFrom,
                Terms);
            duplicateAccount.ApplyConfirmedContribution(usedPayment);
            duplicateContext.SavingsAccounts.Add(duplicateAccount);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                duplicateContext.SaveChangesAsync());
        }
    }
}
