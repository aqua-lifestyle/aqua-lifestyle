using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Authorization.Roles;
using Abp.Configuration;
using Abp.UI;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using AqualLifeStyle.Authorization.Users;

namespace AqualLifeStyle.Tests.Application
{
    public class AccountRegistrationTests : AqualLifeStyleTestBase
    {
        private readonly IAccountAppService _accountAppService;
        private readonly ICustomerAppService _customerAppService;
        private readonly IMembershipAppService _membershipAppService;

        public AccountRegistrationTests()
        {
            _accountAppService = Resolve<IAccountAppService>();
            _customerAppService = Resolve<ICustomerAppService>();
            _membershipAppService = Resolve<IMembershipAppService>();
        }

        [Fact]
        public async Task PublicRegistration_ProvisionsGuestAccessInTheDefaultArea()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"public_customer_{suffix}@test.com";
            var userName = $"public_customer_{suffix}";
            await Resolve<ISettingManager>().ChangeSettingForTenantAsync(
                1,
                "Abp.Account.IsSelfRegistrationEnabled",
                "true");

            using (UsingTenantId(null))
            {
                var result = await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    ContactNumber = "+27 71 234 5678",
                    HomeAddress = "10 Aqua Street, Johannesburg",
                    Name = "Public",
                    Password = "Customer!101",
                    RedirectPath = "/i/AQ7G2X9K",
                    Surname = "Customer",
                    UserName = userName
                });

