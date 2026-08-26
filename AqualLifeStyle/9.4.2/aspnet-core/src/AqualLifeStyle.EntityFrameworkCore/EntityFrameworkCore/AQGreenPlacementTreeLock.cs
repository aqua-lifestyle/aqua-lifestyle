using System;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenPlacementTreeLock
        : IAQGreenPlacementTreeLock, ITransientDependency
    {
        internal const string ResourcePrefix = "aqgreen-placement-v2:";
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenPlacementTreeLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task AcquireAsync(
            Guid placementTreeScopeId,
            CancellationToken cancellationToken = default)
        {
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));

            var context = GetPostgreSqlContext();
            EnsureActiveTransaction(context);
            await context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))",
                new object[] { Resource(placementTreeScopeId) },
                cancellationToken);
        }

        internal static string Resource(Guid placementTreeScopeId) =>
            ResourcePrefix + placementTreeScopeId.ToString("N");

        internal static void EnsureActiveTransaction(AqualLifeStyleDbContext context)
        {
            if (context.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException(
                    "AQGreen placement allocation requires a caller-owned database transaction.");
            }
        }

        private AqualLifeStyleDbContext GetPostgreSqlContext()
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
            {
                throw new NotSupportedException(
                    "AQGreen placement-tree locking requires PostgreSQL.");
            }

            return context;
        }
    }

    public sealed class AQGreenPlacementClock
        : IAQGreenPlacementClock, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenPlacementClock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<DateTime> GetUtcNowAsync(
            CancellationToken cancellationToken = default)
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
            {
                throw new NotSupportedException(
                    "AQGreen placement time requires PostgreSQL.");
            }

            AQGreenPlacementTreeLock.EnsureActiveTransaction(context);
            return await context.Database
                .SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
        }
    }
}
