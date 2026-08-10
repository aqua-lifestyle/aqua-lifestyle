using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using Abp.Timing;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AreaActivationStateLock
        : IAreaActivationStateLock, ITransientDependency
    {
        private const long LockNamespace = 0x4152454100000000;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AreaActivationStateLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public static long LockKey(int tenantId)
        {
            if (tenantId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenantId));
            }

            return LockNamespace | (uint)tenantId;
        }

        public async Task<DateTime> AcquireAsync(int tenantId)
        {
            var context = _dbContextProvider.GetDbContext();
            if (context.Database.IsNpgsql())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})",
                    LockKey(tenantId));
                return await ReadDatabaseUtcNowAsync(context);
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DECLARE @result int; " +
                    "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Transaction', @LockTimeout = 10000; " +
                    "IF @result < 0 THROW 51000, 'Unable to lock Area activation state.', 1;",
                    $"area-activation-state:{tenantId}");
                return await ReadDatabaseUtcNowAsync(context);
            }

            return Clock.Now.ToUniversalTime();
        }

        internal static async Task<DateTime> ReadDatabaseUtcNowAsync(
            AqualLifeStyleDbContext context)
        {
            if (context.Database.IsNpgsql())
            {
                return await context.Database
                    .SqlQueryRaw<DateTime>(
                        "SELECT clock_timestamp() AS \"Value\"")
                    .SingleAsync();
            }

            if (context.Database.IsSqlServer())
            {
                var databaseTime = await context.Database
                    .SqlQueryRaw<DateTime>(
                        "SELECT SYSUTCDATETIME() AS [Value]")
                    .SingleAsync();
                return DateTime.SpecifyKind(databaseTime, DateTimeKind.Utc);
            }

            return Clock.Now.ToUniversalTime();
        }
    }

    public sealed class AreaActivationStateClock
        : IAreaActivationStateClock, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AreaActivationStateClock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public Task<DateTime> GetUtcNowAsync()
        {
            return AreaActivationStateLock.ReadDatabaseUtcNowAsync(
                _dbContextProvider.GetDbContext());
        }
    }
}
