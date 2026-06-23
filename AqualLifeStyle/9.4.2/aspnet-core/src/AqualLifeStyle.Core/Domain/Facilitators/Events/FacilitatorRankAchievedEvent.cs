using System;
using Abp.Events.Bus;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Raised when a facilitator advances to a higher rank and earns its award.
    /// </summary>
    [Serializable]
    public class FacilitatorRankAchievedEvent : EventData
    {
        public int FacilitatorId { get; }
        public int CustomerId { get; }
        public FacilitatorRank Rank { get; }
        public decimal AwardAmount { get; }

        public FacilitatorRankAchievedEvent(int facilitatorId, int customerId, FacilitatorRank rank, decimal awardAmount)
        {
            FacilitatorId = facilitatorId;
            CustomerId = customerId;
            Rank = rank;
            AwardAmount = awardAmount;
        }
    }
}
