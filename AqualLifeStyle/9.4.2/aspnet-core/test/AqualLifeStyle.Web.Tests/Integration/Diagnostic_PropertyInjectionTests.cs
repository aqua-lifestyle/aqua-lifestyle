using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.InternalAccounts;
using Shouldly;
using Xunit;
using System.Linq;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public class Diagnostic_PropertyInjectionTests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public void PropertyInjection_IsConsistentAcrossResolutions()
        {
            var results = new List<string>();

            for (var i = 0; i < 10; i++)
            {
                var adminService = IocManager.Resolve<IAdminUserAppService>();
                var inviteService = IocManager.Resolve<IInternalAccountInvitationAppService>();

                var adminType = adminService.GetType();
                var inviteType = inviteService.GetType();

                var adminTm = GetPublicProperty(adminService, "TenantManager");
                var adminUm = GetPublicProperty(adminService, "UserManager");

                var inviteTm = GetPublicProperty(inviteService, "TenantManager");
                var inviteUm = GetPublicProperty(inviteService, "UserManager");

                var line = $"Iteration={i} AdminType={adminType.FullName} Admin.TM={(adminTm==null?"<null>":adminTm.GetType().FullName)} Admin.UM={(adminUm==null?"<null>":adminUm.GetType().FullName)} | InviteType={inviteType.FullName} Invite.TM={(inviteTm==null?"<null>":inviteTm.GetType().FullName)} Invite.UM={(inviteUm==null?"<null>":inviteUm.GetType().FullName)}";
                Console.WriteLine(line);
                results.Add(line);

                // Small pause to change runtime allocation patterns
                System.Threading.Thread.Sleep(20);
            }

            // If any iteration found a null TenantManager on the admin service, fail with full diagnostic output
            foreach (var r in results)
            {
                if (r.Contains("Admin.TM=<null>"))
                {
                    Assert.False(true, "Detected AdminUserAppService without TenantManager injection:\n" + string.Join("\n", results));
                }
            }

            // Otherwise pass
            Assert.True(true);
        }

        private object GetPublicProperty(object instance, string name)
        {
            var prop = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (prop == null) return null;
            return prop.GetValue(instance);
        }

        [Fact]
        public async Task TenantManager_CanResolveTenantById()
        {
            var tenantManager = IocManager.Resolve<AqualLifeStyle.MultiTenancy.TenantManager>();
            tenantManager.ShouldNotBeNull();

            var tenant = await tenantManager.GetByIdAsync(1);
            Console.WriteLine($"Tenant from TenantManager: {(tenant==null?"<null>":tenant.Name)}");

            var count = UsingDbContext(ctx => ctx.Tenants.ToList().Count);
            Console.WriteLine($"Tenants in DB: {count}");

            // Assert that tenant exists
            tenant.ShouldNotBeNull("TenantManager failed to locate tenant with id=1 in the test DB");
        }
    }
}
