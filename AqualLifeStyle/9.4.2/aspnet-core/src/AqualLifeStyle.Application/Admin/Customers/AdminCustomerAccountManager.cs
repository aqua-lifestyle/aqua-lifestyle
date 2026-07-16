using System;
using System.Linq;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public class AdminCustomerAccountInput
    {
        public int TenantId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }

    public interface IAdminCustomerAccountManager
    {
        Task<Customer> CreateAsync(AdminCustomerAccountInput input);
    }

    public class AdminCustomerAccountManager : IAdminCustomerAccountManager, ITransientDependency
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerMembershipPlanAssignmentValidator _membershipPlanAssignmentValidator;
        private readonly IRepository<User, long> _userRepository;
        private readonly UserManager _userManager;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminCustomerAccountManager(
            ICustomerRepository customerRepository,
            ICustomerMembershipPlanAssignmentValidator membershipPlanAssignmentValidator,
            IRepository<User, long> userRepository,
            UserManager userManager,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _customerRepository = customerRepository;
            _membershipPlanAssignmentValidator = membershipPlanAssignmentValidator;
            _userRepository = userRepository;
            _userManager = userManager;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<Customer> CreateAsync(AdminCustomerAccountInput input)
        {
            Validate(input);
            var email = input.Email.Trim();
            var normalizedEmail = email.ToUpperInvariant();

            using (_unitOfWorkManager.Current.SetTenantId(input.TenantId))
            {
                if (await _userRepository.GetAll().AnyAsync(user => user.NormalizedEmailAddress == normalizedEmail))
                    throw new UserFriendlyException("Customer creation failed.", "The email address is unavailable.");

                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
                {
                    if (await _customerRepository.GetAll().AnyAsync(customer => customer.Email.Value.ToUpper() == normalizedEmail))
                        throw new UserFriendlyException("Customer creation failed.", "The email address is unavailable.");
                }

                if (input.MembershipId.HasValue)
                    await _membershipPlanAssignmentValidator.EnsureAvailableForAreaAsync(
                        input.MembershipId.Value, input.TenantId, "Customer creation");

                var user = new User
                {
                    TenantId = input.TenantId,
                    UserName = email,
                    Name = input.FirstName.Trim(),
                    Surname = input.LastName.Trim(),
                    EmailAddress = email,
                    IsActive = input.IsActive,
                    IsEmailConfirmed = false
                };
                user.SetNormalizedNames();
                user.SetRole(input.MembershipId.HasValue ? AquaUserRole.Member : AquaUserRole.Guest);

                await _userManager.InitializeOptionsAsync(input.TenantId);
                EnsureSuccess(await _userManager.CreateAsync(user, CreateTemporaryPassword()));
                EnsureSuccess(await _userManager.SetRolesAsync(user, new[]
                {
                    input.MembershipId.HasValue ? "Member" : "Guest"
                }));
                await _unitOfWorkManager.Current.SaveChangesAsync();

                var customer = Customer.Create(
                    input.TenantId,
                    user.Id,
                    $"{user.Name} {user.Surname}",
                    new EmailAddress(email),
                    input.MembershipId,
                    user);
                if (!input.IsActive) customer.Deactivate();
                await _customerRepository.InsertAsync(customer);
                return customer;
            }
        }

        private static void Validate(AdminCustomerAccountInput input)
        {
            if (input == null) throw new UserFriendlyException("Customer creation failed.", "The request body was empty.");
            if (input.TenantId <= 0) throw new UserFriendlyException("Customer creation failed.", "A valid tenant is required.");
            if (string.IsNullOrWhiteSpace(input.FirstName)) throw new UserFriendlyException("Customer creation failed.", "First name is required.");
            if (string.IsNullOrWhiteSpace(input.LastName)) throw new UserFriendlyException("Customer creation failed.", "Last name is required.");
            try { _ = new EmailAddress(input.Email); }
            catch (ArgumentException)
            {
                throw new UserFriendlyException("Customer creation failed.", "A valid email address is required.");
            }
            if (input.MembershipId.HasValue && input.MembershipId <= 0)
                throw new UserFriendlyException("Customer creation failed.", "MembershipId must be positive.");
        }

        private static void EnsureSuccess(IdentityResult result)
        {
            if (result.Succeeded) return;
            var message = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new UserFriendlyException("Customer creation failed.", message);
        }

        private static string CreateTemporaryPassword() => $"Aa1!{Guid.NewGuid():N}";
    }
}
