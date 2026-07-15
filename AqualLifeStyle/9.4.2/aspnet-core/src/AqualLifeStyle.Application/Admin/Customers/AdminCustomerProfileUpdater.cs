using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.MultiTenancy;
using Abp.UI;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public class AdminCustomerProfileUpdate
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
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
        private readonly IMembershipRepository _membershipRepository;
        private readonly IRepository<User, long> _userRepository;
        private readonly IAdminUserRoleSynchronizer _userRoleSynchronizer;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminCustomerProfileUpdater(
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IRepository<User, long> userRepository,
            IAdminUserRoleSynchronizer userRoleSynchronizer,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _userRepository = userRepository;
            _userRoleSynchronizer = userRoleSynchronizer;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task UpdateAsync(Customer customer, AdminCustomerProfileUpdate update)
        {
            if (customer == null) throw new UserFriendlyException("Customer update failed.", "The customer was not found.");
            if (update == null) throw new UserFriendlyException("Customer update failed.", "The update was empty.");
            if (string.IsNullOrWhiteSpace(update.FirstName) || string.IsNullOrWhiteSpace(update.LastName))
                throw new UserFriendlyException("Customer update failed.", "First name and last name are required.");
            var normalizedEmail = new EmailAddress(update.Email).Value.ToUpperInvariant();
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                if (await _customerRepository.GetAll().AnyAsync(item => item.Id != customer.Id && item.Email.Value.ToUpper() == normalizedEmail))
                    throw new UserFriendlyException("Customer update failed.", "The email address is unavailable.");
            }
            if (await _userRepository.GetAll().AnyAsync(user => user.Id != customer.UserId && user.NormalizedEmailAddress == normalizedEmail))
                throw new UserFriendlyException("Customer update failed.", "The email address is unavailable.");
            if (update.MembershipId.HasValue && await _membershipRepository.FirstOrDefaultAsync(membership =>
                    membership.Id == update.MembershipId.Value &&
                    (!membership.TenantId.HasValue || membership.TenantId == customer.TenantId) && membership.IsActive) == null)
                throw new UserFriendlyException("Customer update failed.", "The selected membership is missing or inactive.");

            var user = customer.User;
            user.Name = update.FirstName.Trim(); user.Surname = update.LastName.Trim();
            user.EmailAddress = update.Email.Trim(); user.UserName = update.Email.Trim(); user.IsActive = update.IsActive;
            user.SetNormalizedNames();
            await _userRoleSynchronizer.SynchronizeAsync(user, update.MembershipId.HasValue ? AquaUserRole.Member : AquaUserRole.Guest);
            customer.Rename($"{user.Name} {user.Surname}");
            customer.ChangeEmail(new EmailAddress(update.Email));
            customer.ChangeMembership(update.MembershipId);
            if (update.IsActive) customer.Activate(); else customer.Deactivate();
            await _customerRepository.UpdateAsync(customer);
        }
    }
}
