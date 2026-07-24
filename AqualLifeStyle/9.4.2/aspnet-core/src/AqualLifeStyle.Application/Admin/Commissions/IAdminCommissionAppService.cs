using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Commissions.Dto;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public interface IAdminCommissionAppService : IApplicationService
    {
        Task<PagedResultDto<AdminWeeklyCommissionDto>> GetAllAsync(
            AdminCommissionListInput input);

        Task<CommissionCalculationResultDto> CalculateLatestClosedWeekAsync(
            CalculateLatestClosedCommissionWeekInput input);
    }
}
