using System.Threading.Tasks;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.MyAccount;
using AqualLifeStyle.Application.MyAccount.Dto;
using AqualLifeStyle.Authorization.Users;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class MyAccountAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IMyAccountAppService _service;

        public MyAccountAppServiceTests()
        {
            _service = Resolve<IMyAccountAppService>();
        }

        [Fact]
        public async Task ChangePassword_ReplacesPasswordAndInvalidatesExistingSessions()
        {
            string originalSecurityStamp = null;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                originalSecurityStamp = user.SecurityStamp;
            });

            await _service.ChangePasswordAsync(new ChangeMyPasswordInput
            {
                CurrentPassword = "123qwe",
                NewPassword = "PrivateAdminPassword123!"
            });

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                var hasher = Resolve<IPasswordHasher<User>>();
                hasher.VerifyHashedPassword(user, user.Password, "123qwe")
                    .ShouldBe(PasswordVerificationResult.Failed);
                hasher.VerifyHashedPassword(user, user.Password, "PrivateAdminPassword123!")
                    .ShouldNotBe(PasswordVerificationResult.Failed);
                user.SecurityStamp.ShouldNotBe(originalSecurityStamp);
            });
        }

        [Fact]
        public async Task ChangePassword_WithIncorrectCurrentPassword_DoesNotChangePassword()
        {
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _service.ChangePasswordAsync(new ChangeMyPasswordInput
                {
                    CurrentPassword = "not-the-current-password",
                    NewPassword = "PrivateAdminPassword123!"
                }));

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                var hasher = Resolve<IPasswordHasher<User>>();
                hasher.VerifyHashedPassword(user, user.Password, "123qwe")
                    .ShouldNotBe(PasswordVerificationResult.Failed);
            });
        }
    }
}
