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
        public string Password { get; set; }
        public bool AllowSystemGeneratedPassword { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }

    public interface IAdminCustomerAccountManager
    {
        Task<AdminCustomerAccountResult> CreateOrFindRemovedAsync(AdminCustomerAccountInput input);
        Task<Customer> RestoreAsync(int customerId, AdminCustomerAccountInput input);
    }

    public class AdminCustomerAccountResult
    {
        public Customer Customer { get; set; }
        public Customer RemovedCustomer { get; set; }
    }

    public class AdminCustomerAccountManager : IAdminCustomerAccountManager, ITransientDependency
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerMembershipPlanAssignmentValidator _membershipPlanAssignmentValidator;
        private readonly IAdminCustomerProfileUpdater _customerProfileUpdater;
        private readonly IRepository<User, long> _userRepository;
        private readonly UserManager _userManager;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminCustomerAccountManager(
            ICustomerRepository customerRepository,
            ICustomerMembershipPlanAssignmentValidator membershipPlanAssignmentValidator,
            IAdminCustomerProfileUpdater customerProfileUpdater,
            IRepository<User, long> userRepository,
            UserManager userManager,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _customerRepository = customerRepository;
            _membershipPlanAssignmentValidator = membershipPlanAssignmentValidator;
            _customerProfileUpdater = customerProfileUpdater;
            _userRepository = userRepository;
            _userManager = userManager;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<AdminCustomerAccountResult> CreateOrFindRemovedAsync(AdminCustomerAccountInput input)
        {
            Validate(input);
            var email = input.Email.Trim();
            var normalizedEmail = email.ToUpperInvariant();

            using (_unitOfWorkManager.Current.SetTenantId(input.TenantId))
            {
                Customer existingCustomer;
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
                {
                    existingCustomer = await _customerRepository.GetAllIncluding(customer => customer.User)
                        .FirstOrDefaultAsync(customer => customer.Email.Value.ToUpper() == normalizedEmail);
                }

                if (existingCustomer != null)
                {
                    if (!existingCustomer.IsDeleted || existingCustomer.TenantId != input.TenantId)
                        throw new UserFriendlyException("Customer creation failed.", "The email address belongs to an existing account.");
                    return new AdminCustomerAccountResult
                    {
                        RemovedCustomer = existingCustomer
                    };
                }

                if (await _userRepository.GetAll().AnyAsync(user => user.NormalizedEmailAddress == normalizedEmail))
                    throw new UserFriendlyException("Customer creation failed.", "The email address belongs to an existing sign-in account.");

                if (string.IsNullOrWhiteSpace(input.Password) && !input.AllowSystemGeneratedPassword)
                    throw new UserFriendlyException("Customer creation requires confirmation.", "Enter a temporary password for the new customer account.");

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
                EnsureSuccess(await _userManager.CreateAsync(user, input.Password ?? CreateSystemGeneratedPassword()));
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
                return new AdminCustomerAccountResult
                {
                    Customer = customer
                };
            }
        }

        public async Task<Customer> RestoreAsync(int customerId, AdminCustomerAccountInput input)
        {
            Validate(input);
            using (_unitOfWorkManager.Current.SetTenantId(input.TenantId))
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                var customer = await _customerRepository.GetAllIncluding(item => item.User)
                    .SingleOrDefaultAsync(item => item.Id == customerId && item.TenantId == input.TenantId);
                if (customer == null || !customer.IsDeleted)
                    throw new UserFriendlyException("Customer restoration failed.", "The removed customer account is no longer available to restore.");

                customer.IsDeleted = false;
                customer.DeleterUserId = null;
                customer.DeletionTime = null;
                await _customerProfileUpdater.UpdateAsync(customer, new AdminCustomerProfileUpdate
                {
                    FirstName = input.FirstName,
                    LastName = input.LastName,
                    Email = input.Email,
                    MembershipId = input.MembershipId,
                    IsActive = input.IsActive
                });
                await _userManager.InitializeOptionsAsync(input.TenantId);
                customer.User.RequirePasswordReset();
                EnsureSuccess(await _userManager.UpdateAsync(customer.User));
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
            if (input.Password != null && input.Password.Length < 8)
                throw new UserFriendlyException("Customer creation failed.", "The temporary password must contain at least 8 characters.");
        }

        private static void EnsureSuccess(IdentityResult result)
        {
            if (result.Succeeded) return;
            var message = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new UserFriendlyException("Customer creation failed.", message);
        }

        private static string CreateSystemGeneratedPassword() => $"Aa1!{Guid.NewGuid():N}";
    }
}
