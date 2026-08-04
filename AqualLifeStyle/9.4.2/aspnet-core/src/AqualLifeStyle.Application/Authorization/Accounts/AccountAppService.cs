using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Abp.Configuration;
using Abp.Auditing;
using Abp.UI;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Email;
using AqualLifeStyle.Domain.Email;

namespace AqualLifeStyle.Authorization.Accounts
{
    public class AccountAppService : AqualLifeStyleAppServiceBase, IAccountAppService
    {
        public const string PasswordRegex = "(?=^.{8,}$)(?=.*\\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[!@#$%^&*()])(?!.*\\s)[0-9a-zA-Z!@#$%^&*()]*$";

        private readonly UserRegistrationManager _userRegistrationManager;
        private readonly IConfiguration _configuration;
        private readonly ICustomerRepository _customerRepository;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly IAccountEmailThrottleRepository _emailThrottleRepository;
        private readonly AccountEmailVerificationScheduler _emailVerificationScheduler;
        private readonly AccountPasswordResetScheduler _passwordResetScheduler;

        public AccountAppService(
            UserRegistrationManager userRegistrationManager,
            IConfiguration configuration,
            ICustomerRepository customerRepository,
            ITransactionalEmailOutbox emailOutbox,
            IAccountEmailThrottleRepository emailThrottleRepository,
            AccountEmailVerificationScheduler emailVerificationScheduler,
            AccountPasswordResetScheduler passwordResetScheduler)
        {
            _userRegistrationManager = userRegistrationManager;
            _configuration = configuration;
            _customerRepository = customerRepository;
            _emailOutbox = emailOutbox;
            _emailThrottleRepository = emailThrottleRepository;
            _emailVerificationScheduler = emailVerificationScheduler;
            _passwordResetScheduler = passwordResetScheduler;
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

        public async Task<GetTenantSelfRegistrationAvailabilityOutput> GetTenantSelfRegistrationAvailability(
            GetTenantSelfRegistrationAvailabilityInput input)
        {
            var tenant = await TenantManager.FindByTenancyNameAsync(input.TenancyName.Trim());
            if (tenant == null || !tenant.IsActive)
            {
                return new GetTenantSelfRegistrationAvailabilityOutput
                {
                    IsSelfRegistrationEnabled = false
                };
            }

            var isSelfRegistrationEnabled = await SettingManager.GetSettingValueForTenantAsync<bool>(
                AppSettingNames.IsSelfRegistrationEnabled,
                tenant.Id);

            return new GetTenantSelfRegistrationAvailabilityOutput
            {
                IsSelfRegistrationEnabled = isSelfRegistrationEnabled
            };
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
                false
            );
            user.UpdateContactDetails(input.ContactNumber, input.HomeAddress);

            var customerName = $"{input.Name} {input.Surname}".Trim();
            var customer = Customer.Create(
                user.TenantId,
                user.Id,
                customerName,
                new EmailAddress(input.EmailAddress),
                membershipId: null,
                user: user);
            await _customerRepository.InsertAsync(customer);
            using (CurrentUnitOfWork.SetTenantId(user.TenantId))
            {
                await _emailVerificationScheduler.ScheduleAsync(
                    user,
                    $"email-verification:{user.TenantId}:{user.Id}:registration",
                    input.RedirectPath);
            }

            return new RegisterOutput
            {
                CanLogin = false,
                RequiresEmailVerification = true
            };
        }

        [DisableAuditing]
        public async Task<bool> ConfirmEmail(ConfirmEmailInput input)
        {
            if (input == null || input.TenantId <= 0 || input.UserId <= 0 || string.IsNullOrWhiteSpace(input.Token))
                throw InvalidAccountLink("Email verification failed.");
            using (CurrentUnitOfWork.SetTenantId(input.TenantId))
            {
                await UserManager.InitializeOptionsAsync(input.TenantId);
                var user = await UserManager.FindByIdAsync(input.UserId.ToString());
                if (user == null || user.TenantId != input.TenantId || !user.IsActive || user.IsDeleted)
                    throw InvalidAccountLink("Email verification failed.");
                var result = await UserManager.ConfirmEmailAsync(user, input.Token);
                if (!result.Succeeded) throw InvalidAccountLink("Email verification failed.");
                return true;
            }
        }

