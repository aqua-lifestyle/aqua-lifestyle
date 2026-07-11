using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AreaLeaders;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class AreaLeaderRepository : AqualLifeStyleRepositoryBase<AreaLeader>, IAreaLeaderRepository
    {
        public AreaLeaderRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<AreaLeader> GetByCustomerIdAsync(int customerId, int tenantId)
            => GetAll()
                .Where(areaLeader => areaLeader.TenantId == tenantId && areaLeader.CustomerId == customerId)
                .FirstOrDefaultAsync();

        public Task<int> CountActiveAsync()
            => GetAll()
                .Where(areaLeader => !areaLeader.IsDeleted)
                .CountAsync();
    }
}
