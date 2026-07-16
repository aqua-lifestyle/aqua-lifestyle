using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Facilitators.Dto;

namespace AqualLifeStyle.Application.Admin.Facilitators
{
    public interface IAdminFacilitatorAppService : IApplicationService
    {
        Task<PagedResultDto<AdminFacilitatorDto>> GetAllAsync(AdminFacilitatorListInput input);
        Task<AdminFacilitatorDto> GetAsync(EntityDto<int> input);
        Task<AdminFacilitatorDto> ApproveAsync(ApproveFacilitatorInput input);
        Task<AdminFacilitatorDto> PromoteAsync(PromoteFacilitatorInput input);
        Task<AdminFacilitatorDto> DemoteAsync(DemoteFacilitatorInput input);
        Task RemoveAsync(RemoveFacilitatorInput input);
    }
}