        [DisableAuditing]
        public async Task<AccountEmailRequestOutput> ResendEmailVerification(RequestAccountEmailInput input)
        {
            var generic = VerificationRequestAccepted();
            var context = await FindEligibleUserAsync(input, true);
            if (context.User == null) return generic;
            using (CurrentUnitOfWork.SetTenantId(context.TenantId))
            {
                var key = $"email-verification:{context.TenantId}:{context.User.Id}:resend:{Guid.NewGuid():N}";
                var enqueued = await _emailVerificationScheduler.ScheduleAsync(
                    context.User,
                    key,
                    input.RedirectPath);
                await KeepEnqueuedEmailIfAllowedAsync(
                    "verify", context.TenantId, input.EmailAddress, key, enqueued);
            }
            return generic;
        }

        [DisableAuditing]
        public async Task<AccountEmailRequestOutput> RequestPasswordReset(RequestAccountEmailInput input)
        {
            var generic = PasswordResetRequestAccepted();
            var context = await FindEligibleUserAsync(input, false);
            if (context.User == null || !context.User.IsEmailConfirmed || context.User.RequiresPasswordReset())
                return generic;
            using (CurrentUnitOfWork.SetTenantId(context.TenantId))
            {
                var key = $"password-reset:{context.TenantId}:{context.User.Id}:{Guid.NewGuid():N}";
                var enqueued = await _passwordResetScheduler.ScheduleAsync(
                    context.User,
                    key,
                    input.RedirectPath);
                await KeepEnqueuedEmailIfAllowedAsync(
                    "reset", context.TenantId, input.EmailAddress, key, enqueued);
            }
            return generic;
        }

        [DisableAuditing]
        public async Task<bool> ResetPassword(CompletePasswordResetInput input)
        {
            if (input == null || input.TenantId <= 0 || input.UserId <= 0 || string.IsNullOrWhiteSpace(input.Token))
                throw InvalidAccountLink("Password reset failed.");
            using (CurrentUnitOfWork.SetTenantId(input.TenantId))
            {
                await UserManager.InitializeOptionsAsync(input.TenantId);
                var user = await UserManager.FindByIdAsync(input.UserId.ToString());
                if (user == null || user.TenantId != input.TenantId || !user.IsActive || user.IsDeleted ||
                    user.RequiresPasswordReset())
                    throw InvalidAccountLink("Password reset failed.");
                var result = await UserManager.ResetPasswordAsync(user, input.Token, input.NewPassword);
                if (!result.Succeeded) throw InvalidAccountLink("Password reset failed.");
                user.CompleteRequiredPasswordReset();
                CheckErrors(await UserManager.UpdateSecurityStampAsync(user));
                CheckErrors(await UserManager.UpdateAsync(user));
                return true;
            }
        }

        private async Task<(int TenantId, string AreaName, User User)> FindEligibleUserAsync(
            RequestAccountEmailInput input,
            bool requireUnconfirmed)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.AreaName) ||
                string.IsNullOrWhiteSpace(input.EmailAddress))
                return (0, null, null);
            var tenant = await TenantManager.FindByTenancyNameAsync(input.AreaName.Trim());
            if (tenant == null || !tenant.IsActive) return (0, null, null);
            using (CurrentUnitOfWork.SetTenantId(tenant.Id))
            {
                await UserManager.InitializeOptionsAsync(tenant.Id);
                var user = await UserManager.FindByEmailAsync(input.EmailAddress.Trim());
                if (user == null || !user.IsActive || user.IsDeleted || (requireUnconfirmed && user.IsEmailConfirmed))
                    return (tenant.Id, tenant.TenancyName, null);
                return (tenant.Id, tenant.TenancyName, user);
            }
        }

        private async Task KeepEnqueuedEmailIfAllowedAsync(
            string purpose,
            int tenantId,
            string email,
            string idempotencyKey,
            bool enqueued)
        {
            if (!enqueued || tenantId <= 0 || string.IsNullOrWhiteSpace(email)) return;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));
            var key = $"{purpose}:{tenantId}:{hash}";
            var now = DateTime.UtcNow;
            if (await _emailThrottleRepository.TryAcquireAsync(
                    key, tenantId, now, now.AddMinutes(30)))
            {
                return;
            }

            // Enqueue and throttle reservation share the surrounding database transaction.
            // Removing this newly inserted row prevents throttled requests from leaking mail.
            await _emailOutbox.DeleteAsync(idempotencyKey);
        }

        private static UserFriendlyException InvalidAccountLink(string title)
            => new UserFriendlyException(title, "This link is invalid or has expired. Request a new email and try again.");

        private static AccountEmailRequestOutput VerificationRequestAccepted()
            => new AccountEmailRequestOutput { Message = "If an eligible account exists, a verification email will be sent." };

        private static AccountEmailRequestOutput PasswordResetRequestAccepted()
            => new AccountEmailRequestOutput { Message = "If an eligible account exists, a password reset email will be sent." };

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
