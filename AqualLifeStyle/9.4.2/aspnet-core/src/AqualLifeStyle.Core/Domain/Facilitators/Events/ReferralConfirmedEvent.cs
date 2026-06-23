using System;
using Abp.Events.Bus;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Raised when a referral award is confirmed/issued.
    /// </summary>
    [Serializable]
    public class ReferralConfirmedEvent : EventData
    {
        public int ReferralId { get; }
        public int? ReferrerFacilitatorId { get; }
        public int? ReferrerAreaLeaderId { get; }
        public decimal AwardAmount { get; }

        public ReferralConfirmedEvent(int referralId, int? referrerFacilitatorId, int? referrerAreaLeaderId, decimal awardAmount)
        {
            ReferralId = referralId;
            ReferrerFacilitatorId = referrerFacilitatorId;
            ReferrerAreaLeaderId = referrerAreaLeaderId;
            AwardAmount = awardAmount;
        }
    }
}
