using System;
using Abp.Events.Bus;

namespace AqualLifeStyle.Domain.AreaLeaders
{
    /// <summary>
    /// Raised when an area space is approved, enabling its area leader to operate.
    /// </summary>
    [Serializable]
    public class AreaSpaceApprovedEvent : EventData
    {
        public int AreaSpaceId { get; }
        public int AreaLeaderId { get; }

        public AreaSpaceApprovedEvent(int areaSpaceId, int areaLeaderId)
        {
            AreaSpaceId = areaSpaceId;
            AreaLeaderId = areaLeaderId;
        }
    }
}
