using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class MembershipBenefitRepository : AqualLifeStyleRepositoryBase<MembershipBenefit>, IMembershipBenefitRepository
    {
        public MembershipBenefitRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public async Task<List<MembershipBenefit>> GetByMembershipTypeAsync(MembershipType membershipType)
        {
            var query = GetAll().Where(b => b.MembershipType == membershipType && b.IsActive);
            return await Task.FromResult(query.ToList());
        }

        public Task<MembershipBenefit> GetByIdAsync(int id)
        {
            return GetAsync(id);
        }

        public Task AddAsync(MembershipBenefit benefit)
        {
            return InsertAsync(benefit);
        }
    }
}
