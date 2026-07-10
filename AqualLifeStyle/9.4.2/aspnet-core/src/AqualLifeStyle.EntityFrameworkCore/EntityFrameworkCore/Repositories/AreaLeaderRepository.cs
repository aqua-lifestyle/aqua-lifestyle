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

        public Task<AreaLeader> GetByCustomerIdAsync(int customerId)
            => GetAll().FirstOrDefaultAsync(a => a.CustomerId == customerId);

        public Task<int> CountActiveAsync()
            => GetAll().CountAsync(a => !a.IsDeleted);
    }
}
