using System;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenWeeklySalesEligibilityMutationLock
        : IAQGreenWeeklySalesEligibilityMutationLock, ITransientDependency
    {
        internal const string ResourcePrefix = "aqgreen-weekly-sales-eligibility:";
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenWeeklySalesEligibilityMutationLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task AcquireAsync(
            int tenantId,
            Guid participantId,
            DateTime commissionWeekStartUtc,
            string salesEligibilityRulesVersion,
            CancellationToken cancellationToken = default)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (participantId == Guid.Empty)
                throw new ArgumentException("A participation is required.", nameof(participantId));
            AQGreenCommissionWeek.FromStartUtc(commissionWeekStartUtc);
            if (!AQGreenWeeklySalesEligibilityRules.IsSupportedVersion(
                    salesEligibilityRulesVersion))
            {
                throw new AQGreenWeeklySalesEligibilityVersionNotSupportedException(
                    salesEligibilityRulesVersion);
            }

            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
                throw new NotSupportedException(
                    "AQGreen weekly-sales eligibility locking requires PostgreSQL.");
            AQGreenPlacementTreeLock.EnsureActiveTransaction(context);

            await context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))",
                new object[]
                {
                    Resource(
                        tenantId,
                        participantId,
                        commissionWeekStartUtc,
                        salesEligibilityRulesVersion)
                },
                cancellationToken);
        }

        internal static string Resource(
            int tenantId,
            Guid participantId,
            DateTime commissionWeekStartUtc,
            string salesEligibilityRulesVersion) =>
            $"{ResourcePrefix}{tenantId}:{participantId:N}:" +
            $"{commissionWeekStartUtc.Ticks}:{salesEligibilityRulesVersion}";
    }

    public sealed class AQGreenWeeklySalesEligibilityClock
        : IAQGreenWeeklySalesEligibilityClock, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenWeeklySalesEligibilityClock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<DateTime> GetUtcNowAsync(
            CancellationToken cancellationToken = default)
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
                throw new NotSupportedException(
                    "AQGreen weekly-sales eligibility time requires PostgreSQL.");
            AQGreenPlacementTreeLock.EnsureActiveTransaction(context);
            return await context.Database
                .SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
        }
    }
}
