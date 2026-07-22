using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Abp.Authorization;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using Abp.MultiTenancy;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.EntityFrameworkCore.Seed;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    public class TenantRoleAndUserBuilder
    {
        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;
        private readonly bool _seedDemoData;

        public TenantRoleAndUserBuilder(
            AqualLifeStyleDbContext context,
            int tenantId,
            bool? seedDemoData = null)
        {
            _context = context;
            _tenantId = tenantId;
            _seedDemoData = seedDemoData ?? IsDemoDataEnabled();
        }

        public void Create()
        {
            CreateRolesAndUsers();
        }

        private void CreateRolesAndUsers()
        {
            // Admin role

            var adminRole = _context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.TenantId == _tenantId && r.Name == StaticRoleNames.Tenants.Admin);
            if (adminRole == null)
            {
                adminRole = _context.Roles.Add(new Role(_tenantId, StaticRoleNames.Tenants.Admin, StaticRoleNames.Tenants.Admin) { IsStatic = true }).Entity;
                _context.SaveChanges();
            }

            // Grant all permissions to admin role

            var grantedPermissions = _context.Permissions.IgnoreQueryFilters()
                .OfType<RolePermissionSetting>()
                .Where(p => p.TenantId == _tenantId && p.RoleId == adminRole.Id)
                .Select(p => p.Name)
                .ToList();

            var permissions = PermissionFinder
                .GetAllPermissions(new AqualLifeStyleAuthorizationProvider())
                .Where(p => p.MultiTenancySides.HasFlag(MultiTenancySides.Tenant) &&
                            !grantedPermissions.Contains(p.Name))
                .ToList();

            if (permissions.Any())
            {
                _context.Permissions.AddRange(
                    permissions.Select(permission => new RolePermissionSetting
                    {
                        TenantId = _tenantId,
                        Name = permission.Name,
                        IsGranted = true,
                        RoleId = adminRole.Id
                    })
                );
                _context.SaveChanges();
            }

            // Business roles + permission grants

            new BusinessRolesAndPermissionsBuilder(_context, _tenantId).Create();

            // Admin user

            var adminUser = _context.Users.IgnoreQueryFilters().FirstOrDefault(u => u.TenantId == _tenantId && u.UserName == AbpUserBase.AdminUserName);
            if (adminUser == null)
            {
                adminUser = User.CreateTenantAdminUser(_tenantId, "admin@defaulttenant.com");
                var initialPassword = AdministratorBootstrapPasswordProvider.GetAreaAdministratorPassword(_tenantId);
                adminUser.Password = new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
                    .HashPassword(adminUser, initialPassword);
                adminUser.IsEmailConfirmed = true;
                adminUser.IsActive = true;

                _context.Users.Add(adminUser);
                _context.SaveChanges();

                // Assign Admin role to admin user
                _context.UserRoles.Add(new UserRole(_tenantId, adminUser.Id, adminRole.Id));
                _context.SaveChanges();
            }

            new DefaultCustomerUserLinker(_context, passwordHasher: new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))).Link(_tenantId);
            new DefaultCustomerAccountProvisioner(_context).Provision(_tenantId);
            if (_seedDemoData)
            {
                new AreaLeaderDemoDataBuilder(_context, _tenantId).Create();
                new FacilitatorDemoDataBuilder(_context, _tenantId).Create();
                new CustomerDemoDataBuilder(_context, _tenantId).Create();
            }
            new DefaultUserRoleAssigner(_context).AssignRoles(_tenantId);
        }

        private static bool IsDemoDataEnabled() =>
            string.Equals(
                Environment.GetEnvironmentVariable("AQUA_SEED_DEMO_DATA"),
                "true",
                StringComparison.OrdinalIgnoreCase);
    }
}
