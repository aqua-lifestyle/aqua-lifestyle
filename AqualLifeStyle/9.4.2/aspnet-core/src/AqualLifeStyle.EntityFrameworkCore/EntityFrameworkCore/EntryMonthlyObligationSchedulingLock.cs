using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    /// <summary>
    /// PostgreSQL advisory / SQL Server application lock that guarantees a single
    /// running AQGreen monthly-obligation scheduler across host instances. The
    /// lock is transaction-scoped so it is released when the scheduler's unit of
    /// work commits or rolls back. Providers without a supported lock are treated
    /// as a single-node deployment.
    /// </summary>
    public sealed class EntryMonthlyObligationSchedulingLock
        : IEntryMonthlyObligationSchedulingLock, ITransientDependency
    {
        private const long AQGreenMonthlyObligationLockKey = 0x415147524F424C;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public EntryMonthlyObligationSchedulingLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task AcquireAsync()
        {
            var context = _dbContextProvider.GetDbContext();

            if (context.Database.IsNpgsql())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})",
                    AQGreenMonthlyObligationLockKey);
                return;
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DECLARE @result int; " +
                    "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Transaction', @LockTimeout = 10000; " +
                    "IF @result < 0 THROW 51000, 'Unable to lock AQGreen monthly obligation scheduling.', 1;",
                    "aqgreen-monthly-obligation");
            }
        }
    }
}
