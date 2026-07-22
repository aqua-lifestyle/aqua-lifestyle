using System.Threading.Tasks;
using Abp.Configuration;
using Abp.Auditing;
using Abp.UI;
using Abp.Zero.Configuration;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Authorization.Accounts
{
    public class AccountAppService : AqualLifeStyleAppServiceBase, IAccountAppService
    {
        public const string PasswordRegex = "(?=^.{8,}$)(?=.*\\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[!@#$%^&*()])(?!.*\\s)[0-9a-zA-Z!@#$%^&*()]*$";

        private readonly UserRegistrationManager _userRegistrationManager;
        private readonly IConfiguration _configuration;
        private readonly ICustomerRepository _customerRepository;

        public AccountAppService(
            UserRegistrationManager userRegistrationManager,
            IConfiguration configuration,
            ICustomerRepository customerRepository)
        {
            _userRegistrationManager = userRegistrationManager;
            _configuration = configuration;
            _customerRepository = customerRepository;
        }

        public async Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input)
        {
            var tenant = await TenantManager.FindByTenancyNameAsync(input.TenancyName);
            if (tenant == null)
            {
                return new IsTenantAvailableOutput(TenantAvailabilityState.NotFound);
            }

            if (!tenant.IsActive)
            {
                return new IsTenantAvailableOutput(TenantAvailabilityState.InActive);
            }

            return new IsTenantAvailableOutput(TenantAvailabilityState.Available, tenant.Id);
        }

        public async Task<RegisterOutput> Register(RegisterInput input)
        {
            int? targetTenantId = AbpSession.TenantId;
            if (!targetTenantId.HasValue)
            {
                var defaultTenantName = _configuration["App:DefaultTenantName"];
                if (!string.IsNullOrWhiteSpace(defaultTenantName))
                {
                    var tenant = await TenantManager.FindByTenancyNameAsync(defaultTenantName);
                    if (tenant != null && tenant.IsActive)
                    {
                        _userRegistrationManager.DefaultTenantId = tenant.Id;
                        targetTenantId = tenant.Id;
                    }
                }
            }

            var isSelfRegistrationEnabled = targetTenantId.HasValue
                ? await SettingManager.GetSettingValueForTenantAsync<bool>(AppSettingNames.IsSelfRegistrationEnabled, targetTenantId.Value)
                : await SettingManager.GetSettingValueAsync<bool>(AppSettingNames.IsSelfRegistrationEnabled);
            if (!isSelfRegistrationEnabled)
            {
                throw new UserFriendlyException("Registration is disabled.", "Public self-registration is disabled.");
            }

            var user = await _userRegistrationManager.RegisterAsync(
                input.Name,
                input.Surname,
                input.EmailAddress,
                input.UserName,
                input.Password,
                true // Assumed email address is always confirmed. Change this if you want to implement email confirmation.
            );

            var customerName = $"{input.Name} {input.Surname}".Trim();
            var customer = Customer.Create(
                user.TenantId,
                user.Id,
                customerName,
                new EmailAddress(input.EmailAddress),
                membershipId: null,
                user: user);
            await _customerRepository.InsertAsync(customer);

            var isEmailConfirmationRequiredForLogin = await SettingManager.GetSettingValueAsync<bool>(AbpZeroSettingNames.UserManagement.IsEmailConfirmationRequiredForLogin);

            return new RegisterOutput
            {
                CanLogin = user.IsActive && (user.IsEmailConfirmed || !isEmailConfirmationRequiredForLogin)
            };
        }

        [DisableAuditing]
        public async Task<bool> CompletePasswordSetup(CompletePasswordSetupInput input)
        {
            if (input == null)
                throw new UserFriendlyException("Password setup failed.", "The request was empty.");
            var tenant = await TenantManager.FindByTenancyNameAsync(input.AreaName);
            if (tenant == null || !tenant.IsActive)
                throw new UserFriendlyException("Password setup failed.", "This password setup link is invalid or has expired.");

            using (CurrentUnitOfWork.SetTenantId(tenant.Id))
            {
                await UserManager.InitializeOptionsAsync(tenant.Id);
                var user = await UserManager.FindByIdAsync(input.UserId.ToString());
                var customer = user == null
                    ? null
                    : await _customerRepository.GetAll().SingleOrDefaultAsync(item => item.UserId == user.Id);
                if (user == null || user.TenantId != tenant.Id || customer == null)
                    throw new UserFriendlyException("Password setup failed.", "This password setup link is invalid or has expired.");

                var resetResult = await UserManager.ResetPasswordAsync(user, input.ResetToken, input.NewPassword);
                if (!resetResult.Succeeded)
                    throw new UserFriendlyException("Password setup failed.", "This password setup link is invalid or has expired. Ask an administrator for a new link.");

                user.CompleteRequiredPasswordReset();
                user.IsActive = customer.IsActive;
                CheckErrors(await UserManager.UpdateAsync(user));
                await CurrentUnitOfWork.SaveChangesAsync();
                Logger.Info($"Customer password setup completed tenant={tenant.Id} user={user.Id} customer={customer.Id}");
                return true;
            }
        }
    }
}
