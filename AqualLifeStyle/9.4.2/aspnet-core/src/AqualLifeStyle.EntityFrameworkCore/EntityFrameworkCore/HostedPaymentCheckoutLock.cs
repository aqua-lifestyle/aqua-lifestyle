using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class HostedPaymentCheckoutLock
        : IHostedPaymentCheckoutLock, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public HostedPaymentCheckoutLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public Task AcquireCheckoutAsync(Guid checkoutId) =>
            AcquireAsync($"hosted-checkout:{checkoutId:N}");

        public Task AcquireAQGreenParticipationAsync(Guid participationId) =>
            AcquireAsync($"aqgreen-checkout-creation:{participationId:N}");

        public Task AcquireDirectOnyxCustomerAsync(int customerId)
        {
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            return AcquireAsync($"direct-onyx-checkout-creation:{customerId}");
        }

        public async Task AcquireCustomerAreaTransitionsAsync(params int[] customerIds)
        {
            if (customerIds == null || customerIds.Length == 0)
                throw new ArgumentException(
                    "At least one customer identifier is required.",
                    nameof(customerIds));
            if (customerIds.Any(customerId => customerId <= 0))
                throw new ArgumentOutOfRangeException(nameof(customerIds));
            foreach (var customerId in customerIds.Distinct().OrderBy(customerId => customerId))
                await AcquireAsync($"customer-area-transition:{customerId}");
        }

        public async Task AcquireProgrammeApprovalUserSessionAsync(long userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            var context = _dbContextProvider.GetDbContext();
            var resource = $"programme-approval-user:{userId}";
            if (context.Database.IsNpgsql())
            {
                await context.Database.OpenConnectionAsync();
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_lock(hashtextextended({0}, 0))",
                    resource);
                return;
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.OpenConnectionAsync();
                await context.Database.ExecuteSqlRawAsync(
                    "DECLARE @result int; " +
                    "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Session', @LockTimeout = 10000; " +
                    "IF @result < 0 THROW 51000, 'Unable to lock programme approval user.', 1;",
                    resource);
            }
        }

        public async Task ReleaseProgrammeApprovalUserSessionAsync(long userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            var context = _dbContextProvider.GetDbContext();
            var resource = $"programme-approval-user:{userId}";
            if (context.Database.IsNpgsql())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_unlock(hashtextextended({0}, 0))",
                    resource);
                await context.Database.CloseConnectionAsync();
                return;
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_releaseapplock @Resource = {0}, @LockOwner = 'Session';",
                    resource);
                await context.Database.CloseConnectionAsync();
            }
        }

        public Task AcquireProgrammeParticipationDecisionAsync(Guid participationId)
        {
            if (participationId == Guid.Empty)
                throw new ArgumentException("A participation identifier is required.", nameof(participationId));
            return AcquireAsync($"programme-participation-decision:{participationId:N}");
        }

        private async Task AcquireAsync(string resource)
        {
            var context = _dbContextProvider.GetDbContext();
            if (context.Database.IsNpgsql())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))",
                    resource);
                return;
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DECLARE @result int; " +
                    "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Transaction', @LockTimeout = 10000; " +
                    "IF @result < 0 THROW 51000, 'Unable to lock hosted payment checkout.', 1;",
                    resource);
            }
        }
    }
}
