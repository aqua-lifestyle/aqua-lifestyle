using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Savings.Dto;

namespace AqualLifeStyle.Application.Admin.Savings
{
    public interface IAdminSavingsAppService : IApplicationService
    {
        Task<PagedResultDto<SavingsAccountDto>> GetAllAsync(
            AdminSavingsAccountListInput input);
    }
}
