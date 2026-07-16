using System.Threading.Tasks;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.Web.Controllers;
using Shouldly;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using AqualLifeStyle.Authorization;

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
    }
}
