using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Email;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class AccountEmailThrottleRepository
        : AqualLifeStyleRepositoryBase<AccountEmailThrottle, string>,
          IAccountEmailThrottleRepository
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AccountEmailThrottleRepository(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<bool> TryAcquireAsync(
            string key,
            int tenantId,
            DateTime now,
            DateTime expiresAt)
        {
            var renewed = await GetAll()
                .Where(throttle =>
                    throttle.Id == key &&
                    throttle.TenantId == tenantId &&
                    throttle.ExpiresAt <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(throttle => throttle.ExpiresAt, expiresAt));
            if (renewed == 1)
            {
                return true;
            }

            var throttle = AccountEmailThrottle.Create(key, tenantId, expiresAt);
            var context = _dbContextProvider.GetDbContext();
            await InsertAsync(throttle);
            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException exception) when (DatabaseUniqueConstraintDetector.Matches(
                exception,
                "PK_AccountEmailThrottles",
                "AccountEmailThrottles.Id"))
            {
                context.Entry(throttle).State = EntityState.Detached;
                return false;
            }
        }
    }
}
