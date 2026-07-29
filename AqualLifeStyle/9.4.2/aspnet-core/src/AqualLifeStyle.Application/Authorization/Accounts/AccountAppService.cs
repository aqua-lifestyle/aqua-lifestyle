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
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;
        private readonly IAccountEmailThrottleRepository _emailThrottleRepository;

        public AccountAppService(
            UserRegistrationManager userRegistrationManager,
            IConfiguration configuration,
            ICustomerRepository customerRepository,
            ITransactionalEmailOutbox emailOutbox,
            TransactionalEmailTemplateBuilder emailTemplates,
            IAccountEmailThrottleRepository emailThrottleRepository)
        {
            _userRegistrationManager = userRegistrationManager;
            _configuration = configuration;
            _customerRepository = customerRepository;
            _emailOutbox = emailOutbox;
            _emailTemplates = emailTemplates;
            _emailThrottleRepository = emailThrottleRepository;
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
                await UserManager.InitializeOptionsAsync(user.TenantId);
                await EnqueueVerificationAsync(
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
                if (user.IsEmailConfirmed) return true;
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
                var enqueued = await EnqueueVerificationAsync(
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
            if (context.User == null || !context.User.IsEmailConfirmed) return generic;
            using (CurrentUnitOfWork.SetTenantId(context.TenantId))
            {
                await UserManager.InitializeOptionsAsync(context.TenantId);
                var token = await UserManager.GeneratePasswordResetTokenAsync(context.User);
                var url = BuildClientUrl(
                    "/reset-password", context.TenantId, context.User.Id, token,
                    context.AreaName, input.RedirectPath);
                var key = $"password-reset:{context.TenantId}:{context.User.Id}:{Guid.NewGuid():N}";
                var enqueued = await _emailOutbox.EnqueueAsync(context.TenantId, "PasswordReset", key,
                    _emailTemplates.PasswordReset(context.User.Name, context.User.EmailAddress, url, key));
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
                if (user == null || user.TenantId != input.TenantId || !user.IsActive || user.IsDeleted)
                    throw InvalidAccountLink("Password reset failed.");
                var result = await UserManager.ResetPasswordAsync(user, input.Token, input.NewPassword);
                if (!result.Succeeded) throw InvalidAccountLink("Password reset failed.");
                user.CompleteRequiredPasswordReset();
                CheckErrors(await UserManager.UpdateSecurityStampAsync(user));
                CheckErrors(await UserManager.UpdateAsync(user));
                return true;
            }
        }

        private async Task<bool> EnqueueVerificationAsync(
            User user,
            string idempotencyKey,
            string redirectPath = null)
        {
            await UserManager.InitializeOptionsAsync(user.TenantId);
            var token = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            var tenant = await TenantManager.GetByIdAsync(user.TenantId.Value);
            var url = BuildClientUrl(
                "/verify-email", user.TenantId.Value, user.Id, token,
                tenant.TenancyName, redirectPath);
            return await _emailOutbox.EnqueueAsync(user.TenantId, "EmailVerification", idempotencyKey,
                _emailTemplates.VerifyEmail(user.Name, user.EmailAddress, url, idempotencyKey));
        }

        private string BuildClientUrl(
            string path, int tenantId, long userId, string token,
            string areaName = null, string redirectPath = null)
        {
            var root = _configuration["App:ClientRootAddress"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("The client application address is not configured.");
            var url = $"{root}{path}?tenantId={tenantId}&userId={userId}&token={Uri.EscapeDataString(token)}";
            if (!string.IsNullOrWhiteSpace(areaName))
                url += "&area=" + Uri.EscapeDataString(areaName);
            var safeRedirect = SafeClientRedirect(redirectPath);
            if (safeRedirect != null)
                url += "&redirect=" + Uri.EscapeDataString(safeRedirect);
            return url;
        }

        private static string SafeClientRedirect(string value)
        {
            var candidate = value?.Trim();
            return !string.IsNullOrWhiteSpace(candidate) &&
                   candidate.StartsWith("/", StringComparison.Ordinal) &&
                   !candidate.StartsWith("//", StringComparison.Ordinal) &&
                   candidate.IndexOf('\\') < 0 &&
                   Uri.TryCreate(candidate, UriKind.Relative, out _)
                ? candidate
                : null;
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
                    key, tenantId, now, now.AddMinutes(5)))
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
