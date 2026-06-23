using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.Web.Controllers;
using Shouldly;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using AqualLifeStyle.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Abp.Authorization.Users;
using AqualLifeStyle.Authentication.JwtBearer;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public class HomeController_Tests: AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task Index_Test()
        {
            await AuthenticateAsync(null, new AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });

            //Act
            var response = await GetResponseAsStringAsync(
                GetUrl<HomeController>(nameof(HomeController.Index))
            );

            //Assert
            response.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task HostAdministratorToken_IncludesGrantedAdministrationPermissions()
        {
            var authentication = await AuthenticateAsync(null, new AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(authentication.AccessToken).Claims.ToList();
            var permissions = claims.Single(claim => claim.Type == "permissions").Value.Split(',');
            permissions.ShouldContain(AquaPermissions.Admin.Users.View);
            permissions.ShouldContain(AquaPermissions.Admin.Tenants.View);
            permissions.ShouldContain(AquaPermissions.Admin.Customers.View);
            permissions.ShouldContain(PermissionNames.Pages_Roles);
            claims.ShouldContain(claim => claim.Type == JwtSessionSecurityStampValidator.SecurityStampClaimType);
        }

        [Fact]
        public async Task RotatingSecurityStamp_ImmediatelyInvalidatesExistingJwt()
        {
            await AuthenticateAsync(null, new AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });
            UsingDbContext(context =>
            {
                var user = context.Users.Single(item => item.TenantId == null && item.UserName == AbpUserBase.AdminUserName);
                user.SecurityStamp = System.Guid.NewGuid().ToString();
                context.SaveChanges();
            });
            AbpSession.UserId = null;
            AbpSession.TenantId = null;

            var response = await Client.GetAsync("/api/services/app/AdminCustomer/GetAll?SkipCount=0&MaxResultCount=10");
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task DivisionAdministratorToken_IncludesDivisionManagementPermissionsOnly()
        {
            var authentication = await AuthenticateAsync("Default", new AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(authentication.AccessToken).Claims.ToList();
            var permissions = claims.Single(claim => claim.Type == "permissions").Value.Split(',');
            permissions.ShouldContain(AquaPermissions.Admin.Users.View);
            permissions.ShouldContain(AquaPermissions.Admin.Customers.View);
            permissions.ShouldContain(AquaPermissions.Admin.Members.View);
            permissions.ShouldContain(PermissionNames.Pages_Roles);
            permissions.ShouldNotContain(AquaPermissions.Admin.Tenants.View);
        }

        [Fact]
        public async Task Authentication_RequiresRestoredCustomerToCompletePasswordReset()
        {
            UsingDbContext(context =>
            {
                var user = context.Users.Single(item => item.TenantId == 1 && item.UserName == AbpUserBase.AdminUserName);
                user.RequirePasswordReset();
                context.SaveChanges();
            });

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/TokenAuth/Authenticate");
                request.Headers.Add("__tenant", "Default");
                request.Content = new StringContent(JsonSerializer.Serialize(new AuthenticateModel
                {
                    UserNameOrEmailAddress = "admin",
                    Password = "123qwe"
                }), Encoding.UTF8, "application/json");

                var response = await Client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                responseBody.ShouldContain("Password reset required.");
                responseBody.ShouldNotContain("accessToken");
            }
            finally
            {
                UsingDbContext(context =>
                {
                    var user = context.Users.Single(item => item.TenantId == 1 && item.UserName == AbpUserBase.AdminUserName);
                    user.CompleteRequiredPasswordReset();
                    context.SaveChanges();
                });
            }
        }

        [Theory]
        [InlineData("api/services/app/AdminCustomer/Update", "PUT")]
        [InlineData("api/services/app/AdminCustomer/Delete", "DELETE")]
        [InlineData("api/services/app/AdminCustomer/Restore", "POST")]
        [InlineData("api/services/app/Account/CompletePasswordSetup", "POST")]
        [InlineData("api/services/app/AdminUser/Update", "PUT")]
        [InlineData("api/services/app/AdminUser/Delete", "DELETE")]
        [InlineData("api/services/app/AdminAreaLeader/Remove", "DELETE")]
        [InlineData("api/services/app/AdminFacilitator/Remove", "DELETE")]
        [InlineData("api/services/app/AdminMember/EditProfile", "POST")]
        [InlineData("api/services/app/AdminTenant/Edit", "POST")]
        public void AdministratorMutationRoutes_UseTheirConventionalHttpMethods(string relativePath, string expectedMethod)
        {
            var apiExplorer = IocManager.Resolve<IApiDescriptionGroupCollectionProvider>();
            var apiDescription = apiExplorer.ApiDescriptionGroups.Items
                .SelectMany(group => group.Items)
                .SingleOrDefault(description => string.Equals(description.RelativePath, relativePath, System.StringComparison.OrdinalIgnoreCase));

            apiDescription.ShouldNotBeNull($"The administrator route '{relativePath}' was not registered.");
            apiDescription.HttpMethod.ShouldBe(expectedMethod);
        }
    }
}
