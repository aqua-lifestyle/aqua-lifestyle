using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Memberships
{
    public interface IMembershipBenefitRepository : IRepository<MembershipBenefit, int>
    {
        Task<List<MembershipBenefit>> GetByMembershipTypeAsync(MembershipType membershipType);
        Task<MembershipBenefit> GetByIdAsync(int id);
        Task AddAsync(MembershipBenefit benefit);
    }
}
