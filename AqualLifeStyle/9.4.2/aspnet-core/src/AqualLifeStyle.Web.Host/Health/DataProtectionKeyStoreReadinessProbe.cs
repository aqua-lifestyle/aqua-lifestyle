using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Web.Host.Health
{
    /// <summary>
    /// Checks whether the persistent Data Protection key store can be queried.
    /// </summary>
    public interface IDataProtectionKeyStoreReadinessProbe
    {
        /// <summary>
        /// Returns whether the key table is reachable without creating or reading key XML.
        /// </summary>
        Task<bool> IsReadyAsync();
    }

    /// <summary>
    /// Read-only readiness probe for the dedicated Data Protection context.
    /// </summary>
    public sealed class DataProtectionKeyStoreReadinessProbe
        : IDataProtectionKeyStoreReadinessProbe
    {
        private readonly DataProtectionKeyDbContext _dbContext;

        /// <summary>
        /// Initializes a new probe for the persistent key store.
        /// </summary>
        public DataProtectionKeyStoreReadinessProbe(DataProtectionKeyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task<bool> IsReadyAsync()
        {
            try
            {
                await _dbContext.DataProtectionKeys
                    .AsNoTracking()
                    .Select(key => key.Id)
                    .Take(1)
                    .ToListAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
