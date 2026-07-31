using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.IdentityFramework;
using Abp.Localization;
using Abp.UI;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public class AdminCustomerProfileUpdate
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string HomeAddress { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }

    public interface IAdminCustomerProfileUpdater
    {
        Task UpdateAsync(Customer customer, AdminCustomerProfileUpdate update);
    }

    public class AdminCustomerProfileUpdater : IAdminCustomerProfileUpdater, ITransientDependency
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerPersonalDetailsUpdater _personalDetailsUpdater;
        private readonly ICustomerMembershipPlanAssignmentValidator _membershipPlanAssignmentValidator;
        private readonly IAdminUserRoleSynchronizer _userRoleSynchronizer;
        private readonly ICustomerFallbackRoleResolver _fallbackRoleResolver;
        private readonly UserManager _userManager;
        private readonly ILocalizationManager _localizationManager;

        public AdminCustomerProfileUpdater(
            ICustomerRepository customerRepository,
            ICustomerPersonalDetailsUpdater personalDetailsUpdater,
            ICustomerMembershipPlanAssignmentValidator membershipPlanAssignmentValidator,
            IAdminUserRoleSynchronizer userRoleSynchronizer,
            ICustomerFallbackRoleResolver fallbackRoleResolver,
            UserManager userManager,
            ILocalizationManager localizationManager)
        {
            _customerRepository = customerRepository;
            _personalDetailsUpdater = personalDetailsUpdater;
            _membershipPlanAssignmentValidator = membershipPlanAssignmentValidator;
            _userRoleSynchronizer = userRoleSynchronizer;
            _fallbackRoleResolver = fallbackRoleResolver;
            _userManager = userManager;
            _localizationManager = localizationManager;
        }

        public async Task UpdateAsync(Customer customer, AdminCustomerProfileUpdate update)
        {
            if (customer == null) throw new UserFriendlyException("Customer update failed.", "The customer was not found.");
            if (update == null) throw new UserFriendlyException("Customer update failed.", "The update was empty.");
            if (update.MembershipId.HasValue)
                await _membershipPlanAssignmentValidator.EnsureAvailableForAreaAsync(
                    update.MembershipId.Value, customer.TenantId.Value, "Customer update");

            var emailChanged = !string.Equals(
                customer.User.EmailAddress,
                update.Email?.Trim(),
                StringComparison.OrdinalIgnoreCase);
            var activeChanged = customer.User.IsActive != update.IsActive;
            await _personalDetailsUpdater.UpdateAsync(customer, new CustomerPersonalDetailsUpdate
            {
                FirstName = update.FirstName,
                LastName = update.LastName,
                Email = update.Email,
                ContactNumber = update.ContactNumber,
                HomeAddress = update.HomeAddress
            }, "Customer update");

            var user = customer.User;
            user.IsActive = update.IsActive;
            if (emailChanged || activeChanged)
            {
                if (emailChanged) user.IsEmailConfirmed = false;
                (await _userManager.UpdateSecurityStampAsync(user)).CheckErrors(_localizationManager);
            }
            customer.ChangeMembership(update.MembershipId);
            if (user.Role == AquaUserRole.Guest || user.Role == AquaUserRole.Member)
                await _userRoleSynchronizer.SynchronizeAsync(
                    user,
                    await _fallbackRoleResolver.ResolveAsync(customer));
            if (update.IsActive) customer.Activate(); else customer.Deactivate();
            await _customerRepository.UpdateAsync(customer);
        }
    }
}
