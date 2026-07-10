using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class FacilitatorRepository : AqualLifeStyleRepositoryBase<Facilitator>, IFacilitatorRepository
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public FacilitatorRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public Task<Facilitator> GetByCustomerIdAsync(int customerId)
            => GetAll().FirstOrDefaultAsync(f => f.CustomerId == customerId);

        public async Task<Facilitator> GetWithAreaLeaderAsync(int facilitatorId)
        {
            // Load the facilitator directly (without an Include on the required AreaLeader
            // navigation). A required navigation that is filtered (e.g. soft-deleted) would turn
            // the Include into an inner join and drop the facilitator row entirely. Loading the
            // area leader separately keeps the facilitator discoverable while still reporting a
            // null AreaLeader when it is missing or soft-deleted.
            var facilitator = await GetAll().FirstOrDefaultAsync(f => f.Id == facilitatorId);
            if (facilitator?.AreaLeaderId != null)
            {
                var dbContext = _dbContextProvider.GetDbContext();
                var areaLeader = await dbContext.AreaLeaders
                    .FirstOrDefaultAsync(a => a.Id == facilitator.AreaLeaderId && !a.IsDeleted);

                dbContext.Entry(facilitator).Reference(f => f.AreaLeader).CurrentValue = areaLeader;
            }

            return facilitator;
        }

        public Task<int> CountByAreaLeaderAsync(int areaLeaderId)
            => GetAll().CountAsync(f => f.AreaLeaderId == areaLeaderId);
    }
}
