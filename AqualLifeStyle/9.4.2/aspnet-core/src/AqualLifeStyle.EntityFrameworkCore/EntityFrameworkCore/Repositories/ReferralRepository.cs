using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class ReferralRepository : AqualLifeStyleRepositoryBase<Referral>, IReferralRepository
    {
        public ReferralRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<Referral> GetBySourceEnquiryAsync(int enquiryId)
            => GetAll().FirstOrDefaultAsync(r => r.SourceEnquiryId == enquiryId);

        public Task<int> CountDirectByFacilitatorAsync(int facilitatorId)
            => GetAll().CountAsync(r => r.ReferrerFacilitatorId == facilitatorId);

        public Task<int> CountIndirectByAreaLeaderAsync(int areaLeaderId)
            => GetAll().CountAsync(r => r.ReferrerAreaLeaderId == areaLeaderId);
    }
}
