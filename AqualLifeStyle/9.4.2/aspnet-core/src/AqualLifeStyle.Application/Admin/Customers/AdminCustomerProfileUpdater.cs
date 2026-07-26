using System.Threading.Tasks;
using Abp.Dependency;
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

        public AdminCustomerProfileUpdater(
            ICustomerRepository customerRepository,
            ICustomerPersonalDetailsUpdater personalDetailsUpdater,
            ICustomerMembershipPlanAssignmentValidator membershipPlanAssignmentValidator,
            IAdminUserRoleSynchronizer userRoleSynchronizer)
        {
            _customerRepository = customerRepository;
            _personalDetailsUpdater = personalDetailsUpdater;
            _membershipPlanAssignmentValidator = membershipPlanAssignmentValidator;
            _userRoleSynchronizer = userRoleSynchronizer;
        }

        public async Task UpdateAsync(Customer customer, AdminCustomerProfileUpdate update)
        {
            if (customer == null) throw new UserFriendlyException("Customer update failed.", "The customer was not found.");
            if (update == null) throw new UserFriendlyException("Customer update failed.", "The update was empty.");
            if (update.MembershipId.HasValue)
                await _membershipPlanAssignmentValidator.EnsureAvailableForAreaAsync(
                    update.MembershipId.Value, customer.TenantId.Value, "Customer update");

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
            await _userRoleSynchronizer.SynchronizeAsync(user, update.MembershipId.HasValue ? AquaUserRole.Member : AquaUserRole.Guest);
            customer.ChangeMembership(update.MembershipId);
            if (update.IsActive) customer.Activate(); else customer.Deactivate();
            await _customerRepository.UpdateAsync(customer);
        }
    }
}
