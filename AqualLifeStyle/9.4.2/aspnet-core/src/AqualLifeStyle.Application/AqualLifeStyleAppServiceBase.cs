using System;
using System.Threading.Tasks;
using System.Linq;
using Abp.Domain.Repositories;
using Abp.Timing;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Areas;
using Microsoft.AspNetCore.Identity;
using Abp.Application.Services;
using Abp.IdentityFramework;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.MultiTenancy;

namespace AqualLifeStyle
{
    /// <summary>
    /// Derive your application services from this class.
    /// </summary>
    public abstract class AqualLifeStyleAppServiceBase : ApplicationService
    {
        public TenantManager TenantManager { get; set; }

        public UserManager UserManager { get; set; }

        public AqualLifeStyle.Domain.AQGreen.IAQGreenPilotMode PilotMode { get; set; }
            = AqualLifeStyle.Domain.AQGreen.DisabledAQGreenPilotMode.Instance;

        protected AqualLifeStyleAppServiceBase()
        {
            LocalizationSourceName = AqualLifeStyleConsts.LocalizationSourceName;
        }

        // Shared persisted assignment predicate; a Tenant/role or client filter
        // never substitutes for active resource-Area administration authority.
        protected IQueryable<AreaAdminAssignment> ActiveAreaAdministratorAssignments(
            IRepository<AreaAdminAssignment, Guid> assignments,
            IRepository<Area, Guid> areas)
        {
            var tenantId = AbpSession.GetTenantId();
            var userId = AbpSession.GetUserId();
            return from assignment in assignments.GetAll()
                join area in areas.GetAll()
                    on new { assignment.TenantId, assignment.AreaId }
                    equals new { area.TenantId, AreaId = area.Id }
                where assignment.TenantId == tenantId && assignment.UserId == userId &&
                      !assignment.IsDeleted && !assignment.RevokedAt.HasValue &&
                      !area.IsDeleted && area.IsActive
                select assignment;
        }

        // Existing customer-account routes also carry member records. Preserve
        // non-member account authority while applying the same member Area rule.
        protected async Task<IQueryable<Customer>> RestrictMemberRecordsAsync(
            IQueryable<Customer> customers,
            IRepository<AreaAdminAssignment, Guid> assignments,
            IRepository<Area, Guid> areas)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                var allTenants = await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants);
                return allTenants ? customers : customers.Where(customer => !customer.MembershipId.HasValue);
            }
            var tenantId = AbpSession.TenantId.Value;
            var now = Clock.Now.ToUniversalTime();
            var grants = ActiveAreaAdministratorAssignments(assignments, areas)
                .Where(assignment => assignment.EffectiveFrom <= now);
            return customers.Where(customer => customer.TenantId == tenantId &&
                (!customer.MembershipId.HasValue || (customer.AreaId.HasValue &&
                    grants.Any(assignment => assignment.AreaId == customer.AreaId.Value))));
        }

        protected virtual async Task<User> GetCurrentUserAsync()
        {
            var user = await UserManager.FindByIdAsync(AbpSession.GetUserId().ToString());
            if (user == null)
            {
                throw new Exception("There is no current user!");
            }

            return user;
        }

        protected virtual Task<Tenant> GetCurrentTenantAsync()
        {
            return TenantManager.GetByIdAsync(AbpSession.GetTenantId());
        }

        protected virtual void CheckErrors(IdentityResult identityResult)
        {
            identityResult.CheckErrors(LocalizationManager);
        }

        protected int GetRequiredTenantId(string operation)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                throw CreateMissingTenantContextException(operation);
            }

            return AbpSession.TenantId.Value;
        }

        protected virtual Exception CreateMissingTenantContextException(string operation)
        {
            return new UserFriendlyException(operation, "A tenant context is required.");
        }

        protected Task AssertCurrentUserOwnsCustomerAsync(Customer customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (!AbpSession.UserId.HasValue)
            {
                throw new UserFriendlyException("Authorization failed.", "A user context is required.");
            }

            if (customer.UserId != AbpSession.UserId.Value)
            {
                throw new UserFriendlyException("Authorization failed.", "You do not have permission to access this customer.");
            }

            if (!TenantIdsMatch(customer.TenantId, AbpSession.TenantId))
            {
                throw new UserFriendlyException("Authorization failed.", "You do not have permission to access this customer in another tenant.");
            }

            return Task.CompletedTask;
        }

        protected Task<bool> CurrentUserCanAccessCustomerAsync(Customer customer)
        {
            if (customer == null)
            {
                return Task.FromResult(false);
            }

            if (!AbpSession.UserId.HasValue)
            {
                return Task.FromResult(false);
            }

            if (customer.UserId != AbpSession.UserId.Value)
            {
                return Task.FromResult(false);
            }

            if (!TenantIdsMatch(customer.TenantId, AbpSession.TenantId))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        private static bool TenantIdsMatch(int? customerTenantId, int? sessionTenantId)
        {
            return customerTenantId == sessionTenantId;
        }
    }
}
