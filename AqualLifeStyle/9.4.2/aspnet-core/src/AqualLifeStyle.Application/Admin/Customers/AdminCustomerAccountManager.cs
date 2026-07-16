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
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }

    public interface IAdminCustomerAccountManager
    {
        Task<AdminCustomerAccountResult> CreateOrRestoreAsync(AdminCustomerAccountInput input);
    }

    public class AdminCustomerAccountResult
    {
        public Customer Customer { get; set; }
        public bool WasRestored { get; set; }
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

        public async Task<AdminCustomerAccountResult> CreateOrRestoreAsync(AdminCustomerAccountInput input)
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

                    existingCustomer.IsDeleted = false;
                    existingCustomer.DeleterUserId = null;
                    existingCustomer.DeletionTime = null;
                    await _customerProfileUpdater.UpdateAsync(existingCustomer, new AdminCustomerProfileUpdate
                    {
                        FirstName = input.FirstName,
                        LastName = input.LastName,
                        Email = input.Email,
                        MembershipId = input.MembershipId,
                        IsActive = input.IsActive
                    });
                    await _userManager.InitializeOptionsAsync(input.TenantId);
                    var passwordResetToken = await _userManager.GeneratePasswordResetTokenAsync(existingCustomer.User);
                    EnsureSuccess(await _userManager.ResetPasswordAsync(
                        existingCustomer.User,
                        passwordResetToken,
                        input.Password ?? CreateTemporaryPassword()));
                    return new AdminCustomerAccountResult
                    {
                        Customer = existingCustomer,
                        WasRestored = true
                    };
                }

                if (await _userRepository.GetAll().AnyAsync(user => user.NormalizedEmailAddress == normalizedEmail))
                    throw new UserFriendlyException("Customer creation failed.", "The email address belongs to an existing sign-in account.");

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
                EnsureSuccess(await _userManager.CreateAsync(user, input.Password ?? CreateTemporaryPassword()));
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
                    Customer = customer,
                    WasRestored = false
                };
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

        private static string CreateTemporaryPassword() => $"Aa1!{Guid.NewGuid():N}";
    }
}
