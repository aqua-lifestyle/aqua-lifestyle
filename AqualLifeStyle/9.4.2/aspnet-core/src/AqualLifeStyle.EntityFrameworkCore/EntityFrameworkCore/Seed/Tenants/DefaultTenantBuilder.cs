using System.Linq;
using Microsoft.EntityFrameworkCore;
using Abp.Configuration;
using Abp.MultiTenancy;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Editions;
using AqualLifeStyle.MultiTenancy;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    public class DefaultTenantBuilder
    {
        private readonly AqualLifeStyleDbContext _context;

        public DefaultTenantBuilder(AqualLifeStyleDbContext context)
        {
            _context = context;
        }

        public void Create()
        {
            CreateDefaultTenant();
        }

        private void CreateDefaultTenant()
        {
            // Default tenant

            var defaultTenant = _context.Tenants.IgnoreQueryFilters().FirstOrDefault(t => t.TenancyName == AbpTenantBase.DefaultTenantName);
            if (defaultTenant == null)
            {
                defaultTenant = new Tenant(AbpTenantBase.DefaultTenantName, AbpTenantBase.DefaultTenantName);

                var defaultEdition = _context.Editions.IgnoreQueryFilters().FirstOrDefault(e => e.Name == EditionManager.DefaultEditionName);
                if (defaultEdition != null)
                {
                    defaultTenant.EditionId = defaultEdition.Id;
                }

                _context.Tenants.Add(defaultTenant);
                _context.SaveChanges();
            }

            EnableCustomerSelfRegistrationWhenNotConfigured(defaultTenant.Id);
        }

        private void EnableCustomerSelfRegistrationWhenNotConfigured(int tenantId)
        {
            var registrationSettingExists = _context.Settings
                .IgnoreQueryFilters()
                .Any(setting =>
                    setting.Name == AppSettingNames.IsSelfRegistrationEnabled &&
                    setting.TenantId == tenantId &&
                    setting.UserId == null);
            if (registrationSettingExists)
            {
                return;
            }

            _context.Settings.Add(new Setting(
                tenantId,
                null,
                AppSettingNames.IsSelfRegistrationEnabled,
                "true"));
            _context.SaveChanges();
        }
    }
}
