using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Application.InternalAccounts;
using AqualLifeStyle.Application.InternalAccounts.Dto;
using Abp;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Email;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class InternalAccountInvitationEndToEndAppTests : AqualLifeStyleTestBase
    {
        private readonly IAdminUserAppService _administration;
        private readonly IInternalAccountInvitationAppService _invitations;

        public InternalAccountInvitationEndToEndAppTests()
        {
            _administration = Resolve<IAdminUserAppService>();
            _invitations = Resolve<IInternalAccountInvitationAppService>();
        }

        [Fact]
        public async Task EndToEnd_CreateValidateAccept_ThenLoginSucceeds()
        {
            // Create invited account
            var email = $"app-e2e-invite-{Guid.NewGuid():N}@example.com";
            var created = await _administration.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "E2E",
                LastName = "Invited",
                Email = email,
                Role = AquaUserRole.SystemAdmin,
                Justification = "Application-layer E2E acceptance test"
            });

            // Extract invitation link from transactional outbox
            var link = await GetLatestLinkAsync(email);

            // Attempt to login before acceptance - should not succeed
            var preLogin = await Resolve<LogInManager>().LoginAsync(email, "ChosenPass1!", "Default");
            preLogin.Result.ShouldNotBe(AbpLoginResultType.Success);

            // Validate and accept
            var preview = await _invitations.ValidateAsync(link);
            preview.Status.ShouldBe("Pending");

            var accept = await _invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
            {
                InvitationCode = link.InvitationCode,
                SetupToken = link.SetupToken,
                NewPassword = "ChosenPass1!"
            });
            accept.WasAlreadyAccepted.ShouldBeFalse();

            // Login after acceptance should succeed
            var login = await Resolve<LogInManager>().LoginAsync(email, "ChosenPass1!", "Default");
            login.Result.ShouldBe(AbpLoginResultType.Success);
        }

        [Fact]
        public async Task PlatformAdmin_CreateAreaAdmin_AndCompleteInvitationLifecycle()
        {
            typeof(AdminCreateUserInput).GetProperty("Password").ShouldBeNull();

            var email = $"app-e2e-full-{Guid.NewGuid():N}@example.com";
            var created = await _administration.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Full",
                LastName = "Journey",
                Email = email,
                Role = AquaUserRole.SystemAdmin,
                Justification = "Full invitation lifecycle regression"
            });

            created.IsActive.ShouldBeFalse();
            created.RequiresPasswordSetup.ShouldBeTrue();
            created.InvitationStatus.ShouldBe("Pending");
            created.Role.ShouldBe(AquaUserRole.SystemAdmin);

            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == created.Id);
                user.IsActive.ShouldBeFalse();
                user.IsEmailConfirmed.ShouldBeFalse();
                user.RequiresPasswordReset().ShouldBeTrue();
                (await context.InternalAccountInvitations.CountAsync(item => item.UserId == created.Id)).ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "InternalAccountInvitation" && message.Recipient == email)).ShouldBe(1);
            });

            var link = await GetLatestLinkAsync(email);
            var preview = await _invitations.ValidateAsync(link);
            preview.Status.ShouldBe("Pending");
            preview.Username.ShouldBe(email);

            var accepted = await _invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
            {
                InvitationCode = link.InvitationCode,
                SetupToken = link.SetupToken,
                NewPassword = "ChosenPass1!"
            });
            accepted.WasAlreadyAccepted.ShouldBeFalse();

            using (UsingTenantId(1))
            {
                var userManager = Resolve<UserManager>();
                await userManager.InitializeOptionsAsync(1);
                var user = await userManager.FindByIdAsync(created.Id.ToString());
                user.IsActive.ShouldBeTrue();
                user.IsEmailConfirmed.ShouldBeTrue();
                user.RequiresPasswordReset().ShouldBeFalse();
                (await userManager.CheckPasswordAsync(user, "ChosenPass1!")).ShouldBeTrue();
            }

            var login = await Resolve<LogInManager>().LoginAsync(email, "ChosenPass1!", "Default");
            login.Result.ShouldBe(AbpLoginResultType.Success);

            var replay = await _invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
            {
                InvitationCode = link.InvitationCode,
                SetupToken = link.SetupToken,
                NewPassword = "IgnoredPass2!"
            });
            replay.WasAlreadyAccepted.ShouldBeTrue();
            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(link));

            var resendEmail = $"app-e2e-resend-{Guid.NewGuid():N}@example.com";
            var resendUser = await _administration.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Resend",
                LastName = "Journey",
                Email = resendEmail,
                Role = AquaUserRole.SystemAdmin,
                Justification = "Resend and revoke regression"
            });
            var originalResendLink = await GetLatestLinkAsync(resendEmail);
            await _administration.ResendInvitationAsync(new AdminUserInvitationActionInput
            {
                Id = resendUser.Id,
                Justification = "Issuer requested another link"
            });
            var resentLink = await GetLatestLinkAsync(resendEmail);
            resentLink.InvitationCode.ShouldNotBe(originalResendLink.InvitationCode);
            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(originalResendLink));
            (await _invitations.ValidateAsync(resentLink)).Status.ShouldBe("Pending");

            await _administration.RevokeInvitationAsync(new AdminUserInvitationActionInput
            {
                Id = resendUser.Id,
                Justification = "Access withdrawn"
            });
            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(resentLink));
        }

        [Fact]
        public async Task InvitationManagement_RejectsCrossAreaAccess()
        {
            var email = $"app-e2e-cross-{Guid.NewGuid():N}@example.com";
            var created = await _administration.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Cross",
                LastName = "Area",
                Email = email,
                Role = AquaUserRole.SystemAdmin,
                Justification = "Cross-area regression"
            });

            using (UsingTenantId(2))
            {
                await Should.ThrowAsync<AbpException>(() => _administration.ResendInvitationAsync(new AdminUserInvitationActionInput
                {
                    Id = created.Id,
                    Justification = "Attempting cross-area invitation management"
                }));
            }
        }

        private async Task<ValidateInternalAccountInvitationInput> GetLatestLinkAsync(string email)
        {
            var protectedText = await UsingDbContextAsync(1, context => context.TransactionalEmailOutboxMessages
                .Where(message => message.NotificationType == "InternalAccountInvitation" && message.Recipient == email)
                .OrderByDescending(message => message.CreationTime)
                .Select(message => message.TextBody)
                .FirstAsync());
            var text = Resolve<ITransactionalEmailBodyProtector>().Unprotect(protectedText);
            var match = Regex.Match(text, @"[?&]invitation=([^#\s]+)#token=([^\s]+)");
            match.Success.ShouldBeTrue(text);
            return new ValidateInternalAccountInvitationInput
            {
                InvitationCode = Uri.UnescapeDataString(match.Groups[1].Value),
                SetupToken = Uri.UnescapeDataString(match.Groups[2].Value)
            };
        }
    }
}
