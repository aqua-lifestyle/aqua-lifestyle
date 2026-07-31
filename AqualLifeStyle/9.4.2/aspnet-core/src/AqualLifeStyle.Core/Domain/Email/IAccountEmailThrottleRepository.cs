using System;
using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Email
{
    public interface IAccountEmailThrottleRepository : IRepository<AccountEmailThrottle, string>
    {
        Task<bool> TryAcquireAsync(string key, int tenantId, DateTime now, DateTime expiresAt);
    }
}
