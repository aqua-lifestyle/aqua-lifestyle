using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Memberships
{
    public interface IMembershipLookup
    {
        Task<Membership> GetAsync(int id);
    }

    public interface IMembershipRepository : IRepository<Membership, int>, IMembershipLookup
    {
        Task<bool> ExistsByNameAsync(string name);
        Task<Membership> GetByIdAsync(int id);
        Task<Membership> GetFirstActiveAsync(int? tenantId);
        Task AddAsync(Membership membership);
    }
}
