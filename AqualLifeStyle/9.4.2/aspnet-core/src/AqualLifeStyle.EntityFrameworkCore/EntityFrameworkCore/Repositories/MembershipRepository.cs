using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;
using Abp.Runtime.Session;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class MembershipRepository : AqualLifeStyleRepositoryBase<Membership>, IMembershipRepository
    {
        private readonly IAbpSession _abpSession;

        public MembershipRepository(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IAbpSession abpSession)
            : base(dbContextProvider)
        {
            _abpSession = abpSession;
        }

        public override IQueryable<Membership> GetAll()
        {
            var tenantId = _abpSession.TenantId;
            return base.GetAll()
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == null || item.TenantId == tenantId);
        }

        public override Task<Membership> GetAsync(int id)
        {
            return GetAll().SingleAsync(item => item.Id == id);
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
