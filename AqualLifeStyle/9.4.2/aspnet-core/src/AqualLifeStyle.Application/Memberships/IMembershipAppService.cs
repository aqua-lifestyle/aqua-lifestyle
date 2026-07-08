using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships
{
    public interface IMembershipAppService : IApplicationService
    {
        Task<IReadOnlyList<MembershipDto>> GetAllAsync();
        Task<MembershipDto> GetAsync(int id);
        Task<MembershipDto> UpdateAsync(MembershipDto input);
        Task CreateAsync(CreateMembershipDto input);
        Task<MembershipDto> SetActivationDateAsync(int id, SetMembershipActivationDto input);
        Task<MembershipDto> SetMonthlyObligationAsync(int id, SetMonthlyObligationDto input);
        Task<MembershipDto> MarkObligationMetAsync(int id, MarkObligationMetDto input);
        Task<TierBenefitsDto> GetTierBenefitsAsync(int id);
        TierBenefitsDto GetTierBenefitsByType(MembershipType membershipType);
        Task<bool> IsOrderWindowOpenAsync(int id);
        Task<bool> IsSavingsWindowOpenAsync(int id);
        IReadOnlyList<SavingsWindowStatusDto> GetSavingsWindowStatuses(string asOfDate = null);
    }
}
