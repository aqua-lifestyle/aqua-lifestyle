using System.Threading.Tasks;
using Abp.Configuration;
using Abp.Zero.Configuration;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Authorization.Accounts
{
    public class AccountAppService : AqualLifeStyleAppServiceBase, IAccountAppService
    {
        public const string PasswordRegex = "(?=^.{8,}$)(?=.*\\d)(?=.*[a-z])(?=.*[A-Z])(?!.*\\s)[0-9a-zA-Z!@#$%^&*()]*$";

        private readonly UserRegistrationManager _userRegistrationManager;
        private readonly IConfiguration _configuration;

        public AccountAppService(
            UserRegistrationManager userRegistrationManager,
            IConfiguration configuration)
        {
            _userRegistrationManager = userRegistrationManager;
            _configuration = configuration;
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
            if (!AbpSession.TenantId.HasValue)
            {
                var defaultTenantName = _configuration["App:DefaultTenantName"];
                if (!string.IsNullOrWhiteSpace(defaultTenantName))
                {
                    var tenant = await TenantManager.FindByTenancyNameAsync(defaultTenantName);
                    if (tenant != null && tenant.IsActive)
                    {
                        _userRegistrationManager.DefaultTenantId = tenant.Id;
                    }
                }
            }

            var user = await _userRegistrationManager.RegisterAsync(
                input.Name,
                input.Surname,
                input.EmailAddress,
                input.UserName,
                input.Password,
                true // Assumed email address is always confirmed. Change this if you want to implement email confirmation.
            );

            var isEmailConfirmationRequiredForLogin = await SettingManager.GetSettingValueAsync<bool>(AbpZeroSettingNames.UserManagement.IsEmailConfirmationRequiredForLogin);

            return new RegisterOutput
            {
                CanLogin = user.IsActive && (user.IsEmailConfirmed || !isEmailConfirmationRequiredForLogin)
            };
        }
    }
}
