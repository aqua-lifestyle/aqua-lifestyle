using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.AreaLeaders.Dto;

namespace AqualLifeStyle.Application.Admin.AreaLeaders
{
    public interface IAdminAreaLeaderAppService : IApplicationService
    {
        Task<PagedResultDto<AdminAreaLeaderDto>> GetAllAsync(AdminAreaLeaderListInput input);
        Task<AdminAreaLeaderDto> GetAsync(EntityDto<int> input);
        Task<AdminAreaLeaderDto> ApproveAsync(ApproveAreaLeaderInput input);
        Task<AdminAreaLeaderDto> PromoteAsync(PromoteAreaLeaderInput input);
        Task<AdminAreaLeaderDto> DemoteAsync(DemoteAreaLeaderInput input);
        Task RemoveAsync(RemoveAreaLeaderInput input);
    }
}
