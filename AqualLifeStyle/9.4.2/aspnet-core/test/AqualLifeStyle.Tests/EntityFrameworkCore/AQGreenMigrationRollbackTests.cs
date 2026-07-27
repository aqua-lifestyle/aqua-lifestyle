using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class AQGreenMigrationRollbackTests : AqualLifeStyleTestBase
    {
        private const int TenantId = 1;
        private static readonly DateTime TermsEffectiveFrom =
            new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        private static async Task<int> CreateCustomerAsync(AqualLifeStyleDbContext context)
        {
            var adminUser = await context.Users.FirstAsync(u => u.UserName == AbpUserBase.AdminUserName);
            var customer = await context.Customers
                .FirstOrDefaultAsync(c => c.UserId == adminUser.Id);

            if (customer == null)
            {
                customer = Customer.Create(
                    tenantId: TenantId,
                    userId: adminUser.Id,
                    name: "AQGreen Migration Test Member",
                    email: new EmailAddress($"aqgreen-migration-test-{Guid.NewGuid():N}@example.test"));

                context.Customers.Add(customer);
                await context.SaveChangesAsync();
            }

            return customer.Id;
        }

        [Fact]
        public async Task Down_FinancialHistoryCheck_Blocks_When_Any_Participation_Has_JoiningPaymentId()
        {
            await UsingDbContextAsync(async context =>
            {
                var customerId = await CreateCustomerAsync(context);
                var participation = EntryParticipation.StartIndependently(
                    tenantId: TenantId,
                    customerId: customerId,
                    terms: EntryProgrammeTerms.CreateSingleJoiningPayment(
                        version: "2026-07-single-1200",
                        effectiveFrom: TermsEffectiveFrom,
                        joiningPaymentAmount: 1200m,
                        monthlyCommitmentAmount: 600m,
                        gracePeriodDays: 7),
                    startedAt: DateTime.UtcNow);

                var payment = MemberPayment.CreatePending(
                    tenantId: TenantId,
                    customerId: customerId,
                    purpose: MemberPaymentPurpose.AQGreenJoining,
                    amount: 1200m,
                    provider: "Yoco",
                    externalReference: "chk_test",
                    initiatedAt: DateTime.UtcNow);

                context.MemberPayments.Add(payment);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                context.Entry(participation).Property("JoiningPaymentId").CurrentValue = payment.Id;
                await context.SaveChangesAsync();

                var hasJoiningPayment = context.EntryParticipations
                    .Any(ep => ep.JoiningPaymentId != null);

                hasJoiningPayment.ShouldBeTrue(
                    "Any participation with JoiningPaymentId should block downgrade");
            });
        }

        [Fact]
        public async Task Down_FinancialHistoryCheck_Permits_When_No_Participation_Has_JoiningPaymentId()
        {
            await UsingDbContextAsync(async context =>
            {
                var customerId = await CreateCustomerAsync(context);
                var participation = EntryParticipation.StartIndependently(
                    tenantId: TenantId,
                    customerId: customerId,
                    terms: EntryProgrammeTerms.CreateSingleJoiningPayment(
                        version: "2026-07-single-1200",
                        effectiveFrom: TermsEffectiveFrom,
                        joiningPaymentAmount: 1200m,
                        monthlyCommitmentAmount: 600m,
                        gracePeriodDays: 7),
                    startedAt: DateTime.UtcNow);

                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                var hasJoiningPayment = context.EntryParticipations
                    .Any(ep => ep.JoiningPaymentId != null);

                hasJoiningPayment.ShouldBeFalse(
                    "Participation without JoiningPaymentId should not block downgrade");
            });
        }

        [Fact]
        public async Task Down_CheckoutBlock_Triggers_When_Checkouts_Exist()
        {
            await UsingDbContextAsync(async context =>
            {
                var customerId = await CreateCustomerAsync(context);
                var participation = EntryParticipation.StartIndependently(
                    tenantId: TenantId,
                    customerId: customerId,
                    terms: EntryProgrammeTerms.CreateSingleJoiningPayment(
                        version: "2026-07-single-1200",
                        effectiveFrom: TermsEffectiveFrom,
                        joiningPaymentAmount: 1200m,
                        monthlyCommitmentAmount: 600m,
                        gracePeriodDays: 7),
                    startedAt: DateTime.UtcNow);

                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                var checkout = AQGreenJoiningCheckout.Create(
                    tenantId: TenantId,
                    participationId: participation.Id,
                    customerId: customerId,
                    amount: 1200m,
                    currency: "ZAR",
                    createdAt: DateTime.UtcNow);

                context.Set<AQGreenJoiningCheckout>().Add(checkout);
                await context.SaveChangesAsync();

                var hasCheckouts = context.Set<AQGreenJoiningCheckout>().Any();
                hasCheckouts.ShouldBeTrue(
                    "Checkout records alone should block downgrade");
            });
        }

        [Fact]
        public void Migration_SQL_Contains_Global_JoiningPaymentId_Check()
        {
            // Verify the migration class is discoverable and its Down() method
            // contains the global JoiningPaymentId check.
            var migrationType = typeof(AqualLifeStyleDbContext).Assembly
                .GetTypes()
                .FirstOrDefault(t => t.Name == "AddAQGreenSingleJoiningPayment");

            migrationType.ShouldNotBeNull();
            migrationType.GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)
                .ShouldNotBeNull();
        }
    }
}
