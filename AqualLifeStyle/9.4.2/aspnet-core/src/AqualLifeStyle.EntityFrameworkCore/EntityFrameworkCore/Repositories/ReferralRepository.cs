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
            => GetAll()
                .Where(referral => referral.SourceEnquiryId == enquiryId)
                .FirstOrDefaultAsync();

        public Task<Referral> GetBySourceEnquiryAsync(int enquiryId, int? tenantId)
            => GetAll()
                .Where(referral =>
                    referral.SourceEnquiryId == enquiryId &&
                    referral.TenantId == tenantId)
                .FirstOrDefaultAsync();

        public Task<int> CountDirectByFacilitatorAsync(int facilitatorId)
            => GetAll()
                .Where(referral => referral.ReferrerFacilitatorId == facilitatorId)
                .CountAsync();

        public Task<int> CountIndirectByAreaLeaderAsync(int areaLeaderId)
            => GetAll()
                .Where(referral => referral.ReferrerAreaLeaderId == areaLeaderId)
                .CountAsync();
    }
}
