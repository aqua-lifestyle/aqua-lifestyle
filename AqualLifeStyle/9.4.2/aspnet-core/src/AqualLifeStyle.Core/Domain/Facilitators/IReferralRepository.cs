using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Facilitators
{
    public interface IReferralRepository : IRepository<Referral, int>
    {
        Task<Referral> GetBySourceEnquiryAsync(int enquiryId);
        Task<int> CountDirectByFacilitatorAsync(int facilitatorId);
        Task<int> CountIndirectByAreaLeaderAsync(int areaLeaderId);
    }
}
