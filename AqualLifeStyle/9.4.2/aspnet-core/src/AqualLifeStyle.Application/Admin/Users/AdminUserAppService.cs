using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.IdentityFramework;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Application.InternalAccounts;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Accounts;
using AqualLifeStyle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Users
{
    [Audited]
    public class AdminUserAppService : AdminAppServiceBase, IAdminUserAppService
    {
        private readonly IRepository<User, long> _userRepository;
        private readonly UserManager _userManager;
        private readonly IAdminUserRoleSynchronizer _userRoleSynchronizer;
        private readonly IRepository<InternalAccountInvitation, Guid> _invitationRepository;
        private readonly InternalAccountInvitationManager _invitationManager;
        private readonly AccountPasswordResetScheduler _passwordResetScheduler;

        public AdminUserAppService(
            IRepository<User, long> userRepository,
            UserManager userManager,
            IAdminUserRoleSynchronizer userRoleSynchronizer,
            IRepository<InternalAccountInvitation, Guid> invitationRepository,
            InternalAccountInvitationManager invitationManager,
            AccountPasswordResetScheduler passwordResetScheduler)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _userRoleSynchronizer = userRoleSynchronizer;
            _invitationRepository = invitationRepository;
            _invitationManager = invitationManager;
            _passwordResetScheduler = passwordResetScheduler;
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.View)]
        public async Task<PagedResultDto<AdminUserDto>> GetAllAsync(AdminUserListInput input)
        {
            input ??= new AdminUserListInput();
            ValidateRequestedTenant(input.TenantId, "User");
            using (DisableAllTenantDataFiltersForHost())
            {
                var query = _userRepository.GetAll().Where(user => user.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue) query = query.Where(user => user.TenantId == AbpSession.TenantId.Value);
                else if (input.TenantId.HasValue) query = query.Where(user => user.TenantId == input.TenantId.Value);
                if (input.IsActive.HasValue) query = query.Where(user => user.IsActive == input.IsActive.Value);
                if (input.Role.HasValue) query = query.Where(user => user.Role == input.Role.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    query = query.Where(user => user.UserName.ToLower().Contains(keyword) ||
                        user.Name.ToLower().Contains(keyword) || user.Surname.ToLower().Contains(keyword) ||
                        user.EmailAddress.ToLower().Contains(keyword));
                }
                var total = await query.CountAsync();
                var users = await query.OrderByDescending(user => user.CreationTime)
                    .Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
                var invitations = await GetLatestInvitationsAsync(users.Select(user => user.Id));
                return new PagedResultDto<AdminUserDto>(
                    total,
                    users.Select(user => Map(
                        user,
                        invitations.TryGetValue(user.Id, out var invitation) ? invitation : null)).ToList());
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.View)]
        public async Task<AdminUserDto> GetAsync(EntityDto<long> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "User");
            var user = await GetUserAsync(input.Id);
            return Map(user, await _invitationManager.GetLatestAsync(user.Id));
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Create)]
        public async Task<AdminUserDto> CreateAsync(AdminCreateUserInput input)
        {
            if (input == null) throw Failed("User creation", "The request body was empty.");
            ValidateRole(input.Role);
            var tenantId = ResolveTargetTenant(input.TenantId, "User", "creation");
            var tenant = await TenantManager.GetByIdAsync(tenantId);
            if (!tenant.IsActive) throw Failed("User creation", "The selected tenant is inactive.");
            User user;
            InternalAccountInvitationIssueResult invitation;
            using (CurrentUnitOfWork.SetTenantId(tenantId))
            {
                await _userManager.InitializeOptionsAsync(tenantId);
                user = new User
                {
                    TenantId = tenantId,
                    UserName = input.Email.Trim(), Name = input.FirstName.Trim(), Surname = input.LastName.Trim(),
                    EmailAddress = input.Email.Trim(), IsActive = false, IsEmailConfirmed = false
                };
                user.SetRole(input.Role);
                user.SetNormalizedNames();
                (await _userManager.CreateAsync(user, CreateSystemGeneratedPassword())).CheckErrors(LocalizationManager);
                (await _userManager.SetRolesAsync(user, new[] { input.Role.ToString() })).CheckErrors(LocalizationManager);
                await CurrentUnitOfWork.SaveChangesAsync();
                invitation = await _invitationManager.CreateAsync(user, tenant, DateTime.UtcNow);
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            LogAdminMutation(
                "User",
                invitation.EmailWasQueued ? "created with invitation email queued" : "created with invitation email unavailable",
                user.Id,
                user.TenantId,
                input.Justification);
            return Map(user, invitation.Invitation);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Edit)]
        public async Task<AdminUserDto> UpdateAsync(AdminUpdateUserInput input)
        {
            if (input == null) throw Failed("User update", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            if (!input.IsActive && user.Id == AbpSession.GetUserId()) throw Failed("User update", "You cannot deactivate your own account.");
            var invitation = await _invitationManager.GetLatestAsync(user.Id);
            var invitationStatus = invitation?.GetEffectiveStatus(DateTime.UtcNow);
            if (input.IsActive && user.RequiresPasswordReset())
                throw Failed("User update", "This account cannot be activated until its owner accepts the invitation and chooses a password.");
            if (invitationStatus == InternalAccountInvitationStatus.Pending &&
                !string.Equals(user.EmailAddress, input.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw Failed("User update", "Revoke the pending invitation before changing the email address, then send a new invitation.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                user.Name = input.FirstName.Trim(); user.Surname = input.LastName.Trim();
                user.EmailAddress = input.Email.Trim(); user.UserName = input.Email.Trim(); user.IsActive = input.IsActive;
                user.SetNormalizedNames();
                (await _userManager.UpdateAsync(user)).CheckErrors(LocalizationManager);
            }
            LogAdminMutation("User", "updated", user.Id, user.TenantId, input.Justification);
            return Map(user, invitation);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.AssignRole)]
        public async Task<AdminUserDto> AssignRoleAsync(AdminAssignUserRoleInput input)
        {
            if (input == null) throw Failed("Role assignment", "The request body was empty.");
            ValidatePositiveId(input.Id, "User"); ValidateRole(input.Role);
            var user = await GetUserAsync(input.Id);
            if (user.Id == AbpSession.GetUserId() && user.Role != input.Role)
                throw Failed("Role assignment", "You cannot change your own administrator role.");
            var invitation = await _invitationManager.GetLatestAsync(user.Id);
            if (invitation?.GetEffectiveStatus(DateTime.UtcNow) == InternalAccountInvitationStatus.Pending &&
                user.Role != input.Role)
                throw Failed("Role assignment", "Revoke the pending invitation before changing this account's access level, then send a new invitation.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                await _userRoleSynchronizer.SynchronizeAsync(user, input.Role);
            }
            LogAdminMutation("User", $"assigned role {input.Role}", user.Id, user.TenantId, input.Justification);
            return Map(user, invitation);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.ResetPassword)]
        public async Task ResetPasswordAsync(AdminResetUserPasswordInput input)
        {
            if (input == null) throw Failed("Password reset", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            var invitation = await _invitationManager.GetLatestAsync(user.Id);
            if (!user.IsActive)
                throw Failed("Password reset", "Reactivate this account before sending a password reset email.");
            if (user.RequiresPasswordReset() ||
                invitation?.GetEffectiveStatus(DateTime.UtcNow) == InternalAccountInvitationStatus.Pending)
                throw Failed("Password reset", "This account has not completed setup. Resend its invitation instead.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                await _userManager.InitializeOptionsAsync(user.TenantId);
                (await _userManager.UpdateSecurityStampAsync(user)).CheckErrors(LocalizationManager);
                (await _userManager.UpdateAsync(user)).CheckErrors(LocalizationManager);
                await _passwordResetScheduler.ScheduleAsync(
                    user,
                    $"administrator-password-reset:{user.TenantId}:{user.Id}:{Guid.NewGuid():N}");
            }
            LogAdminMutation("User", "password reset email queued", user.Id, user.TenantId, input.Justification);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Invite)]
        public async Task<AdminUserDto> ResendInvitationAsync(
            AdminUserInvitationActionInput input)
        {
            if (input == null) throw Failed("Invitation resend", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            var invitation = await _invitationManager.GetLatestAsync(user.Id);
            var tenant = await TenantManager.GetByIdAsync(user.TenantId.Value);
            InternalAccountInvitationIssueResult resent;
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                if (invitation == null)
                {
                    if (!user.RequiresPasswordReset())
                        throw Failed("Invitation resend", "No invitation exists for this account.");
                    resent = await _invitationManager.CreateAsync(user, tenant, DateTime.UtcNow);
                }
                else
                {
                    resent = await _invitationManager.ResendAsync(
                        user,
                        tenant,
                        invitation,
                        AbpSession.GetUserId(),
                        DateTime.UtcNow);
                }
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            LogAdminMutation(
                "User invitation",
                resent.EmailWasQueued ? "issued with email queued" : "issued with email unavailable",
                user.Id,
                user.TenantId,
                input.Justification);
            return Map(user, resent.Invitation);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Invite)]
        public async Task<AdminUserDto> RevokeInvitationAsync(
            AdminUserInvitationActionInput input)
        {
            if (input == null) throw Failed("Invitation revocation", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            var invitation = await _invitationManager.GetLatestAsync(user.Id);
            if (invitation == null)
                throw Failed("Invitation revocation", "No invitation exists for this account.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                await _invitationManager.RevokeAsync(
                    user,
                    invitation,
                    AbpSession.GetUserId(),
                    input.Justification,
                    DateTime.UtcNow);
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            LogAdminMutation("User invitation", "revoked", user.Id, user.TenantId, input.Justification);
            return Map(user, invitation);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Delete)]
        public async Task DeleteAsync(AdminDeleteUserInput input)
        {
            if (input == null) throw Failed("User removal", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            if (user.Id == AbpSession.GetUserId()) throw Failed("User removal", "You cannot remove your own account.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                user.IsActive = false;
                (await _userManager.UpdateAsync(user)).CheckErrors(LocalizationManager);
                (await _userManager.DeleteAsync(user)).CheckErrors(LocalizationManager);
            }
            LogAdminMutation("User", "removed", user.Id, user.TenantId, input.Justification);
        }

        private async Task<User> GetUserAsync(long id)
        {
            using (DisableAllTenantDataFiltersForHost())
            {
                var query = _userRepository.GetAll().Where(user => user.Id == id && user.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue) query = query.Where(user => user.TenantId == AbpSession.TenantId.Value);
                var user = await query.SingleOrDefaultAsync();
                if (user == null) throw Failed("User lookup", "The user was not found.");
                return user;
            }
        }

        private static void ValidateRole(AquaUserRole role)
        {
            if (!Enum.IsDefined(typeof(AquaUserRole), role)) throw Failed("Role assignment", "The selected role is invalid.");
        }
        private async Task<System.Collections.Generic.Dictionary<long, InternalAccountInvitation>>
            GetLatestInvitationsAsync(System.Collections.Generic.IEnumerable<long> userIdSequence)
        {
            var userIds = userIdSequence.Distinct().ToArray();
            if (userIds.Length == 0)
                return new System.Collections.Generic.Dictionary<long, InternalAccountInvitation>();
            using (DisableTenantFilterForHost())
            {
                var invitations = await _invitationRepository.GetAll()
                    .Where(invitation => userIds.Contains(invitation.UserId))
                    .OrderByDescending(invitation => invitation.CreationTime)
                    .ToListAsync();
                return invitations
                    .GroupBy(invitation => invitation.UserId)
                    .ToDictionary(group => group.Key, group => group.First());
            }
        }

        private static string CreateSystemGeneratedPassword()
            => $"Aa1!{Guid.NewGuid():N}";

        private static AdminUserDto Map(User user, InternalAccountInvitation invitation) => new AdminUserDto
        {
            Id = user.Id, TenantId = user.TenantId, UserName = user.UserName, FirstName = user.Name,
            LastName = user.Surname, Email = user.EmailAddress, IsActive = user.IsActive, Role = user.Role,
            CreationTime = user.CreationTime,
            InvitationStatus = invitation == null
                ? null
                : invitation.GetEffectiveStatus(DateTime.UtcNow).ToString(),
            InvitationExpiresAt = invitation?.ExpiresAt,
            RequiresPasswordSetup = user.RequiresPasswordReset()
        };
    }
}
