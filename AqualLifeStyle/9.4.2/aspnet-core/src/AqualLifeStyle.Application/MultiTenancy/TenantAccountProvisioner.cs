using System;
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
using AqualLifeStyle.Application.InternalAccounts;
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
        public string Justification { get; set; }
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
        private readonly InternalAccountInvitationManager _invitationManager;
        private readonly IAreaActivationStateRecorder _activationStateRecorder;
        private readonly IAreaActivationStateClock _activationStateClock;

        public TenantAccountProvisioner(TenantManager tenantManager, EditionManager editionManager,
            UserManager userManager, RoleManager roleManager, IAbpZeroDbMigrator databaseMigrator,
            IUnitOfWorkManager unitOfWorkManager, ILocalizationManager localizationManager,
            InternalAccountInvitationManager invitationManager,
            IAreaActivationStateRecorder activationStateRecorder,
            IAreaActivationStateClock activationStateClock)
        {
            _tenantManager = tenantManager;
            _editionManager = editionManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _databaseMigrator = databaseMigrator;
            _unitOfWorkManager = unitOfWorkManager;
            _localizationManager = localizationManager;
            _invitationManager = invitationManager;
            _activationStateRecorder = activationStateRecorder;
            _activationStateClock = activationStateClock;
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
            var effectiveAt = await _activationStateClock.GetUtcNowAsync();
            await _activationStateRecorder.RecordAsync(
                tenant.Id,
                tenant.IsActive,
                AreaActivationStateRecordKind.Provisioned,
                string.IsNullOrWhiteSpace(request.Justification)
                    ? "Area provisioned through tenant administration."
                    : request.Justification,
                effectiveAt);
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
                (await _userManager.CreateAsync(adminUser, CreateSystemGeneratedPassword())).CheckErrors(_localizationManager);
                await _unitOfWorkManager.Current.SaveChangesAsync();
                (await _userManager.AddToRoleAsync(adminUser, adminRole.Name)).CheckErrors(_localizationManager);
                await _invitationManager.CreateAsync(adminUser, tenant, DateTime.UtcNow);
                await _unitOfWorkManager.Current.SaveChangesAsync();
            }
            return tenant;
        }

        private static string CreateSystemGeneratedPassword()
            => $"Aa1!{Guid.NewGuid():N}";
    }
}
