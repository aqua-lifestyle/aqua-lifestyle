using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.MultiTenancy
{
    public interface IAreaActivationStateClock
    {
        Task<DateTime> GetUtcNowAsync();
    }
}
