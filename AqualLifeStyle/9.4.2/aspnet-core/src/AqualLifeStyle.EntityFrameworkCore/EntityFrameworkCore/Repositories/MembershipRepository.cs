using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class MembershipRepository : AqualLifeStyleRepositoryBase<Membership>, IMembershipRepository
    {
        public MembershipRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<bool> ExistsByNameAsync(string name)
        {
            return GetAll().AnyAsync(x => x.Name == name.Trim());
        }

        public Task<Membership> GetByIdAsync(int id)
        {
            return GetAsync(id);
        }

        public Task<Membership> GetFirstActiveAsync(int? tenantId)
        {
            return GetAll()
                .Where(x => x.IsActive && x.TenantId == tenantId)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();
        }

        public Task AddAsync(Membership membership)
        {
            return InsertAsync(membership);
        }
    }
}
