using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Abp.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public class AccountApplicationService_Tests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task TenantSelfRegistrationAvailability_ReturnsTheLiveAreaSettingWithoutAuthentication()
        {
            var settingManager = IocManager.Resolve<ISettingManager>();
            await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "true");

            try
            {
                var response = await Client.GetAsync(
                    "/api/services/app/Account/GetTenantSelfRegistrationAvailability?tenancyName=Default");

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await response.Content.ReadAsStringAsync())
                    .ShouldContain("\"isSelfRegistrationEnabled\":true");
            }
            finally
            {
                await settingManager.ChangeSettingForTenantAsync(
                    1,
                    "Abp.Account.IsSelfRegistrationEnabled",
                    "false");
            }
        }
    }
}
