using System;
using System.Threading.Tasks;
using Abp.UI;
using Abp.Configuration;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AccountRegistrationBlockedTests : AqualLifeStyleTestBase
    {
        private readonly IAccountAppService _accountAppService;

        public AccountRegistrationBlockedTests()
        {
            _accountAppService = Resolve<IAccountAppService>();
        }

        [Fact]
        public async Task Register_WhenSelfRegistrationDisabled_ShouldThrowUserFriendlyException()
        {
            // Arrange: ensure we're operating in the default tenant
            var settingManager = Resolve<ISettingManager>();
            await settingManager.ChangeSettingForApplicationAsync("Abp.Account.IsSelfRegistrationEnabled", "true");

            using (UsingTenantId(1))
            {
                // Disable self registration for tenant 1
                await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "false");

                var input = new RegisterInput
                {
                    EmailAddress = $"blocked_{Guid.NewGuid():N}@test.com",
                    Name = "Blocked",
                    Password = "Customer!101",
                    Surname = "User",
                    UserName = $"blocked_{Guid.NewGuid():N}"
                };

                // Act & Assert
                await Should.ThrowAsync<UserFriendlyException>(() => _accountAppService.Register(input));
            }
        }
    }
}
