using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Abp.UI;
using Abp.Authorization;
using Abp.Authorization.Users;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Application.InternalAccounts;
using AqualLifeStyle.Application.InternalAccounts.Dto;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Accounts;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class InternalAccountInvitationTests : AqualLifeStyleTestBase
    {
        private readonly IAdminUserAppService _administration;
        private readonly IInternalAccountInvitationAppService _invitations;
        private readonly IAccountAppService _accounts;

        public InternalAccountInvitationTests()
        {
            _administration = Resolve<IAdminUserAppService>();
            _invitations = Resolve<IInternalAccountInvitationAppService>();
            _accounts = Resolve<IAccountAppService>();
        }

        [Fact]
        public async Task Validate_IsSideEffectFree_AndAcceptActivatesConfirmsAndIsIdempotent()
        {
            var account = await CreateInvitedAccountAsync();
            var link = await GetLatestLinkAsync(account.Email);

            var preview = await _invitations.ValidateAsync(link);
            preview.Status.ShouldBe("Pending");
            await AssertPendingUserAsync(account.Id, emailConfirmed: false);

            var accepted = await _invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
            {
                InvitationCode = link.InvitationCode,
                SetupToken = link.SetupToken,
                NewPassword = "ChosenPass1!"
            });
            accepted.WasAlreadyAccepted.ShouldBeFalse();

            var replay = await _invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
            {
                InvitationCode = link.InvitationCode,
                SetupToken = link.SetupToken,
                NewPassword = "IgnoredPass2!"
            });
            replay.WasAlreadyAccepted.ShouldBeTrue();
            replay.AreaName.ShouldBe("Default");
            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(link));

            using (UsingTenantId(1))
            {
                var manager = Resolve<UserManager>();
                await manager.InitializeOptionsAsync(1);
                var user = await manager.FindByIdAsync(account.Id.ToString());
                user.IsActive.ShouldBeTrue();
                user.IsEmailConfirmed.ShouldBeTrue();
                user.RequiresPasswordReset().ShouldBeFalse();
                (await manager.CheckPasswordAsync(user, "ChosenPass1!")).ShouldBeTrue();
            }
            await UsingDbContextAsync(1, async context =>
            {
                var invitation = await context.InternalAccountInvitations.SingleAsync(item => item.UserId == account.Id);
                invitation.Status.ShouldBe(InternalAccountInvitationStatus.Accepted);
                invitation.AcceptedAt.ShouldNotBeNull();
                invitation.EmailConfirmedAt.ShouldNotBeNull();
            });
        }

        [Fact]
        public async Task PendingInvitation_CannotSignInWithGeneratedPassword()
        {
            var account = await CreateInvitedAccountAsync();
            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == account.Id);
                user.Password = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
                    .HashPassword(user, "KnownPass1!");
            });

            var login = await Resolve<LogInManager>().LoginAsync(account.Email, "KnownPass1!", "Default");
            login.Result.ShouldBe(AbpLoginResultType.UserIsNotActive);

            await AssertPendingUserAsync(account.Id, emailConfirmed: false);
        }

        [Fact]
        public async Task PendingInvitation_PersistsOnlyProtectedEmailBodies()
        {
            var account = await CreateInvitedAccountAsync();
            var protector = Resolve<ITransactionalEmailBodyProtector>();
            await UsingDbContextAsync(1, async context =>
            {
                var message = await context.TransactionalEmailOutboxMessages.SingleAsync(item =>
                    item.NotificationType == "InternalAccountInvitation" && item.Recipient == account.Email);
                var plaintext = protector.Unprotect(message.TextBody);
                var match = Regex.Match(plaintext, @"[?&]invitation=([^#\s]+)#token=([^\s]+)");
                match.Success.ShouldBeTrue();
                message.HtmlBody.ShouldStartWith(TransactionalEmailBodyProtector.EnvelopePrefix);
                message.TextBody.ShouldStartWith(TransactionalEmailBodyProtector.EnvelopePrefix);
                message.TextBody.ShouldNotContain(match.Groups[1].Value);
                message.TextBody.ShouldNotContain(match.Groups[2].Value);
                message.TextBody.ShouldNotContain(plaintext);
            });
        }

        [Fact]
        public async Task Resend_RevokesOldLink_AndRevokeIsIdempotent()
        {
            var account = await CreateInvitedAccountAsync();
            var oldLink = await GetLatestLinkAsync(account.Email);

            await _administration.ResendInvitationAsync(new AdminUserInvitationActionInput
            {
                Id = account.Id,
                Justification = "Recipient requested another link"
            });
            var newLink = await GetLatestLinkAsync(account.Email);
            newLink.InvitationCode.ShouldNotBe(oldLink.InvitationCode);
            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(oldLink));
            (await _invitations.ValidateAsync(newLink)).Status.ShouldBe("Pending");

            var action = new AdminUserInvitationActionInput
            {
                Id = account.Id,
                Justification = "Access request withdrawn"
            };
            await _administration.RevokeInvitationAsync(action);
            await _administration.RevokeInvitationAsync(action);
            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(newLink));
        }

        [Fact]
        public async Task ExpiredLink_IsRejectedWithoutValidationMutation()
        {
            var account = await CreateInvitedAccountAsync();
            var link = await GetLatestLinkAsync(account.Email);
            await UsingDbContextAsync(1, async context =>
            {
                var invitation = await context.InternalAccountInvitations.SingleAsync(item => item.UserId == account.Id);
                typeof(InternalAccountInvitation).GetProperty(nameof(InternalAccountInvitation.ExpiresAt))!
                    .SetValue(invitation, DateTime.UtcNow.AddMinutes(-1));
            });

            await Should.ThrowAsync<UserFriendlyException>(() => _invitations.ValidateAsync(link));
            await UsingDbContextAsync(1, async context =>
            {
                var invitation = await context.InternalAccountInvitations.SingleAsync(item => item.UserId == account.Id);
                invitation.Status.ShouldBe(InternalAccountInvitationStatus.Pending);
                invitation.GetEffectiveStatus(DateTime.UtcNow).ShouldBe(InternalAccountInvitationStatus.Expired);
                invitation.EmailConfirmedAt.ShouldBeNull();
            });
        }

        [Fact]
        public async Task PasswordReset_DoesNotBypassPendingInvitation_AndWorksAfterAcceptance()
        {
            var account = await CreateInvitedAccountAsync();
            var link = await GetLatestLinkAsync(account.Email);

            await _accounts.RequestPasswordReset(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = account.Email
            });
            await UsingDbContextAsync(1, async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "PasswordReset" && message.Recipient == account.Email)).ShouldBe(0));

            await _invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
            {
                InvitationCode = link.InvitationCode,
                SetupToken = link.SetupToken,
                NewPassword = "ChosenPass1!"
            });
            await _accounts.RequestPasswordReset(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = account.Email
            });
            await UsingDbContextAsync(1, async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "PasswordReset" && message.Recipient == account.Email)).ShouldBe(1));
        }

        [Fact]
        public async Task Resend_CreatesInvitationForSetupRequiredAccountWithoutOne()
        {
            var userId = await CreateTestUserAsync(1, $"legacy-{Guid.NewGuid():N}", $"legacy-{Guid.NewGuid():N}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == userId);
                user.IsActive = false;
                user.IsEmailConfirmed = false;
                user.RequirePasswordReset();
            });

            var result = await _administration.ResendInvitationAsync(new AdminUserInvitationActionInput
            {
                Id = userId,
                Justification = "Recover setup-required legacy account"
            });
            result.InvitationStatus.ShouldBe("Pending");
            result.RequiresPasswordSetup.ShouldBeTrue();
        }

        private async Task<AdminUserDto> CreateInvitedAccountAsync()
        {
            var email = $"invite-{Guid.NewGuid():N}@example.com";
            return await _administration.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Invited",
                LastName = "Administrator",
                Email = email,
                Role = AquaUserRole.SystemAdmin,
                Justification = "Invitation lifecycle test"
            });
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

        private Task AssertPendingUserAsync(long userId, bool emailConfirmed)
            => UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == userId);
                user.IsActive.ShouldBeFalse();
                user.IsEmailConfirmed.ShouldBe(emailConfirmed);
                user.RequiresPasswordReset().ShouldBeTrue();
                var invitation = await context.InternalAccountInvitations.SingleAsync(item => item.UserId == userId);
                invitation.Status.ShouldBe(InternalAccountInvitationStatus.Pending);
                invitation.EmailConfirmedAt.ShouldBeNull();
            });
    }
}
