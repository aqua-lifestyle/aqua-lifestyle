using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class WeeklyCommissionPayoutMutationLock
        : IWeeklyCommissionPayoutMutationLock, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public WeeklyCommissionPayoutMutationLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public Task AcquireEntryAsync(Guid commissionId) =>
            AcquireAsync("entry", commissionId);

        public Task AcquireOnyxAsync(Guid commissionId) =>
            AcquireAsync("onyx", commissionId);

        private async Task AcquireAsync(string programme, Guid commissionId)
        {
            if (commissionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A commission identifier is required.",
                    nameof(commissionId));
            }

            var resource = $"weekly-commission-payout:{programme}:{commissionId:N}";
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
                    "IF @result < 0 THROW 51000, 'Unable to lock weekly commission payout mutation.', 1;",
                    resource);
            }
        }
    }
}
