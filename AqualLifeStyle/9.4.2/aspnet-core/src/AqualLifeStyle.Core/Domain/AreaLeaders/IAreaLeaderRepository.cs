using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.AreaLeaders
{
    public interface IAreaLeaderRepository : IRepository<AreaLeader, int>
    {
        Task<AreaLeader> GetByCustomerIdAsync(int customerId, int tenantId);
        Task<int> CountActiveAsync();
    }
}
