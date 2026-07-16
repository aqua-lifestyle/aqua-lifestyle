using System.Threading.Tasks;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.Web.Controllers;
using Shouldly;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using AqualLifeStyle.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

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

        [Theory]
        [InlineData("api/services/app/AdminCustomer/Update", "PUT")]
        [InlineData("api/services/app/AdminCustomer/Delete", "DELETE")]
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
