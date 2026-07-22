using System.Linq;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Security
{
    public class AdministratorSeedTests : AqualLifeStyleTestBase
    {
        [Fact]
        public void HostAdministratorRole_IsNotAssignedToNewUsersByDefault()
        {
            UsingDbContext(null, context =>
            {
                var administratorRole = context.Roles
                    .IgnoreQueryFilters()
                    .Single(role => role.TenantId == null && role.Name == StaticRoleNames.Host.Admin);

                administratorRole.IsDefault.ShouldBeFalse();
            });
        }

        [Fact]
        public void SeededAreaAdministrator_CanUseBootstrapPassword()
        {
            UsingDbContext(1, context =>
            {
                var administrator = context.Users.Single(user =>
                    user.TenantId == 1 && user.UserName == User.AdminUserName);

                administrator.RequiresPasswordReset().ShouldBeFalse();
            });
        }
    }
}
