using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Tenants.Dto;

namespace AqualLifeStyle.Application.Admin.Tenants
{
    public interface IAdminTenantAppService : IApplicationService
    {
        Task<PagedResultDto<AdminTenantDto>> GetAllAsync(AdminTenantListInput input);
        Task<AdminTenantDto> GetAsync(EntityDto<int> input);
        Task<AdminTenantDto> CreateAsync(CreateAdminTenantInput input);
        Task<AdminTenantDto> EditAsync(EditAdminTenantInput input);
        Task<AdminTenantDto> SetActivationAsync(SetTenantActivationInput input);
        Task<AdminTenantDto> AssignAreaLeaderAsync(AssignTenantAreaLeaderInput input);
    }
}
