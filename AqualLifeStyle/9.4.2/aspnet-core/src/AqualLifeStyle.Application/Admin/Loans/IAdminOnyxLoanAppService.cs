using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Loans.Dto;

namespace AqualLifeStyle.Application.Admin.Loans
{
    public interface IAdminOnyxLoanAppService : IApplicationService
    {
        Task<PagedResultDto<OnyxLoanAgreementDto>> GetAllAsync(
            AdminOnyxLoanAgreementListInput input);
    }
}
