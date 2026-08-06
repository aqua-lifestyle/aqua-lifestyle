using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Application.InternalAccounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Accounts;
using AqualLifeStyle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.InternalAccounts
{
    [Audited]
    public class InternalAccountInvitationAppService
        : AqualLifeStyleAppServiceBase, IInternalAccountInvitationAppService
    {
        private readonly IRepository<InternalAccountInvitation, Guid> _invitationRepository;

        public InternalAccountInvitationAppService(
            IRepository<InternalAccountInvitation, Guid> invitationRepository)
        {
            _invitationRepository = invitationRepository;
        }

        public async Task<InternalAccountInvitationPreviewDto> ValidateAsync(
            ValidateInternalAccountInvitationInput input)
        {
            ValidateInput(input);
            var context = await ResolveAsync(input.InvitationCode, input.SetupToken);
            if (context.Invitation.Status == InternalAccountInvitationStatus.Accepted)
                throw new UserFriendlyException(
                    "Invitation already accepted.",
                    "This one-time invitation has already been used. Continue to the normal sign-in page.");

            await UserManager.InitializeOptionsAsync(context.Area.Id);
            if (!await UserManager.VerifyUserTokenAsync(
                    context.User,
                    UserManager.Options.Tokens.PasswordResetTokenProvider,
                    "ResetPassword",
                    input.SetupToken))
                throw InvalidInvitation();

            return Map(context, "Pending");
        }

        public async Task<AcceptInternalAccountInvitationOutput> AcceptAsync(
            AcceptInternalAccountInvitationInput input)
        {
            ValidateInput(input);
            if (string.IsNullOrWhiteSpace(input.NewPassword))
                throw new UserFriendlyException("Account setup failed.", "Choose a password before continuing.");
            var context = await ResolveAsync(input.InvitationCode, input.SetupToken);
            if (context.Invitation.Status == InternalAccountInvitationStatus.Accepted)
            {
                return new AcceptInternalAccountInvitationOutput
                {
                    WasAlreadyAccepted = true,
                    AreaName = context.Area.TenancyName
                };
            }

            await UserManager.InitializeOptionsAsync(context.Area.Id);
            if (!await UserManager.VerifyUserTokenAsync(
                    context.User,
                    UserManager.Options.Tokens.PasswordResetTokenProvider,
                    "ResetPassword",
                    input.SetupToken))
                throw InvalidInvitation();

            try
            {
                var now = DateTime.UtcNow;
                using (CurrentUnitOfWork.SetTenantId(context.Area.Id))
                {
                    context.Invitation.ConfirmEmail(now);
                    context.User.IsEmailConfirmed = true;
                    var result = await UserManager.ResetPasswordAsync(
                        context.User,
                        input.SetupToken,
                        input.NewPassword);
                    if (!result.Succeeded)
                        throw new UserFriendlyException(
                            "Account setup failed.",
                            string.Join(" ", result.Errors.Select(error => error.Description)));
                    context.User.CompleteRequiredPasswordReset();
                    context.User.IsActive = true;
                    CheckErrors(await UserManager.UpdateSecurityStampAsync(context.User));
                    CheckErrors(await UserManager.UpdateAsync(context.User));
                    context.Invitation.Accept(now);
                    await CurrentUnitOfWork.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new UserFriendlyException(
                    "Account invitation changed.",
                    "The invitation was accepted, resent, or revoked by another request. Reload the link before trying again.",
                    exception);
            }
            Logger.Info(
                $"Internal account invitation accepted tenant={context.Area.Id} user={context.User.Id} invitation={context.Invitation.Id}");
            return new AcceptInternalAccountInvitationOutput
            {
                WasAlreadyAccepted = false,
                AreaName = context.Area.TenancyName
            };
        }

        private async Task<InvitationContext> ResolveAsync(string invitationCode, string setupToken)
        {
            var codeHash = InternalAccountInvitationManager.HashSecret(invitationCode?.Trim());
            InternalAccountInvitation invitation;
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                invitation = await _invitationRepository.FirstOrDefaultAsync(candidate =>
                    candidate.PublicCodeHash == codeHash);
            }
            if (invitation == null ||
                !InternalAccountInvitationManager.SecretMatches(
                    invitation.SetupTokenHash,
                    setupToken))
                throw InvalidInvitation();

            var effectiveStatus = invitation.GetEffectiveStatus(DateTime.UtcNow);
            if (effectiveStatus == InternalAccountInvitationStatus.Expired)
            {
                throw new UserFriendlyException(
                    "Invitation expired.",
                    "Ask a Platform Administrator to send a new invitation.");
            }
            if (effectiveStatus == InternalAccountInvitationStatus.Revoked)
                throw new UserFriendlyException(
                    "Invitation revoked.",
                    "Ask a Platform Administrator if you still require access.");

            var area = await TenantManager.FindByIdAsync(invitation.TenantId);
            if (area == null)
                throw InvalidInvitation();
            User user;
            using (CurrentUnitOfWork.SetTenantId(area.Id))
            {
                user = await UserManager.FindByIdAsync(invitation.UserId.ToString());
            }
            if (user == null ||
                user.TenantId != invitation.TenantId ||
                user.Role != invitation.Role ||
                !string.Equals(
                    user.EmailAddress,
                    invitation.InvitedEmailAddress,
                    StringComparison.OrdinalIgnoreCase) ||
                (!user.RequiresPasswordReset() &&
                 invitation.Status != InternalAccountInvitationStatus.Accepted))
                throw InvalidInvitation();
            return new InvitationContext(invitation, area, user);
        }

        private static void ValidateInput(ValidateInternalAccountInvitationInput input)
        {
            if (input == null ||
                string.IsNullOrWhiteSpace(input.InvitationCode) ||
                string.IsNullOrWhiteSpace(input.SetupToken))
                throw InvalidInvitation();
        }

        private static UserFriendlyException InvalidInvitation()
            => new UserFriendlyException(
                "Account invitation is invalid.",
                "Ask a Platform Administrator to send a new invitation.");

        private static InternalAccountInvitationPreviewDto Map(
            InvitationContext context,
            string status)
            => new InternalAccountInvitationPreviewDto
            {
                AreaName = context.Area.TenancyName,
                AreaDisplayName = context.Area.Name,
                InviteeName = $"{context.User.Name} {context.User.Surname}".Trim(),
                Username = context.User.EmailAddress,
                AccessLevel = AccessLevelName(context.User.Role),
                Status = status,
                ExpiresAt = context.Invitation.ExpiresAt
            };

        private static string AccessLevelName(AquaUserRole role) => role switch
        {
            AquaUserRole.SystemAdmin => "Area Administrator",
            AquaUserRole.AreaLeader => "Area Leader",
            AquaUserRole.Facilitator => "Facilitator",
            AquaUserRole.Member => "Club Member",
            AquaUserRole.Guest => "Customer",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The access level is unsupported.")
        };

        private sealed class InvitationContext
        {
            public InternalAccountInvitation Invitation { get; }
            public MultiTenancy.Tenant Area { get; }
            public User User { get; }

            public InvitationContext(
                InternalAccountInvitation invitation,
                MultiTenancy.Tenant area,
                User user)
            {
                Invitation = invitation;
                Area = area;
                User = user;
            }
        }
    }
}
