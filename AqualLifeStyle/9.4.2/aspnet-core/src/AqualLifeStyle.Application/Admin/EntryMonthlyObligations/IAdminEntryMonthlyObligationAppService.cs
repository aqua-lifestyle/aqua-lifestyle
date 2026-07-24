using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;

namespace AqualLifeStyle.Application.Admin.EntryMonthlyObligations
{
    public interface IAdminEntryMonthlyObligationAppService
        : IApplicationService
    {
        Task<PagedResultDto<EntryMonthlyObligationDto>> GetAllAsync(
            AdminEntryMonthlyObligationListInput input);
    }
}
