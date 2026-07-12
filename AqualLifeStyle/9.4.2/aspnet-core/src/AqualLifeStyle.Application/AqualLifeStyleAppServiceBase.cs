using System;
using System.Threading.Tasks;
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

        protected AqualLifeStyleAppServiceBase()
        {
            LocalizationSourceName = AqualLifeStyleConsts.LocalizationSourceName;
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
