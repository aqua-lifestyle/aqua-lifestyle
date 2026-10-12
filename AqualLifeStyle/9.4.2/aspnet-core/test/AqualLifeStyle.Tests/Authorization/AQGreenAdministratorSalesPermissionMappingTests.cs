using System.Linq;
using Abp.Authorization.Roles;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.EntityFrameworkCore.Seed.Tenants;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Authorization
{
    public sealed class AQGreenAdministratorSalesPermissionMappingTests : AqualLifeStyleTestBase
    {
        private const string Review = AquaPermissions.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility;

        [Theory]
        [InlineData("Admin")]
        [InlineData("SystemAdmin")]
        public void TenantAdministratorRoles_ReceiveReviewWithoutBroaderFinancialScope(string roleName)
        {
            UsingDbContext(1, context =>
            {
                var role = context.Roles.Single(item => item.TenantId == 1 && item.Name == roleName);
                var grants = context.Permissions.OfType<RolePermissionSetting>()
                    .Where(item => item.TenantId == 1 && item.RoleId == role.Id).ToList();
                grants.Single(item => item.Name == Review).IsGranted.ShouldBeTrue();
                foreach (var permission in new[] { AquaPermissions.Admin.AllTenants,
                    AquaPermissions.Admin.Commissions.Calculate, AquaPermissions.Admin.Commissions.Release,
                    AquaPermissions.Admin.Commissions.RecordPayment })
                    grants.ShouldNotContain(item => item.Name == permission && item.IsGranted);
            });
        }

        [Fact]
        public void HostAdmin_RetainsReviewAndExplicitCentralScopeGrants()
        {
            UsingDbContext(null, context =>
            {
                var role = context.Roles.IgnoreQueryFilters()
                    .Single(item => item.TenantId == null && item.Name == "Admin");
                var grants = context.Permissions.IgnoreQueryFilters().OfType<RolePermissionSetting>()
                    .Where(item => item.TenantId == null && item.RoleId == role.Id).ToList();
                grants.Single(item => item.Name == Review).IsGranted.ShouldBeTrue();
                grants.Single(item => item.Name == AquaPermissions.Admin.AllTenants).IsGranted.ShouldBeTrue();
            });
        }

        [Theory]
        [InlineData("AreaLeader")]
        [InlineData("Facilitator")]
        [InlineData("Member")]
        [InlineData("Guest")]
        public void BusinessRoleAlone_DoesNotConveyAdministratorReview(string roleName)
        {
            UsingDbContext(1, context =>
            {
                var role = context.Roles.Single(item => item.TenantId == 1 && item.Name == roleName);
                context.Permissions.OfType<RolePermissionSetting>()
                    .Count(item => item.TenantId == 1 && item.RoleId == role.Id &&
                        item.Name == Review && item.IsGranted).ShouldBe(0);
            });
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("SystemAdmin")]
        public void SeederRetry_PreservesExplicitReviewDenialAndSinglePermissionRow(string roleName)
        {
            UsingDbContext(1, context =>
            {
                var role = context.Roles.Single(item => item.TenantId == 1 && item.Name == roleName);
                var permission = context.Permissions.OfType<RolePermissionSetting>()
                    .Single(item => item.TenantId == 1 && item.RoleId == role.Id && item.Name == Review);
                permission.IsGranted = false;
                context.SaveChanges();
                new TenantRoleAndUserBuilder(context, 1, seedDemoData: false).Create();
                context.Permissions.OfType<RolePermissionSetting>()
                    .Single(item => item.TenantId == 1 && item.RoleId == role.Id && item.Name == Review)
                    .IsGranted.ShouldBeFalse();
            });
        }
    }
}
