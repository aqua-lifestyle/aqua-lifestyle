using System;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.MyAccount.Dto;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.MyAccount
{
    [AbpAuthorize]
    [Audited]
    public class MyAccountAppService : AqualLifeStyleAppServiceBase, IMyAccountAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerPersonalDetailsUpdater _personalDetailsUpdater;

        public MyAccountAppService(
            ICustomerRepository customerRepository,
            ICustomerPersonalDetailsUpdater personalDetailsUpdater)
        {
            _customerRepository = customerRepository;
            _personalDetailsUpdater = personalDetailsUpdater;
        }

        public async Task<MyProfileDto> GetProfileAsync()
        {
            return Map(await GetCurrentCustomerAsync());
        }

        public async Task<MyProfileDto> UpdateProfileAsync(UpdateMyProfileInput input)
        {
            if (input == null)
                throw new UserFriendlyException("Profile update failed.", "The request was empty.");

            var customer = await GetCurrentCustomerAsync();
            await _personalDetailsUpdater.UpdateAsync(customer, new CustomerPersonalDetailsUpdate
            {
                FirstName = input.FirstName,
                LastName = input.Surname,
                Email = input.EmailAddress,
                ContactNumber = input.ContactNumber,
                HomeAddress = input.HomeAddress
            }, "Profile update");
            await _customerRepository.UpdateAsync(customer);
            await CurrentUnitOfWork.SaveChangesAsync();

            Logger.Info($"Customer profile updated tenant={customer.TenantId} user={customer.UserId} customer={customer.Id}");
            return Map(customer);
        }

        public async Task<ChangeMyPasswordResult> ChangePasswordAsync(ChangeMyPasswordInput input)
        {
            if (input == null)
            {
                throw new UserFriendlyException("Password change failed.", "The request was empty.");
            }

            if (string.Equals(input.CurrentPassword, input.NewPassword, StringComparison.Ordinal))
            {
                throw new UserFriendlyException(
                    "Password change failed.",
                    "Choose a new password that is different from your current password.");
            }

            await UserManager.InitializeOptionsAsync(AbpSession.TenantId);
            var user = await GetCurrentUserAsync();
            var isLockedOut = await UserManager.IsLockedOutAsync(user);
            if (isLockedOut)
            {
                Logger.Warn($"Failed password change attempt tenant={user.TenantId?.ToString() ?? "host"} user={user.Id} lockedOut={isLockedOut}");
                return ChangeMyPasswordResult.Failure(
                    "Your account is temporarily locked. Please try again later or contact support.");
            }

            if (!await UserManager.CheckPasswordAsync(user, input.CurrentPassword))
            {
                CheckErrors(await UserManager.AccessFailedAsync(user));

                isLockedOut = await UserManager.IsLockedOutAsync(user);
                Logger.Warn($"Failed password change attempt tenant={user.TenantId?.ToString() ?? "host"} user={user.Id} lockedOut={isLockedOut}");

                return ChangeMyPasswordResult.Failure(isLockedOut
                    ? "Your account is temporarily locked. Please try again later or contact support."
                    : "Your current password is incorrect. No changes were made.");
            }

            CheckErrors(await UserManager.ChangePasswordAsync(user, input.NewPassword));
            CheckErrors(await UserManager.UpdateSecurityStampAsync(user));
            CheckErrors(await UserManager.ResetAccessFailedCountAsync(user));
            await CurrentUnitOfWork.SaveChangesAsync();

            Logger.Info($"Account password changed tenant={user.TenantId?.ToString() ?? "host"} user={user.Id}");
            return ChangeMyPasswordResult.Success();
        }

        private async Task<Customer> GetCurrentCustomerAsync()
        {
            var userId = AbpSession.GetUserId();
            var customer = await _customerRepository.GetAllIncluding(item => item.User)
                .SingleOrDefaultAsync(item => item.UserId == userId);
            if (customer == null)
                throw new UserFriendlyException("Profile unavailable.", "A customer profile is not linked to this account.");
            return customer;
        }

        private static MyProfileDto Map(Customer customer) => new MyProfileDto
        {
            FirstName = customer.User.Name,
            Surname = customer.User.Surname,
            EmailAddress = customer.User.EmailAddress,
            ContactNumber = customer.User.PhoneNumber,
            HomeAddress = customer.User.HomeAddress
        };
    }
}
