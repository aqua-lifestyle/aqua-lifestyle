using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Deliberate, non-persisted integration boundary for B3.2 verification.
    /// Production remains disabled until the separately authorised D10 cutover.
    /// </summary>
    public interface IAQGreenPlacementV2ApprovalGate
    {
        Task<bool> IsEnabledAsync(int? tenantId, Guid participantId);
    }
}
