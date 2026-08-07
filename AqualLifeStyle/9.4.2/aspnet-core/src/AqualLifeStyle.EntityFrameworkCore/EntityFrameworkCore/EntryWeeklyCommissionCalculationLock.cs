using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    /// <summary>
    /// PostgreSQL advisory / SQL Server application lock that guarantees a single
    /// running AQGreen weekly commission calculator across host instances. The
    /// lock is transaction-scoped so it is released when the calculation's unit of
    /// work commits or rolls back. It uses a distinct key from the monthly
    /// obligation lock so commission calculation and obligation scheduling are not
    /// serialised against each other. Providers without a supported lock are
    /// treated as a single-node deployment.
    /// </summary>
    public sealed class EntryWeeklyCommissionCalculationLock
        : IEntryWeeklyCommissionCalculationLock, ITransientDependency
    {
        public static long LockKey = AQGreenWeeklyCommissionLockKey;
        private const long AQGreenWeeklyCommissionLockKey = 0x41514757434F4D50;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public EntryWeeklyCommissionCalculationLock(
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
                    AQGreenWeeklyCommissionLockKey);
                return;
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DECLARE @result int; " +
                    "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Transaction', @LockTimeout = 10000; " +
                    "IF @result < 0 THROW 51000, 'Unable to lock AQGreen weekly commission calculation.', 1;",
                    "aqgreen-weekly-commission");
            }
        }
    }
}