                result.CanLogin.ShouldBeFalse();
                result.RequiresEmailVerification.ShouldBeTrue();
            }

            var userId = await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(
                    item => item.UserName == userName);
                user.PhoneNumber.ShouldBe("+27 71 234 5678");
                user.HomeAddress.ShouldBe("10 Aqua Street, Johannesburg");
                user.IsEmailConfirmed.ShouldBeFalse();
                var verification = await context.TransactionalEmailOutboxMessages.SingleAsync(message =>
                    message.NotificationType == "EmailVerification" && message.Recipient == email);
                verification.HtmlBody.ShouldContain("redirect=%2Fi%2FAQ7G2X9K");
                var roleNames = await (
                    from userRole in context.UserRoles
                    join role in context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name).ToListAsync();

                roleNames.ShouldContain("Guest");
                return user.Id;
            });

            SetCurrentUser(userId, 1);
            (await _customerAppService.GetMyCustomerAsync()).Email.ShouldBe(email);
            (await _membershipAppService.GetActiveTiersAsync()).ShouldNotBeEmpty();

            var participations = await Resolve<
                IClubMemberProgrammeParticipationAppService>()
                .GetMyParticipationsAsync();
            participations.CanJoinEntry.ShouldBeTrue();
            participations.CanJoinOnyxDirectly.ShouldBeTrue();
        }

        [Fact]
        public async Task Register_ProvisionsCustomerWithGuestAccess()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"customer_{suffix}@test.com";
            var userName = $"customer_{suffix}";

            using (UsingTenantId(1))
            {
                var settingManager = Resolve<ISettingManager>();
                await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "true");

                var result = await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    ContactNumber = "+27 72 345 6789",
                    HomeAddress = "20 Club Road, Johannesburg",
                    Name = "New",
                    Password = "Customer!101",
                    RedirectPath = "//evil.example.test",
                    Surname = "Customer",
                    UserName = userName
                });

                result.CanLogin.ShouldBeFalse();
                result.RequiresEmailVerification.ShouldBeTrue();

                await UsingDbContextAsync(async context =>
                {
                    var user = await context.Users.SingleAsync(item => item.UserName == userName);
                    user.PhoneNumber.ShouldBe("+27 72 345 6789");
                    user.HomeAddress.ShouldBe("20 Club Road, Johannesburg");
                    user.IsEmailConfirmed.ShouldBeFalse();
                    (await context.TransactionalEmailOutboxMessages.SingleAsync(message =>
                        message.NotificationType == "EmailVerification" && message.Recipient == email))
                        .HtmlBody.ShouldNotContain("evil.example.test");
                    var customer = await context.Customers.SingleAsync(item => item.UserId == user.Id);
                    var roleNames = await (
                        from userRole in context.UserRoles
                        join role in context.Roles on userRole.RoleId equals role.Id
                        where userRole.UserId == user.Id
                        select role.Name).ToListAsync();
                    var guestRole = await context.Roles.SingleAsync(role => role.TenantId == 1 && role.Name == "Guest");
                    var guestPermissions = await context.Permissions
                        .OfType<RolePermissionSetting>()
                        .Where(permission => permission.TenantId == 1 && permission.RoleId == guestRole.Id && permission.IsGranted)
                        .Select(permission => permission.Name)
                        .ToListAsync();

                    customer.Email.Value.ShouldBe(email);
                    customer.MembershipId.ShouldBeNull();
                    roleNames.ShouldContain("Guest");
                    guestPermissions.ShouldContain(PermissionNames.Pages_Products);
                    guestPermissions.ShouldContain(AquaPermissions.Members.ViewSelf);
                    guestPermissions.ShouldContain(AquaPermissions.Memberships.ViewSelf);
                    guestPermissions.ShouldContain(AquaPermissions.ProgrammeParticipations.ViewSelf);
                    guestPermissions.ShouldContain(AquaPermissions.ProgrammeParticipations.Join);
                    guestPermissions.ShouldContain(AquaPermissions.Orders.Place);
                    guestPermissions.ShouldNotContain(PermissionNames.Pages_Customers);
                    guestPermissions.ShouldNotContain(PermissionNames.Pages_Memberships);

                    SetCurrentUser(user.Id, user.TenantId);
                });

                var currentCustomer = await _customerAppService.GetMyCustomerAsync();
                currentCustomer.Email.ShouldBe(email);
                (await _membershipAppService.GetActiveTiersAsync()).ShouldNotBeEmpty();
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _customerAppService.GetAllAsync());
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _membershipAppService.GetAllAsync());
            }
        }

        [Fact]
        public async Task EmailVerification_ConfirmsTheCorrectAreaUser()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"verify_{suffix}@test.com";
            long userId;
            string token;
            using (UsingTenantId(1))
            {
                await Resolve<ISettingManager>().ChangeSettingForTenantAsync(
                    1, "Abp.Account.IsSelfRegistrationEnabled", "true");
                var output = await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    ContactNumber = "+27 72 111 2233",
                    HomeAddress = "30 Verification Road, Johannesburg",
                    Name = "Verify",
                    Password = "Customer!101",
                    Surname = "Member",
                    UserName = $"verify_{suffix}"
                });
                output.RequiresEmailVerification.ShouldBeTrue();

                var manager = Resolve<UserManager>();
                await manager.InitializeOptionsAsync(1);
                var user = await manager.FindByEmailAsync(email);
                userId = user.Id;
                token = await manager.GenerateEmailConfirmationTokenAsync(user);
            }

            (await _accountAppService.ConfirmEmail(new ConfirmEmailInput
            {
                TenantId = 1,
                UserId = userId,
                Token = token
            })).ShouldBeTrue();

            (await _accountAppService.ConfirmEmail(new ConfirmEmailInput
            {
                TenantId = 1,
                UserId = userId,
                Token = token
            })).ShouldBeTrue();

            await UsingDbContextAsync(1, async context =>
                (await context.Users.SingleAsync(user => user.Id == userId))
                    .IsEmailConfirmed.ShouldBeTrue());
        }

        [Fact]
        public async Task EmailVerification_InvalidTokenFailsWithoutConfirmingUser()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"invalid_verify_{suffix}@test.com";
            long userId;
            using (UsingTenantId(1))
            {
                await Resolve<ISettingManager>().ChangeSettingForTenantAsync(
                    1, "Abp.Account.IsSelfRegistrationEnabled", "true");
                await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    ContactNumber = "+27 72 111 3344",
                    HomeAddress = "31 Verification Road, Johannesburg",
                    Name = "Invalid",
                    Password = "Customer!101",
                    Surname = "Token",
                    UserName = $"invalid_verify_{suffix}"
                });
                var user = await Resolve<UserManager>().FindByEmailAsync(email);
                userId = user.Id;
            }

            await Should.ThrowAsync<UserFriendlyException>(() =>
                _accountAppService.ConfirmEmail(new ConfirmEmailInput
                {
                    TenantId = 1,
                    UserId = userId,
                    Token = "invalid-token"
                }));

            await UsingDbContextAsync(1, async context =>
                (await context.Users.SingleAsync(user => user.Id == userId))
                    .IsEmailConfirmed.ShouldBeFalse());
        }

        [Fact]
        public async Task ResendVerification_IsGenericAndThrottled()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"resend_{suffix}@test.com";
            using (UsingTenantId(1))
            {
                await Resolve<ISettingManager>().ChangeSettingForTenantAsync(
                    1, "Abp.Account.IsSelfRegistrationEnabled", "true");
                await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    ContactNumber = "+27 72 111 4455",
                    HomeAddress = "32 Verification Road, Johannesburg",
                    Name = "Resend",
                    Password = "Customer!101",
                    Surname = "Member",
                    UserName = $"resend_{suffix}"
                });
            }

            var first = await _accountAppService.ResendEmailVerification(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = email
            });
            var repeated = await _accountAppService.ResendEmailVerification(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = email
            });
            var missing = await _accountAppService.ResendEmailVerification(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = $"missing_{suffix}@test.com"
            });

            first.Message.ShouldBe(repeated.Message);
            first.Message.ShouldBe(missing.Message);
            await UsingDbContextAsync(1, async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "EmailVerification" && message.Recipient == email)).ShouldBe(2));
        }

        [Fact]
        public async Task PasswordReset_IsGenericAndRotatesTheSecurityStamp()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"reset_{suffix}@test.com";
            User user;
            string confirmationToken;
            using (UsingTenantId(1))
            {
                await Resolve<ISettingManager>().ChangeSettingForTenantAsync(
                    1, "Abp.Account.IsSelfRegistrationEnabled", "true");
                await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    ContactNumber = "+27 72 111 5566",
                    HomeAddress = "33 Reset Road, Johannesburg",
                    Name = "Reset",
                    Password = "Customer!101",
                    Surname = "Member",
                    UserName = $"reset_{suffix}"
                });
                var manager = Resolve<UserManager>();
                await manager.InitializeOptionsAsync(1);
                user = await manager.FindByEmailAsync(email);
                confirmationToken = await manager.GenerateEmailConfirmationTokenAsync(user);
            }
            await _accountAppService.ConfirmEmail(new ConfirmEmailInput
            {
                TenantId = 1,
                UserId = user.Id,
                Token = confirmationToken
            });

            var accepted = await _accountAppService.RequestPasswordReset(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = email
            });
            var missing = await _accountAppService.RequestPasswordReset(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = $"missing_{suffix}@test.com"
            });
            accepted.Message.ShouldBe(missing.Message);

            string resetToken;
            string oldSecurityStamp;
            using (UsingTenantId(1))
            {
                var manager = Resolve<UserManager>();
                await manager.InitializeOptionsAsync(1);
                user = await manager.FindByEmailAsync(email);
                oldSecurityStamp = user.SecurityStamp;
                resetToken = await manager.GeneratePasswordResetTokenAsync(user);
            }

            (await _accountAppService.ResetPassword(new CompletePasswordResetInput
            {
                TenantId = 1,
                UserId = user.Id,
                Token = resetToken,
                NewPassword = "Changed!202"
            })).ShouldBeTrue();

            using (UsingTenantId(1))
            {
                var manager = Resolve<UserManager>();
                await manager.InitializeOptionsAsync(1);
                var updated = await manager.FindByEmailAsync(email);
                updated.SecurityStamp.ShouldNotBe(oldSecurityStamp);
                (await manager.CheckPasswordAsync(updated, "Changed!202")).ShouldBeTrue();
            }
            await UsingDbContextAsync(1, async context =>
            {
                var resetMessage = await context.TransactionalEmailOutboxMessages.SingleAsync(message =>
                    message.NotificationType == "PasswordReset" && message.Recipient == email);
                resetMessage.HtmlBody.ShouldContain("area=Default");
            });
        }
    }
}
