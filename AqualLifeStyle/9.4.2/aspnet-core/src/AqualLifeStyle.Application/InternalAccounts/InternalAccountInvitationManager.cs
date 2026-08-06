using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.IdentityFramework;
using Abp.Localization;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Accounts;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Email;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Application.InternalAccounts
{
    public sealed class InternalAccountInvitationIssueResult
    {
        public InternalAccountInvitation Invitation { get; }
        public bool EmailWasQueued { get; }

        public InternalAccountInvitationIssueResult(
            InternalAccountInvitation invitation,
            bool emailWasQueued)
        {
            Invitation = invitation;
            EmailWasQueued = emailWasQueued;
        }
    }

    public class InternalAccountInvitationManager : ITransientDependency
    {
        public static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(24);

        private readonly IRepository<InternalAccountInvitation, Guid> _invitationRepository;
        private readonly UserManager _userManager;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly AccountEmailLinkBuilder _emailLinkBuilder;
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly ILocalizationManager _localizationManager;
        private readonly ILogger<InternalAccountInvitationManager> _logger;

        public InternalAccountInvitationManager(
            IRepository<InternalAccountInvitation, Guid> invitationRepository,
            UserManager userManager,
            IUnitOfWorkManager unitOfWorkManager,
            AccountEmailLinkBuilder emailLinkBuilder,
            TransactionalEmailTemplateBuilder emailTemplates,
            ITransactionalEmailOutbox emailOutbox,
            ILocalizationManager localizationManager,
            ILogger<InternalAccountInvitationManager> logger)
        {
            _invitationRepository = invitationRepository;
            _userManager = userManager;
            _unitOfWorkManager = unitOfWorkManager;
            _emailLinkBuilder = emailLinkBuilder;
            _emailTemplates = emailTemplates;
            _emailOutbox = emailOutbox;
            _localizationManager = localizationManager;
            _logger = logger;
        }

        public async Task<InternalAccountInvitationIssueResult> CreateAsync(
            User user,
            Tenant area,
            DateTime now)
        {
            var unitOfWork = BeginUnitOfWorkIfNeeded();
            var tenantScope = BeginTenantScopeIfNeeded(area?.Id);
            try
            {
                ValidateUserAndArea(user, area);
                var pending = await GetPendingAsync(user.Id);
                if (pending != null)
                {
                    pending.MarkExpired(now);
                    if (pending.Status == InternalAccountInvitationStatus.Pending)
                        throw new InvalidOperationException("This account already has a pending invitation.");
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                var result = await IssueAsync(user, area, null, now);
                await CompleteUnitOfWorkIfNeeded(unitOfWork);
                return result;
            }
            finally
            {
                tenantScope?.Dispose();
                unitOfWork?.Dispose();
            }
        }

        public async Task<InternalAccountInvitationIssueResult> ResendAsync(
            User user,
            Tenant area,
            InternalAccountInvitation previousInvitation,
            long administratorUserId,
            DateTime now)
        {
            var unitOfWork = BeginUnitOfWorkIfNeeded();
            var tenantScope = BeginTenantScopeIfNeeded(area?.Id);
            try
            {
                ValidateUserAndArea(user, area);
                if (previousInvitation == null)
                    throw new ArgumentNullException(nameof(previousInvitation));
                if (previousInvitation.UserId != user.Id || previousInvitation.TenantId != area.Id)
                    throw new InvalidOperationException("The invitation does not belong to this account.");
                previousInvitation.MarkExpired(now);
                if (previousInvitation.Status == InternalAccountInvitationStatus.Accepted)
                    throw new InvalidOperationException("An accepted invitation cannot be resent.");
                if (previousInvitation.Status == InternalAccountInvitationStatus.Pending)
                {
                    previousInvitation.Revoke(
                        now,
                        administratorUserId,
                        "Superseded by a resent invitation.");
                }
                await _unitOfWorkManager.Current.SaveChangesAsync();
                var result = await IssueAsync(user, area, previousInvitation.Id, now);
                await CompleteUnitOfWorkIfNeeded(unitOfWork);
                return result;
            }
            finally
            {
                tenantScope?.Dispose();
                unitOfWork?.Dispose();
            }
        }

        public async Task RevokeAsync(
            User user,
            InternalAccountInvitation invitation,
            long administratorUserId,
            string reason,
            DateTime now)
        {
            var unitOfWork = BeginUnitOfWorkIfNeeded();
            var tenantScope = BeginTenantScopeIfNeeded(user?.TenantId);
            try
            {
                if (user == null) throw new ArgumentNullException(nameof(user));
                if (invitation == null) throw new ArgumentNullException(nameof(invitation));
                if (invitation.UserId != user.Id || invitation.TenantId != user.TenantId)
                    throw new InvalidOperationException("The invitation does not belong to this account.");
                if (invitation.Status == InternalAccountInvitationStatus.Revoked)
                    return;
                invitation.MarkExpired(now);
                if (invitation.Status == InternalAccountInvitationStatus.Accepted)
                    throw new InvalidOperationException("An accepted invitation cannot be revoked.");
                if (invitation.Status == InternalAccountInvitationStatus.Expired)
                    throw new InvalidOperationException("An expired invitation cannot be revoked.");
                invitation.Revoke(now, administratorUserId, reason);
                await _userManager.InitializeOptionsAsync(user.TenantId);
                (await _userManager.UpdateSecurityStampAsync(user)).CheckErrors(_localizationManager);
                user.IsActive = false;
                user.RequirePasswordReset();
                (await _userManager.UpdateAsync(user)).CheckErrors(_localizationManager);
                await CompleteUnitOfWorkIfNeeded(unitOfWork);
            }
            finally
            {
                tenantScope?.Dispose();
                unitOfWork?.Dispose();
            }
        }

        public async Task<InternalAccountInvitation> GetLatestAsync(long userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            var unitOfWork = BeginUnitOfWorkIfNeeded();
            var tenantScope = BeginTenantScopeIfNeeded(null);
            try
            {
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
                {
                    return await _invitationRepository.GetAll()
                        .Where(invitation => invitation.UserId == userId)
                        .OrderByDescending(invitation => invitation.CreationTime)
                        .FirstOrDefaultAsync();
                }
            }
            finally
            {
                tenantScope?.Dispose();
                unitOfWork?.Dispose();
            }
        }

        public static string HashSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

        public static bool SecretMatches(string expectedHash, string value)
        {
            if (string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(value))
                return false;
            var actualHash = HashSecret(value);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash),
                Convert.FromHexString(actualHash));
        }

        private async Task<InternalAccountInvitationIssueResult> IssueAsync(
            User user,
            Tenant area,
            Guid? previousInvitationId,
            DateTime now)
        {
            await _userManager.InitializeOptionsAsync(area.Id);
            user.IsActive = false;
            user.IsEmailConfirmed = false;
            user.RequirePasswordReset();
            (await _userManager.UpdateSecurityStampAsync(user)).CheckErrors(_localizationManager);
            (await _userManager.UpdateAsync(user)).CheckErrors(_localizationManager);

            var setupToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var publicCode = GeneratePublicCode();
            var invitation = InternalAccountInvitation.Create(
                area.Id,
                user.Id,
                user.Role,
                user.EmailAddress,
                HashSecret(publicCode),
                HashSecret(setupToken),
                now,
                now.Add(InvitationLifetime),
                previousInvitationId);
            await _invitationRepository.InsertAsync(invitation);

            var setupUrl = _emailLinkBuilder.BuildInternalAccountInvitation(publicCode, setupToken);
            var signInUrl = _emailLinkBuilder.BuildSignIn(area.TenancyName);
            var idempotencyKey = $"internal-account-invitation:{invitation.Id}";
            var queued = await _emailOutbox.EnqueueAsync(
                area.Id,
                "InternalAccountInvitation",
                idempotencyKey,
                _emailTemplates.InternalAccountInvitation(
                    $"{user.Name} {user.Surname}".Trim(),
                    user.EmailAddress,
                    area.Name,
                    AccessLevelName(user.Role),
                    setupUrl,
                    signInUrl,
                    invitation.ExpiresAt,
                    idempotencyKey));
            _logger.LogInformation(
                "Internal account invitation issued TenantId={TenantId} UserId={UserId} InvitationId={InvitationId} EmailQueued={EmailQueued}",
                area.Id,
                user.Id,
                invitation.Id,
                queued);
            return new InternalAccountInvitationIssueResult(invitation, queued);
        }

        private async Task<InternalAccountInvitation> GetPendingAsync(long userId)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                return await _invitationRepository.FirstOrDefaultAsync(invitation =>
                    invitation.UserId == userId &&
                    invitation.Status == InternalAccountInvitationStatus.Pending);
            }
        }

        private IUnitOfWorkCompleteHandle BeginUnitOfWorkIfNeeded()
            => _unitOfWorkManager.Current == null ? _unitOfWorkManager.Begin() : null;

        private IDisposable BeginTenantScopeIfNeeded(int? tenantId)
        {
            if (!tenantId.HasValue || _unitOfWorkManager.Current == null)
                return null;

            return _unitOfWorkManager.Current.SetTenantId(tenantId.Value);
        }

        private static async Task CompleteUnitOfWorkIfNeeded(IUnitOfWorkCompleteHandle unitOfWork)
        {
            if (unitOfWork != null)
                await unitOfWork.CompleteAsync();
        }

        private static string GeneratePublicCode()
        {
            var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            return value.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string AccessLevelName(AquaUserRole role) => role switch
        {
            AquaUserRole.SystemAdmin => "Area Administrator",
            AquaUserRole.AreaLeader => "Area Leader",
            AquaUserRole.Facilitator => "Facilitator",
            AquaUserRole.Member => "Club Member",
            AquaUserRole.Guest => "Customer",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The access level is unsupported.")
        };

        private static void ValidateUserAndArea(User user, Tenant area)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (user.TenantId != area.Id)
                throw new InvalidOperationException("The account does not belong to the selected Area.");
        }
    }
}
