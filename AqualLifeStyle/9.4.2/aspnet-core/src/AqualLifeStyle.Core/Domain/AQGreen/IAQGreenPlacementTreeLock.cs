using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenPlacementTreeLock
    {
        Task AcquireAsync(
            Guid placementTreeScopeId,
            CancellationToken cancellationToken = default);
    }

    public interface IAQGreenPlacementClock
    {
        Task<DateTime> GetUtcNowAsync(CancellationToken cancellationToken = default);
    }
}
