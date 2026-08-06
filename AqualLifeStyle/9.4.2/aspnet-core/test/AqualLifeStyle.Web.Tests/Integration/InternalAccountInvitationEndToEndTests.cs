using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Application.InternalAccounts;
using AqualLifeStyle.Application.InternalAccounts.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Accounts;
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

            // Regression: the created user must be persisted in the invitation's pending state even
            // when the identity store's UpdateAsync is skipped inside a transactional UnitOfWork.
            await UsingDbContextAsync(async context =>
            {
                var createdUser = await context.Users
                    .Where(u => u.TenantId == 1 && u.EmailAddress == email && u.UserName == email)
                    .SingleAsync();
                createdUser.IsActive.ShouldBeFalse();
                createdUser.IsEmailConfirmed.ShouldBeFalse();
                createdUser.RequiresPasswordReset().ShouldBeTrue();
                createdUser.SecurityStamp.ShouldNotBeNullOrEmpty();

                var invitationExists = await context.InternalAccountInvitations
                    .AnyAsync(i => i.TenantId == 1 &&
                                   i.UserId == createdUser.Id &&
                                   i.InvitedEmailAddress == email &&
                                   i.Status == InternalAccountInvitationStatus.Pending);
                invitationExists.ShouldBeTrue();
            });

            // Extract the invitation link from transactional outbox
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

        [Fact]
        public async Task CreateAndInvite_TransactionalRollback_LeavesNoPartialAdministrator()
        {
            // Atomicity proof: the user, its role assignment, the pending invitation and the
            // transactional outbox row must all commit or roll back together. This invariant depends on
            // the UnitOfWork being transactional (the production default); the default SQLite test
            // harness runs non-transactionally, so this regression runs in the transactional (PG)
            // reproduction mode.
            if (!string.Equals(Environment.GetEnvironmentVariable("REPRO_TRANSACTIONAL"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AqualLifeStyle.Authorization.Users.User>(new Microsoft.Extensions.Options.OptionsWrapper<Microsoft.AspNetCore.Identity.PasswordHasherOptions>(new Microsoft.AspNetCore.Identity.PasswordHasherOptions()));
            UsingDbContext(context =>
            {
                var user = context.Users.Single(u => u.TenantId == 1 && u.UserName == AbpUserBase.AdminUserName);
                user.Password = passwordHasher.HashPassword(user, "123qwe");
                user.CompleteRequiredPasswordReset();
                context.SaveChanges();
            });

            LoginAsDefaultTenantAdmin();
            var administration = IocManager.Resolve<IAdminUserAppService>();
            var uowManager = IocManager.Resolve<Abp.Domain.Uow.IUnitOfWorkManager>();
            var email = $"e2e-rollback-{Guid.NewGuid():N}@example.com";

            var token = uowManager.Begin();
            try
            {
                await administration.CreateAsync(new AdminCreateUserInput
                {
                    TenantId = 1,
                    FirstName = "Rollback",
                    LastName = "Invitee",
                    Email = email,
                    Role = AquaUserRole.SystemAdmin,
                    Justification = "atomic rollback regression test"
                });
                // Simulate a downstream failure after the create+invite flushed within the
                // transaction; CompleteAsync is never called, so the UnitOfWork rolls back.
                throw new InvalidOperationException("Simulated downstream failure after create-and-invite");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                token.Dispose();
            }

            await UsingDbContextAsync(async context =>
            {
                (await context.Users.CountAsync(u => u.TenantId == 1 && u.EmailAddress == email)).ShouldBe(0);
                (await context.InternalAccountInvitations.CountAsync(i => i.TenantId == 1 && i.InvitedEmailAddress == email)).ShouldBe(0);
                (await context.TransactionalEmailOutboxMessages.CountAsync(m => m.Recipient == email)).ShouldBe(0);
            });
        }
    }
}
