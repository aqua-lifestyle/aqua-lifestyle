using System;
using System.Linq;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    public class DefaultUserRoleAssigner
    {
        private readonly AqualLifeStyleDbContext _context;

        public DefaultUserRoleAssigner(AqualLifeStyleDbContext context)
        {
            _context = context;
        }

        public void AssignRoles(int tenantId)
        {
            var users = _context.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId && u.IsActive && !u.IsDeleted)
                .ToList();

            var roleNames = _context.Roles
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .ToDictionary(r => r.Id, r => r.Name);

            var rolePriority = new[]
            {
                AquaUserRole.SystemAdmin,
                AquaUserRole.AreaLeader,
                AquaUserRole.Facilitator,
                AquaUserRole.Member,
                AquaUserRole.Guest
            };

            foreach (var user in users)
            {
                if (user.Role != AquaUserRole.Guest)
                {
                    continue;
                }

                var userRoles = _context.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.UserId == user.Id && ur.TenantId == tenantId)
                    .ToList();

                if (!userRoles.Any())
                {
                    user.SetRole(AquaUserRole.Guest);
                    continue;
                }

                AquaUserRole? bestRole = null;
                foreach (var userRole in userRoles)
                {
                    if (!roleNames.TryGetValue(userRole.RoleId, out var roleName))
                    {
                        continue;
                    }

                    var mappedRole = MapRoleName(roleName);
                    if (mappedRole.HasValue)
                    {
                        if (bestRole == null || Array.IndexOf(rolePriority, mappedRole.Value) < Array.IndexOf(rolePriority, bestRole.Value))
                        {
                            bestRole = mappedRole;
                        }
                    }
                }

                user.SetRole(bestRole ?? AquaUserRole.Guest);
            }

            _context.SaveChanges();
        }

        private static AquaUserRole? MapRoleName(string roleName)
        {
            return roleName switch
            {
                "Admin" => AquaUserRole.SystemAdmin,
                "SystemAdmin" => AquaUserRole.SystemAdmin,
                "AreaLeader" => AquaUserRole.AreaLeader,
                "Facilitator" => AquaUserRole.Facilitator,
                "Member" => AquaUserRole.Member,
                "Guest" => AquaUserRole.Guest,
                _ => null
            };
        }
    }
}
