using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    public interface IClubMemberEntryMonthlyObligationAppService
        : IApplicationService
    {
        Task<IReadOnlyList<EntryMonthlyObligationDto>>
            GetMyObligationsAsync();

        Task<EntryMonthlyObligationCheckoutDto> CreateCheckoutAsync(
            CreateEntryMonthlyObligationCheckoutInput input);
    }
}
