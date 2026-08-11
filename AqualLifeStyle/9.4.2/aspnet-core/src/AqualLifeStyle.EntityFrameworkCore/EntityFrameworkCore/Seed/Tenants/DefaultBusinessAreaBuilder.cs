using System;
using System.Linq;
using Abp.MultiTenancy;
using AqualLifeStyle.Domain.Areas;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    /// <summary>
    /// Bootstraps Johannesburg only for Aqua's existing Default Tenant. This is
    /// not a general default for newly provisioned Tenants.
    /// </summary>
    internal sealed class DefaultBusinessAreaBuilder
    {
        public const string JohannesburgCode = "JHB";
        public const string JohannesburgName = "Johannesburg";

        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;

        public DefaultBusinessAreaBuilder(AqualLifeStyleDbContext context, int tenantId)
        {
            _context = context;
            _tenantId = tenantId;
        }

        public void Create()
        {
            var tenant = _context.Tenants.IgnoreQueryFilters()
                .Single(item => item.Id == _tenantId);
            if (!string.Equals(
                    tenant.TenancyName,
                    AbpTenantBase.DefaultTenantName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var area = _context.Areas.IgnoreQueryFilters()
                .SingleOrDefault(item =>
                    item.TenantId == _tenantId && item.Code == JohannesburgCode);
            if (area == null)
            {
                area = Area.Create(_tenantId, JohannesburgCode, JohannesburgName);
                _context.Areas.Add(area);
                _context.SaveChanges();
            }

            var baselineAt = DateTime.UtcNow;
            var administratorUserIds = (
                from userRole in _context.UserRoles.IgnoreQueryFilters()
                join role in _context.Roles.IgnoreQueryFilters()
                    on userRole.RoleId equals role.Id
                join user in _context.Users.IgnoreQueryFilters()
                    on userRole.UserId equals user.Id
                where userRole.TenantId == _tenantId &&
                      role.TenantId == _tenantId &&
                      user.TenantId == _tenantId &&
                      !role.IsDeleted &&
                      !user.IsDeleted &&
                      user.IsActive &&
                      (role.Name == "Admin" || role.Name == "SystemAdmin")
                select user.Id)
                .Distinct()
                .ToList();
            var assignedUserIds = _context.AreaAdminAssignments.IgnoreQueryFilters()
                .Where(item =>
                    item.TenantId == _tenantId &&
                    item.AreaId == area.Id &&
                    !item.RevokedAt.HasValue)
                .Select(item => item.UserId)
                .ToHashSet();
            foreach (var userId in administratorUserIds.Where(id => !assignedUserIds.Contains(id)))
            {
                _context.AreaAdminAssignments.Add(
                    AreaAdminAssignment.Assign(area, userId, _tenantId, baselineAt));
            }

            _context.SaveChanges();
        }
    }
}
