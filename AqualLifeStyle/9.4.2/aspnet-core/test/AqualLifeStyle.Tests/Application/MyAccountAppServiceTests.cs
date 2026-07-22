using System;
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

        private async Task<long> CreateAndLoginTestUserAsync()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = await CreateTestUserAsync(AbpSession.TenantId, $"testuser{suffix}", $"testuser{suffix}@defaulttenant.com");
            SetCurrentUser(userId, AbpSession.TenantId);
            return userId;
        }

        [Fact]
        public async Task ChangePassword_ReplacesPasswordAndInvalidatesExistingSessions()
        {
            await CreateAndLoginTestUserAsync();

            string originalSecurityStamp = null;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                originalSecurityStamp = user.SecurityStamp;
            });

            await _service.ChangePasswordAsync(new ChangeMyPasswordInput
            {
                CurrentPassword = User.DefaultPassword,
                NewPassword = "PrivateAdminPassword123!"
            });

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                var hasher = Resolve<IPasswordHasher<User>>();
                hasher.VerifyHashedPassword(user, user.Password, User.DefaultPassword)
                    .ShouldBe(PasswordVerificationResult.Failed);
                hasher.VerifyHashedPassword(user, user.Password, "PrivateAdminPassword123!")
                    .ShouldNotBe(PasswordVerificationResult.Failed);
                user.SecurityStamp.ShouldNotBe(originalSecurityStamp);
            });
        }

        [Fact]
        public async Task ChangePassword_WithIncorrectCurrentPassword_DoesNotChangePassword()
        {
            await CreateAndLoginTestUserAsync();

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
                hasher.VerifyHashedPassword(user, user.Password, User.DefaultPassword)
                    .ShouldNotBe(PasswordVerificationResult.Failed);
                user.AccessFailedCount.ShouldBeGreaterThan(0);
            });
        }
    }
}
