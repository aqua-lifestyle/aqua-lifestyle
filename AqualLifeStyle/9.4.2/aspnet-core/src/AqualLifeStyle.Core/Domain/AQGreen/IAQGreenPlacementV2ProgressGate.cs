using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Deliberate, non-persisted integration boundary for B5.1 read-path selection.
    /// Production remains disabled until the separately authorised D10 cutover.
    /// This gate controls whether V2 structural progress is used for read-only progress queries.
    /// It is separate from the approval gate (IAQGreenPlacementV2ApprovalGate) which controls
    /// the V2 approval/placement write path.
    /// </summary>
    public interface IAQGreenPlacementV2ProgressGate
    {
        Task<bool> IsEnabledAsync(int? tenantId, Guid participantId);
    }
}