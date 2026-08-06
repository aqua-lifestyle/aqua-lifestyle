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
            var snapshots = new List<(string AdminTM, string AdminUM, string InviteTM, string InviteUM)>();

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

                var adminTmStr = adminTm == null ? "<null>" : adminTm.GetType().FullName;
                var adminUmStr = adminUm == null ? "<null>" : adminUm.GetType().FullName;
                var inviteTmStr = inviteTm == null ? "<null>" : inviteTm.GetType().FullName;
                var inviteUmStr = inviteUm == null ? "<null>" : inviteUm.GetType().FullName;

                snapshots.Add((adminTmStr, adminUmStr, inviteTmStr, inviteUmStr));

                var line = $"Iteration={i} AdminType={adminType.FullName} Admin.TM={adminTmStr} Admin.UM={adminUmStr} | InviteType={inviteType.FullName} Invite.TM={inviteTmStr} Invite.UM={inviteUmStr}";
                Console.WriteLine(line);
                results.Add(line);

                // Small pause to change runtime allocation patterns
                System.Threading.Thread.Sleep(20);
            }

            // Build a concise diagnostic if something is wrong
            string diagnostic = string.Join("\n", results);

            // Ensure none of the resolved dependencies are null
            for (var i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                if (s.AdminTM == "<null>" || s.AdminUM == "<null>" || s.InviteTM == "<null>" || s.InviteUM == "<null>")
                {
                    Assert.False(true, "Detected null injected dependency on iteration " + i + ":\n" + diagnostic);
                }
            }

            // Ensure consistency across iterations by comparing to the first snapshot
            var baseline = snapshots.First();
            for (var i = 1; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                if (s.AdminTM != baseline.AdminTM || s.AdminUM != baseline.AdminUM || s.InviteTM != baseline.InviteTM || s.InviteUM != baseline.InviteUM)
                {
                    Assert.False(true, "Detected inconsistent injected dependency across resolutions (baseline vs iteration " + i + "):\n" + diagnostic);
                }
            }

            // If we reach here, all checks passed
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

            // Create a dedicated tenant fixture for this test to avoid relying on seeded data or execution order
            var tenantId = UsingDbContext(ctx =>
            {
                var t = new AqualLifeStyle.MultiTenancy.Tenant("tn" + Guid.NewGuid().ToString("n").Substring(0, 8), "Test Tenant " + Guid.NewGuid().ToString("n").Substring(0, 6));
                ctx.Tenants.Add(t);
                ctx.SaveChanges();
                return t.Id;
            });

            var tenant = await tenantManager.GetByIdAsync(tenantId);
            Console.WriteLine($"Tenant from TenantManager: {(tenant==null?"<null>":tenant.Name)} (id={tenantId})");

            var count = UsingDbContext(ctx => ctx.Tenants.ToList().Count);
            Console.WriteLine($"Tenants in DB: {count}");

            // Assert that tenant exists
            tenant.ShouldNotBeNull($"TenantManager failed to locate tenant with id={tenantId} in the test DB");

            // Then assert the resolved tenant has the expected id
            tenant.Id.ShouldBe(tenantId);
        }
    }
}
