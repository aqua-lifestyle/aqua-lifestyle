using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Facilitators
{
    public interface IFacilitatorRepository : IRepository<Facilitator, int>
    {
        Task<Facilitator> GetByCustomerIdAsync(int customerId);
        Task<int> CountByAreaLeaderAsync(int areaLeaderId);
    }
}
