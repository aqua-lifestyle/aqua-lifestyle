using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AreaLeaders;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class AreaSpaceRepository : AqualLifeStyleRepositoryBase<AreaSpace>, IAreaSpaceRepository
    {
        public AreaSpaceRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
    }
}
