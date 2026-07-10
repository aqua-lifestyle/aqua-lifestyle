using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class FacilitatorRepository : AqualLifeStyleRepositoryBase<Facilitator>, IFacilitatorRepository
    {
        public FacilitatorRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<Facilitator> GetByCustomerIdAsync(int customerId)
            => GetAll().FirstOrDefaultAsync(f => f.CustomerId == customerId);

        public Task<int> CountByAreaLeaderAsync(int areaLeaderId)
            => GetAll().CountAsync(f => f.AreaLeaderId == areaLeaderId);
    }
}
