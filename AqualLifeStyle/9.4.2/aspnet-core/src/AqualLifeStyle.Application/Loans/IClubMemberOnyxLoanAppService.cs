using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Loans.Dto;

namespace AqualLifeStyle.Application.Loans
{
    public interface IClubMemberOnyxLoanAppService : IApplicationService
    {
        Task<MyOnyxLoanAgreementsDto> GetMyAgreementsAsync();
    }
}
