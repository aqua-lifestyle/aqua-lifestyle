using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Customers
{
    public interface ICustomerRepository : IRepository<Customer, int>
    {
        Task<bool> ExistsByEmailAsync(string email);
        Task<bool> ExistsByEmailAsync(string email, int? excludeCustomerId);
        Task<bool> AssignMembershipIfUnassignedAsync(int customerId, int? tenantId, int membershipId);
    }
}
