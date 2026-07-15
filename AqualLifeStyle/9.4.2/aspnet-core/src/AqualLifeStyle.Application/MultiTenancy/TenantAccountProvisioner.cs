using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Extensions;
using Abp.IdentityFramework;
using Abp.Localization;
using Abp.MultiTenancy;
using Abp.Runtime.Security;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Editions;

namespace AqualLifeStyle.MultiTenancy
{
    public class TenantProvisioningRequest
    {
        public string TenancyName { get; set; }
        public string Name { get; set; }
        public string AdminEmailAddress { get; set; }
        public string ConnectionString { get; set; }
        public bool IsActive { get; set; }
    }

    public interface ITenantAccountProvisioner
    {
        Task<Tenant> ProvisionAsync(TenantProvisioningRequest request);
    }

    public class TenantAccountProvisioner : ITenantAccountProvisioner, ITransientDependency
    {
        private readonly TenantManager _tenantManager;
        private readonly EditionManager _editionManager;
        private readonly UserManager _userManager;
        private readonly RoleManager _roleManager;
        private readonly IAbpZeroDbMigrator _databaseMigrator;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly ILocalizationManager _localizationManager;

        public TenantAccountProvisioner(TenantManager tenantManager, EditionManager editionManager,
            UserManager userManager, RoleManager roleManager, IAbpZeroDbMigrator databaseMigrator,
            IUnitOfWorkManager unitOfWorkManager, ILocalizationManager localizationManager)
        {
            _tenantManager = tenantManager;
            _editionManager = editionManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _databaseMigrator = databaseMigrator;
            _unitOfWorkManager = unitOfWorkManager;
            _localizationManager = localizationManager;
        }

        public async Task<Tenant> ProvisionAsync(TenantProvisioningRequest request)
        {
            var tenant = new Tenant(request.TenancyName.Trim(), request.Name.Trim())
            {
                IsActive = request.IsActive,
                ConnectionString = request.ConnectionString.IsNullOrEmpty()
                    ? null
                    : SimpleStringCipher.Instance.Encrypt(request.ConnectionString)
            };
            var defaultEdition = await _editionManager.FindByNameAsync(EditionManager.DefaultEditionName);
            if (defaultEdition != null) tenant.EditionId = defaultEdition.Id;
            await _tenantManager.CreateAsync(tenant);
            await _unitOfWorkManager.Current.SaveChangesAsync();
            _databaseMigrator.CreateOrMigrateForTenant(tenant);

            using (_unitOfWorkManager.Current.SetTenantId(tenant.Id))
            {
                (await _roleManager.CreateStaticRoles(tenant.Id)).CheckErrors(_localizationManager);
                await _unitOfWorkManager.Current.SaveChangesAsync();
                var adminRole = _roleManager.Roles.Single(role => role.Name == StaticRoleNames.Tenants.Admin);
                await _roleManager.GrantAllPermissionsAsync(adminRole);
                var adminUser = User.CreateTenantAdminUser(tenant.Id, request.AdminEmailAddress.Trim());
                await _userManager.InitializeOptionsAsync(tenant.Id);
                (await _userManager.CreateAsync(adminUser, User.DefaultPassword)).CheckErrors(_localizationManager);
                await _unitOfWorkManager.Current.SaveChangesAsync();
                (await _userManager.AddToRoleAsync(adminUser, adminRole.Name)).CheckErrors(_localizationManager);
                await _unitOfWorkManager.Current.SaveChangesAsync();
            }
            return tenant;
        }
    }
}
