using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Memberships.Dto.Benefits;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships
{
    public interface IMembershipBenefitAppService : IApplicationService
    {
        Task<List<MembershipBenefitDto>> GetByMembershipTypeAsync(MembershipType membershipType);
        Task<MembershipBenefitDto> GetAsync(int id);
        Task<MembershipBenefitDto> CreateAsync(CreateMembershipBenefitDto input);
        Task<MembershipBenefitDto> UpdateAsync(int id, MembershipBenefitDto input);
        Task DeleteAsync(int id);
    }
}
