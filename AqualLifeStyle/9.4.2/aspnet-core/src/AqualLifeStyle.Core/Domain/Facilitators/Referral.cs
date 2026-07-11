using System;
using Abp.Domain.Entities.Auditing;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// A single attributed referral: a converted enquiry that brought a new customer into the network.
    /// Direct referrals credit the sourcing facilitator; indirect referrals credit that facilitator's
    /// upline area leader. Awards are issued on confirmation.
    /// </summary>
    public class Referral : FullAuditedAggregateRoot<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public int? ReferrerFacilitatorId { get; private set; }
        public int? ReferrerAreaLeaderId { get; private set; }
        public int ReferredCustomerId { get; private set; }
        public int SourceEnquiryId { get; private set; }
        public ReferralType Type { get; private set; }
        public decimal AwardAmount { get; private set; }
        public bool AwardIssued { get; private set; }
        public DateTime? ConfirmedAt { get; private set; }
        public DateTime ConvertedAt { get; private set; }

        protected Referral()
        {
        }

        private Referral(
            int tenantId,
            int? referrerFacilitatorId,
            int? referrerAreaLeaderId,
            int referredCustomerId,
            int sourceEnquiryId,
            ReferralType type,
            decimal awardAmount,
            DateTime convertedAt)
        {
            if (tenantId <= 0) throw new ArgumentException("TenantId must be valid.", nameof(tenantId));
            if (referredCustomerId <= 0) throw new ArgumentException("ReferredCustomerId must be valid.", nameof(referredCustomerId));
            if (sourceEnquiryId <= 0) throw new ArgumentException("SourceEnquiryId must be valid.", nameof(sourceEnquiryId));
            if (awardAmount < 0) throw new ArgumentException("Award amount cannot be negative.", nameof(awardAmount));
            if (referrerFacilitatorId == null && referrerAreaLeaderId == null)
            {
                throw new ArgumentException("A referral must credit a facilitator or an area leader.");
            }

            TenantId = tenantId;
            ReferrerFacilitatorId = referrerFacilitatorId;
            ReferrerAreaLeaderId = referrerAreaLeaderId;
            ReferredCustomerId = referredCustomerId;
            SourceEnquiryId = sourceEnquiryId;
            Type = type;
            AwardAmount = awardAmount;
            AwardIssued = false;
            ConvertedAt = convertedAt == default ? DateTime.UtcNow : convertedAt;
        }

        public static Referral CreateDirect(
            int tenantId, int referrerFacilitatorId, int referredCustomerId, int sourceEnquiryId, decimal awardAmount, DateTime convertedAt)
            => new Referral(tenantId, referrerFacilitatorId, null, referredCustomerId, sourceEnquiryId, ReferralType.Direct, awardAmount, convertedAt);

        public static Referral CreateIndirect(
            int tenantId, int referrerAreaLeaderId, int referredCustomerId, int sourceEnquiryId, decimal awardAmount, DateTime convertedAt)
            => new Referral(tenantId, null, referrerAreaLeaderId, referredCustomerId, sourceEnquiryId, ReferralType.Indirect, awardAmount, convertedAt);

        /// <summary>
        /// Mark the award as issued (idempotent). Raises <see cref="ReferralConfirmedEvent"/>.
        /// </summary>
        public void ConfirmAward()
        {
            if (AwardIssued)
            {
                return;
            }

            if (AwardAmount <= 0)
            {
                throw new InvalidOperationException("Cannot confirm an award of zero.");
            }

            AwardIssued = true;
            ConfirmedAt = DateTime.UtcNow;
            DomainEvents.Add(new ReferralConfirmedEvent(Id, ReferrerFacilitatorId, ReferrerAreaLeaderId, AwardAmount));
        }
    }
}
