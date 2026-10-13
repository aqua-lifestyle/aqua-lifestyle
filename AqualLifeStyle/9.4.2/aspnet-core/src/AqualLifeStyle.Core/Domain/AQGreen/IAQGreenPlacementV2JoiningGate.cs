using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Selects prospective invitation joining and validates its resolved destination.
    /// Production requires separately authorized D10 commissioning.
    /// </summary>
    public interface IAQGreenPlacementV2JoiningGate
    {
        Task<bool> IsEnabledAsync(int tenantId);
        Task ValidateDestinationAsync(int tenantId, Guid destinationAreaId);
    }
}
