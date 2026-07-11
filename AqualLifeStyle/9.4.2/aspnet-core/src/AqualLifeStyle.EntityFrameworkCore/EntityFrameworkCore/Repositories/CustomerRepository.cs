using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Domain.Customers;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class CustomerRepository : AqualLifeStyleRepositoryBase<Customer>, ICustomerRepository
    {
        public CustomerRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<bool> ExistsByEmailAsync(string email)
        {
            return ExistsByEmailAsync(email, null);
        }

        public Task<bool> ExistsByEmailAsync(string email, int? excludeCustomerId)
        {
            var normalizedEmail = email?.Trim();
            var query = GetAll().Where(c => c.Email.Value == normalizedEmail);
            if (excludeCustomerId.HasValue)
            {
                query = query.Where(c => c.Id != excludeCustomerId.Value);
            }

            return query.AnyAsync();
        }

        public Task<Customer> GetByIdAsync(int id)
        {
            return GetAsync(id);
        }

        public async Task<bool> AssignMembershipIfUnassignedAsync(int customerId, int? tenantId, int membershipId)
        {
            // Single atomic, concurrency-safe set-based UPDATE. Only assigns when the customer is
            // still unassigned, so concurrent conversions can never overwrite an existing membership.
            var rowsAffected = await GetAll()
                .Where(c => c.Id == customerId && c.TenantId == tenantId && !c.MembershipId.HasValue)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.MembershipId, membershipId));

            return rowsAffected == 1;
        }

        public Task AddAsync(Customer customer)
        {
            return InsertAsync(customer);
        }
    }
}
