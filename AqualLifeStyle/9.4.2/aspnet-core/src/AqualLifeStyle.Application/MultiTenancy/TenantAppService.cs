using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Extensions;
using Abp.IdentityFramework;
using Abp.Linq.Extensions;
using Abp.MultiTenancy;
using Abp.Runtime.Security;
using Abp.UI;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Editions;
using AqualLifeStyle.MultiTenancy.Dto;
using Microsoft.AspNetCore.Identity;

namespace AqualLifeStyle.MultiTenancy
{
    [AbpAuthorize(PermissionNames.Pages_Tenants)]
    public class TenantAppService : AsyncCrudAppService<Tenant, TenantDto, int, PagedTenantResultRequestDto, CreateTenantDto, TenantDto>, ITenantAppService
    {
        private readonly ITenantAccountProvisioner _tenantAccountProvisioner;

        public TenantAppService(
            IRepository<Tenant, int> repository,
            ITenantAccountProvisioner tenantAccountProvisioner)
            : base(repository)
        {
            _tenantAccountProvisioner = tenantAccountProvisioner;
        }

        public override async Task<TenantDto> CreateAsync(CreateTenantDto input)
        {
            CheckCreatePermission();

            var tenant = await _tenantAccountProvisioner.ProvisionAsync(new TenantProvisioningRequest
            {
                TenancyName = input.TenancyName, Name = input.Name, AdminEmailAddress = input.AdminEmailAddress,
                ConnectionString = input.ConnectionString, IsActive = input.IsActive,
                Justification = "Area provisioned through legacy tenant administration."
            });

            return MapToEntityDto(tenant);
        }

        protected override IQueryable<Tenant> CreateFilteredQuery(PagedTenantResultRequestDto input)
        {
            return Repository.GetAll()
                .WhereIf(!input.Keyword.IsNullOrWhiteSpace(), x => x.TenancyName.Contains(input.Keyword) || x.Name.Contains(input.Keyword))
                .WhereIf(input.IsActive.HasValue, x => x.IsActive == input.IsActive);
        }

        protected override void MapToEntity(TenantDto updateInput, Tenant entity)
        {
            // Manually mapped since TenantDto contains non-editable properties too.
            entity.Name = updateInput.Name;
            entity.TenancyName = updateInput.TenancyName;
            if (entity.IsActive != updateInput.IsActive)
            {
                throw new UserFriendlyException(
                    "Area activation must be changed through the audited Area activation action.");
            }
        }

        public override Task DeleteAsync(EntityDto<int> input)
        {
            CheckDeletePermission();
            throw new UserFriendlyException(
                "Area deletion is unavailable because financial history must be preserved. Deactivate the Area instead.");
        }

        private void CheckErrors(IdentityResult identityResult)
        {
            identityResult.CheckErrors(LocalizationManager);
        }
    }
}
