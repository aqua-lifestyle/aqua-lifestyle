using System;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Email;
using AqualLifeStyle.MultiTenancy;

namespace AqualLifeStyle.Authorization.Accounts
{
    public sealed class AccountEmailVerificationScheduler : ITransientDependency
    {
        private readonly UserManager _userManager;
        private readonly TenantManager _tenantManager;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;
        private readonly AccountEmailLinkBuilder _emailLinkBuilder;

        public AccountEmailVerificationScheduler(
            UserManager userManager,
            TenantManager tenantManager,
            ITransactionalEmailOutbox emailOutbox,
            TransactionalEmailTemplateBuilder emailTemplates,
            AccountEmailLinkBuilder emailLinkBuilder)
        {
            _userManager = userManager;
            _tenantManager = tenantManager;
            _emailOutbox = emailOutbox;
            _emailTemplates = emailTemplates;
            _emailLinkBuilder = emailLinkBuilder;
        }

        public async Task<bool> ScheduleAsync(
            User user,
            string idempotencyKey,
            string redirectPath = null)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (!user.TenantId.HasValue)
                throw new InvalidOperationException("Email verification requires an Area account.");

            await _userManager.InitializeOptionsAsync(user.TenantId);
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var tenant = await _tenantManager.GetByIdAsync(user.TenantId.Value);
            var url = _emailLinkBuilder.Build(
                "/verify-email",
                user.TenantId.Value,
                user.Id,
                token,
                tenant.TenancyName,
                redirectPath);
            return await _emailOutbox.EnqueueAsync(
                user.TenantId,
                "EmailVerification",
                idempotencyKey,
                _emailTemplates.VerifyEmail(user.Name, user.EmailAddress, url, idempotencyKey));
        }
    }
}
