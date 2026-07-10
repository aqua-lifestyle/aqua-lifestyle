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
            var query = GetAll()
                .Where(c => c.Id == customerId && c.TenantId == tenantId && !c.MembershipId.HasValue);

            // Relational providers (PostgreSQL in production) execute a single atomic,
            // concurrency-safe set-based UPDATE. The EF Core InMemory provider used by tests
            // cannot translate ExecuteUpdate, so fall back to an equivalent tracked update there.
            if (GetDbContext().Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                var customer = await query.FirstOrDefaultAsync();
                if (customer == null)
                {
                    return false;
                }

                customer.ChangeMembership(membershipId);
                await UpdateAsync(customer);
                return true;
            }

            var rowsAffected = await query
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.MembershipId, membershipId));

            return rowsAffected == 1;
        }

        public Task AddAsync(Customer customer)
        {
            return InsertAsync(customer);
        }
    }
}
