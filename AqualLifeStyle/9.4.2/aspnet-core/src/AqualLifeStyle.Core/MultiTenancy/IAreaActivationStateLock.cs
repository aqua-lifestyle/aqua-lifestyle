using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.MultiTenancy
{
    public interface IAreaActivationStateLock
    {
        Task<DateTime> AcquireAsync(int tenantId);
    }
}
