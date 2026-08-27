using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenApprovalAuthorityStabilizer
        : IAQGreenApprovalAuthorityStabilizer, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenApprovalAuthorityStabilizer(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<Guid> StabilizeAsync(
            int tenantId,
            int customerId,
            long? areaAdministratorUserId,
            CancellationToken cancellationToken = default)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (areaAdministratorUserId <= 0)
                throw new ArgumentOutOfRangeException(nameof(areaAdministratorUserId));

            var context = GetReadCommittedPostgreSqlContext();
            var customer = await context.Customers
                .FromSqlInterpolated(
                    $"SELECT * FROM public.\"Customers\" WHERE \"TenantId\" = {tenantId} AND \"Id\" = {customerId} FOR UPDATE")
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (customer == null || customer.IsDeleted || !customer.AreaId.HasValue)
                throw new AQGreenPlacementConflictException(
                    "AQGreen approval requires a current participant Area assignment.");

            var areaId = customer.AreaId.Value;
            var area = await context.Areas
                .FromSqlInterpolated(
                    $"SELECT * FROM public.\"Areas\" WHERE \"TenantId\" = {tenantId} AND \"Id\" = {areaId} FOR UPDATE")
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (area == null || area.IsDeleted || !area.IsActive)
                throw new AQGreenPlacementConflictException(
                    "AQGreen approval requires an active participant Area.");

            if (areaAdministratorUserId.HasValue)
            {
                _ = await context.AreaAdminAssignments
                    .FromSqlInterpolated(
                        $"SELECT * FROM public.\"AreaAdminAssignments\" WHERE \"TenantId\" = {tenantId} AND \"AreaId\" = {areaId} AND \"UserId\" = {areaAdministratorUserId.Value} AND \"RevokedAt\" IS NULL AND NOT \"IsDeleted\" FOR UPDATE")
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(cancellationToken);
                // A missing row means the later authoritative authorization check
                // denies the command. Existing grant/revocation rows are locked so
                // authority that was present cannot disappear before commit.
            }

            return areaId;
        }

        private AqualLifeStyleDbContext GetReadCommittedPostgreSqlContext()
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
                throw new NotSupportedException(
                    "AQGreen V2 approval authority stabilization requires PostgreSQL.");
            if (context.Database.CurrentTransaction == null)
                throw new InvalidOperationException(
                    "AQGreen V2 approval requires a caller-owned database transaction.");
            if (context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel !=
                IsolationLevel.ReadCommitted)
                throw new InvalidOperationException(
                    "AQGreen V2 approval requires a READ COMMITTED transaction.");
            return context;
        }
    }

}
