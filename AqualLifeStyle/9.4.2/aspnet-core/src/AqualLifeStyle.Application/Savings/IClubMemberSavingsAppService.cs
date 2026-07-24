using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Savings.Dto;

namespace AqualLifeStyle.Application.Savings
{
    public interface IClubMemberSavingsAppService : IApplicationService
    {
        Task<MySavingsAccountDto> GetMyAccountAsync();
    }
}
