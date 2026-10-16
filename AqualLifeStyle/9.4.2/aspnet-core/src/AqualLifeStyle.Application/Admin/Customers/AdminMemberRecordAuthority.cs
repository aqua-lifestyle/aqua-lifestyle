using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using Abp.Timing;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public interface IAdminMemberRecordAuthority
    {
        void RequireTransaction();
        Task RequireLiveAsync(int tenantId, int customerId);
        Task RequireRemovedAsync(int tenantId, int customerId);
    }

    // Shared administrative record guard, not an HTTP endpoint. The callers keep
    // their granular action permissions and own the complete write transaction.
    public sealed class AdminMemberRecordAuthority : IAdminMemberRecordAuthority, ITransientDependency
    {
        private readonly ICustomerRepository _customers;
        private readonly IRepository<AreaAdminAssignment, Guid> _assignments;
        private readonly IRepository<Area, Guid> _areas;
        private readonly IAbpSession _session;
        private readonly IPermissionChecker _permissions;
        private readonly IUnitOfWorkManager _unitOfWork;
        private readonly IAQGreenApprovalAuthorityStabilizer _stabilizer;

        public AdminMemberRecordAuthority(ICustomerRepository customers,
            IRepository<AreaAdminAssignment, Guid> assignments, IRepository<Area, Guid> areas,
            IAbpSession session, IPermissionChecker permissions, IUnitOfWorkManager unitOfWork,
            IAQGreenApprovalAuthorityStabilizer stabilizer)
        {
            _customers = customers; _assignments = assignments; _areas = areas;
            _session = session; _permissions = permissions; _unitOfWork = unitOfWork;
            _stabilizer = stabilizer;
        }

        public Task RequireLiveAsync(int tenantId, int customerId) => RequireAsync(tenantId, customerId, removed: false);
        public Task RequireRemovedAsync(int tenantId, int customerId) => RequireAsync(tenantId, customerId, removed: true);

        public void RequireTransaction()
        {
            if (_unitOfWork.Current?.Options.IsTransactional != true ||
                _unitOfWork.Current.Options.IsolationLevel != System.Transactions.IsolationLevel.ReadCommitted)
                throw new InvalidOperationException("Member administration requires a caller-owned READ COMMITTED transaction.");
        }

        private async Task RequireAsync(int tenantId, int customerId, bool removed)
        {
            RequireTransaction();
            var actor = _session.GetUserId();
            if (_session.TenantId.HasValue && _session.TenantId.Value != tenantId)
                throw OutsideScope();

            using (_unitOfWork.Current.DisableFilter(AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant, AbpDataFilters.SoftDelete))
            {
                await _stabilizer.LockCustomerAsync(tenantId, customerId);
                var query = _customers.GetAll().Where(c => c.Id == customerId &&
                    c.TenantId == tenantId && c.IsDeleted == removed);
                var source = await query.AsNoTracking().Select(c => new { c.MembershipId }).SingleOrDefaultAsync();
                if (source == null) throw OutsideScope();
                if (source.MembershipId.HasValue && _session.TenantId.HasValue)
                {
                    // Authorize and lock the stored source state before any
                    // restore/demotion can change its member classification.
                    if (removed) await _stabilizer.StabilizeRemovedCustomerAsync(tenantId, customerId, actor);
                    else await _stabilizer.StabilizeAsync(tenantId, customerId, actor);
                }
                var authorized = await RestrictAsync(query, _session, _permissions, _assignments, _areas);
                if (!await authorized.AsNoTracking().AnyAsync()) throw OutsideScope();
            }
        }

        internal static IQueryable<AreaAdminAssignment> ActiveAssignments(
            IRepository<AreaAdminAssignment, Guid> assignments, IRepository<Area, Guid> areas,
            int tenantId, long userId) =>
            from assignment in assignments.GetAll()
            join area in areas.GetAll()
                on new { assignment.TenantId, assignment.AreaId }
                equals new { area.TenantId, AreaId = area.Id }
            where assignment.TenantId == tenantId && assignment.UserId == userId &&
                  !assignment.IsDeleted && !assignment.RevokedAt.HasValue && !area.IsDeleted && area.IsActive
            select assignment;

        internal static async Task<IQueryable<Customer>> RestrictAsync(IQueryable<Customer> customers,
            IAbpSession session, IPermissionChecker permissions,
            IRepository<AreaAdminAssignment, Guid> assignments, IRepository<Area, Guid> areas)
        {
            if (!session.TenantId.HasValue)
            {
                // ABP's parameterless overload uses the current UOW Tenant.
                // Imports select a target Tenant for persistence; evaluate the
                // persisted permission in the actual actor's Host realm instead.
                var allTenants = await permissions.IsGrantedAsync(
                    new Abp.UserIdentifier(session.TenantId, session.GetUserId()), AquaPermissions.Admin.AllTenants);
                return allTenants ? customers : customers.Where(customer => !customer.MembershipId.HasValue);
            }
            var tenantId = session.TenantId.Value;
            var now = Clock.Now.ToUniversalTime();
            var grants = ActiveAssignments(assignments, areas, tenantId, session.GetUserId())
                .Where(assignment => assignment.EffectiveFrom <= now);
            return customers.Where(customer => customer.TenantId == tenantId &&
                (!customer.MembershipId.HasValue || (customer.AreaId.HasValue &&
                    grants.Any(assignment => assignment.AreaId == customer.AreaId.Value))));
        }

        private static AbpAuthorizationException OutsideScope() =>
            new AbpAuthorizationException("The member is outside your authorized Tenant/Area scope.");
    }
}
