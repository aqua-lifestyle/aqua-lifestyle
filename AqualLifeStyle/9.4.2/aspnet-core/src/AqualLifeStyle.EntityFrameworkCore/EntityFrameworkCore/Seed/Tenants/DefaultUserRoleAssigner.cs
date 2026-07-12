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

            foreach (var user in users)
            {
                if (user.Role != AquaUserRole.Guest)
                {
                    continue;
                }

                var isAdmin = _context.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.UserId == user.Id && ur.TenantId == tenantId)
                    .AsEnumerable()
                    .Any(ur => roleNames.TryGetValue(ur.RoleId, out var name) && (name == "Admin" || name == "SystemAdmin"));

                if (isAdmin) { user.SetRole(AquaUserRole.SystemAdmin); continue; }

                var customer = _context.Customers.IgnoreQueryFilters().SingleOrDefault(item => item.TenantId == tenantId && item.UserId == user.Id);
                if (customer == null) { user.SetRole(AquaUserRole.Guest); continue; }

                if (_context.AreaLeaders.IgnoreQueryFilters().Any(item => item.TenantId == tenantId && !item.IsDeleted && item.CustomerId == customer.Id))
                    user.SetRole(AquaUserRole.AreaLeader);
                else if (_context.Facilitators.IgnoreQueryFilters().Any(item => item.TenantId == tenantId && !item.IsDeleted && item.CustomerId == customer.Id))
                    user.SetRole(AquaUserRole.Facilitator);
                else if (customer.MembershipId.HasValue)
                    user.SetRole(AquaUserRole.Member);
                else
                    user.SetRole(AquaUserRole.Guest);
            }

            _context.SaveChanges();
        }

    }
}
