using System;
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
