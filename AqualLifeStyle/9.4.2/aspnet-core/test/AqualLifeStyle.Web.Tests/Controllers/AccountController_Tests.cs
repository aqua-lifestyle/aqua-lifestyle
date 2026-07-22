using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Abp.Configuration;
using AngleSharp.Html.Dom;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public class AccountController_Tests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task LoginPage_WhenSelfRegistrationDisabled_ShouldNotShowRegisterLink()
        {
            var settingManager = IocManager.Resolve<ISettingManager>();
            await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "false");

            using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
            request.Headers.Add("__tenant", "Default");

            var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var html = await response.Content.ReadAsStringAsync();
            var document = ParseHtml(html);
            var registerLink = document.QuerySelectorAll("a").OfType<IHtmlAnchorElement>()
                .FirstOrDefault(a => a.TextContent.Trim().Contains("Register"));

            registerLink.ShouldBeNull();
        }

        [Fact]
        public async Task RegisterPage_WhenSelfRegistrationDisabled_RedirectsToLogin()
        {
            var settingManager = IocManager.Resolve<ISettingManager>();
            await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "false");

            using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Register");
            request.Headers.Add("__tenant", "Default");

            var response = await Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.OriginalString.ShouldContain("/Account/Login");
        }

        [Fact]
        public async Task RegisterPost_WhenSelfRegistrationDisabled_RedirectsToLogin()
        {
            var settingManager = IocManager.Resolve<ISettingManager>();
            await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "false");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register")
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("EmailAddress", "blocked@example.com"),
                    new KeyValuePair<string, string>("Name", "Blocked"),
                    new KeyValuePair<string, string>("Password", "Customer!101"),
                    new KeyValuePair<string, string>("Surname", "Customer"),
                    new KeyValuePair<string, string>("UserName", "blocked")
                })
            };
            request.Headers.Add("__tenant", "Default");

            var response = await Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.OriginalString.ShouldContain("/Account/Login");
        }
    }
}
