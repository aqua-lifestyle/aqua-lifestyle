using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Application.InternalAccounts;
using AqualLifeStyle.Application.InternalAccounts.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Email;
using AqualLifeStyle.Models.TokenAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Abp.Authorization;
using AqualLifeStyle.Authorization;
using Shouldly;
using Xunit;
using Abp.Authorization.Users;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public class InternalAccountInvitationEndToEndTests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task InvitationAcceptance_AllowsTokenAuthentication()
        {
            // Arrange
            // Authenticate as Default tenant admin for HTTP API calls
            // Ensure the default admin has a usable password in the test DB
            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AqualLifeStyle.Authorization.Users.User>(new Microsoft.Extensions.Options.OptionsWrapper<Microsoft.AspNetCore.Identity.PasswordHasherOptions>(new Microsoft.AspNetCore.Identity.PasswordHasherOptions()));
            UsingDbContext(context =>
            {
                var user = context.Users.Single(u => u.TenantId == 1 && u.UserName == AbpUserBase.AdminUserName);
                user.Password = passwordHasher.HashPassword(user, "123qwe");
                user.CompleteRequiredPasswordReset();
                context.SaveChanges();
            });

            await AuthenticateAsync("Default", new AqualLifeStyle.Models.TokenAuth.AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });

            var email = $"e2e-invite-{Guid.NewGuid():N}@example.com";

            LoginAsDefaultTenantAdmin();
            var administration = IocManager.Resolve<IAdminUserAppService>();
            var uowManager = IocManager.Resolve<Abp.Domain.Uow.IUnitOfWorkManager>();
            using (var unitOfWork = uowManager.Begin())
            {
                await administration.CreateAsync(new AdminCreateUserInput
                {
                    TenantId = 1,
                    FirstName = "Invited",
                    LastName = "Administrator",
                    Email = email,
                    Role = AquaUserRole.SystemAdmin,
                    Justification = "Invitation lifecycle integration test"
                });
                await unitOfWork.CompleteAsync();
            }

            // Extract the latest invitation link from transactional outbox
            string invitationCode = null, setupToken = null;
            var protector = IocManager.Resolve<ITransactionalEmailBodyProtector>();
            await UsingDbContextAsync(async context =>
            {
                var protectedText = await context.TransactionalEmailOutboxMessages
                    .Where(m => m.NotificationType == "InternalAccountInvitation" && m.Recipient == email)
                    .OrderByDescending(m => m.CreationTime)
                    .Select(m => m.TextBody)
                    .FirstAsync();
                var text = protector.Unprotect(protectedText);
                var match = Regex.Match(text, @"[?&]invitation=([^#\s]+)#token=([^\s]+)");
                match.Success.ShouldBeTrue();
                invitationCode = Uri.UnescapeDataString(match.Groups[1].Value);
                setupToken = Uri.UnescapeDataString(match.Groups[2].Value);
            });

            // Attempt to authenticate before acceptance (should fail)
            var preLogin = await Resolve<LogInManager>().LoginAsync(email, "ChosenPass1!", "Default");
            preLogin.Result.ShouldNotBe(AbpLoginResultType.Success);

            // Validate (side-effect free) and accept the invitation
            using (var unitOfWork = uowManager.Begin())
            {
                var invitations = IocManager.Resolve<IInternalAccountInvitationAppService>();
                var preview = await invitations.ValidateAsync(new ValidateInternalAccountInvitationInput
                {
                    InvitationCode = invitationCode,
                    SetupToken = setupToken
                });
                preview.Status.ShouldBe("Pending");

                var acceptResult = await invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
                {
                    InvitationCode = invitationCode,
                    SetupToken = setupToken,
                    NewPassword = "ChosenPass1!"
                });
                acceptResult.WasAlreadyAccepted.ShouldBeFalse();
                await unitOfWork.CompleteAsync();
            }

            // Authenticate via TokenAuthController to obtain JWT
            var authModel = new AuthenticateModel
            {
                UserNameOrEmailAddress = email,
                Password = "ChosenPass1!"
            };
            var authResult = await AuthenticateAsync("Default", authModel);
            authResult.ShouldNotBeNull();
            authResult.AccessToken.ShouldNotBeNullOrEmpty();
            authResult.UserId.ShouldBeGreaterThan(0);

            // Replay acceptance should indicate already accepted
            using (var replayUnitOfWork = uowManager.Begin())
            {
                var invitations = IocManager.Resolve<IInternalAccountInvitationAppService>();
                var replay = await invitations.AcceptAsync(new AcceptInternalAccountInvitationInput
                {
                    InvitationCode = invitationCode,
                    SetupToken = setupToken,
                    NewPassword = "AnotherPass2!"
                });
                replay.WasAlreadyAccepted.ShouldBeTrue();
                await replayUnitOfWork.CompleteAsync();
            }
        }
    }
}
