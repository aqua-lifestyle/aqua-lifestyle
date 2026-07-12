using System.Linq;
using Abp.Authorization;
using Abp.Authorization.Roles;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    public class BusinessRolesAndPermissionsBuilder
    {
        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;

        public BusinessRolesAndPermissionsBuilder(AqualLifeStyleDbContext context, int tenantId)
        {
            _context = context;
            _tenantId = tenantId;
        }

        public void Create()
        {
            CreateRolesAndGrantPermissions();
        }

        private void CreateRolesAndGrantPermissions()
        {
            var roles = new[]
            {
                new { Name = "SystemAdmin", DisplayName = "System Admin" },
                new { Name = "AreaLeader", DisplayName = "Area Leader" },
                new { Name = "Facilitator", DisplayName = "Facilitator" },
                new { Name = "Member", DisplayName = "Member" },
                new { Name = "Guest", DisplayName = "Guest" }
            };

            foreach (var roleDef in roles)
            {
                var role = _context.Roles.IgnoreQueryFilters()
                    .FirstOrDefault(r => r.TenantId == _tenantId && r.Name == roleDef.Name);

                if (role == null)
                {
                    role = _context.Roles.Add(new Role(_tenantId, roleDef.Name, roleDef.DisplayName)).Entity;
                    _context.SaveChanges();
                }

                GrantPermissions(role.Id);
            }
        }

        private void GrantPermissions(int roleId)
        {
            var role = _context.Roles.IgnoreQueryFilters().First(r => r.Id == roleId);
            var granted = _context.Permissions.IgnoreQueryFilters()
                .OfType<RolePermissionSetting>()
                .Where(p => p.TenantId == _tenantId && p.RoleId == roleId)
                .Select(p => p.Name)
                .ToHashSet();

            var permissions = PermissionFinder
                .GetAllPermissions(new AqualLifeStyleAuthorizationProvider())
                .Where(p => p.MultiTenancySides.HasFlag(MultiTenancySides.Tenant))
                .ToList();

            var toGrant = permissions
                .Where(p => !granted.Contains(p.Name) && RoleShouldReceive(role.Name, p.Name))
                .Select(p => new RolePermissionSetting
                {
                    TenantId = _tenantId,
                    Name = p.Name,
                    IsGranted = true,
                    RoleId = roleId
                })
                .ToList();

            if (toGrant.Any())
            {
                _context.Permissions.AddRange(toGrant);
                _context.SaveChanges();
            }
        }

        private static bool RoleShouldReceive(string roleName, string permissionName)
        {
            if (roleName == "Guest")
            {
                return false;
            }

            if (roleName == "SystemAdmin" || roleName == StaticRoleNames.Tenants.Admin)
            {
                return true;
            }

            if (roleName == "AreaLeader")
            {
                return AreaLeaderPermissions.Contains(permissionName);
            }

            if (roleName == "Facilitator")
            {
                return FacilitatorPermissions.Contains(permissionName);
            }

            if (roleName == "Member")
            {
                return MemberPermissions.Contains(permissionName);
            }

            return false;
        }

        private static readonly string[] AreaLeaderPermissions =
        {
            PermissionNames.Pages_AreaLeaders,
            PermissionNames.Pages_AreaLeaders_Manage,
            PermissionNames.Pages_AreaSpaces,
            PermissionNames.Pages_AreaSpaces_Manage,
            PermissionNames.Pages_Facilitators,
            PermissionNames.Pages_Facilitators_Manage,
            PermissionNames.Pages_Customers,
            PermissionNames.Pages_Customers_Manage,
            PermissionNames.Pages_Memberships,
            PermissionNames.Pages_Memberships_Manage,
            PermissionNames.Pages_Enquiries,
            PermissionNames.Pages_Enquiries_Manage,
            PermissionNames.Pages_Referrals,
            PermissionNames.Pages_Referrals_Manage,
            PermissionNames.Pages_Orders,
            PermissionNames.Pages_Orders_Manage
        };

        private static readonly string[] FacilitatorPermissions =
        {
            PermissionNames.Pages_Facilitators,
            PermissionNames.Pages_Customers,
            PermissionNames.Pages_Customers_Manage,
            PermissionNames.Pages_Memberships,
            PermissionNames.Pages_Memberships_Manage,
            PermissionNames.Pages_Enquiries,
            PermissionNames.Pages_Enquiries_Manage,
            PermissionNames.Pages_Referrals,
            PermissionNames.Pages_Referrals_Manage,
            PermissionNames.Pages_Orders,
            PermissionNames.Pages_Orders_Manage
        };

        private static readonly string[] MemberPermissions =
        {
            PermissionNames.Pages_Customers,
            PermissionNames.Pages_Memberships,
            PermissionNames.Pages_Enquiries,
            PermissionNames.Pages_Orders
        };
    }
}
