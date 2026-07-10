using System;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Domain.AreaNetwork
{
    /// <summary>
    /// Stateless domain service that attributes a converted enquiry to the network:
    /// a <see cref="ReferralType.Direct"/> referral credits the sourcing facilitator (and awards the
    /// facilitator's rank-up award), and a <see cref="ReferralType.Indirect"/> referral credits the
    /// facilitator's upline area leader. Keeps aggregates decoupled (ADR-002): this service orchestrates
    /// the side-effects; aggregates raise their own events.
    /// </summary>
    public sealed class ReferralAttributionService
    {
        private readonly CommissionCalculator _commissionCalculator;

        public ReferralAttributionService(CommissionCalculator commissionCalculator)
        {
            _commissionCalculator = commissionCalculator ?? throw new ArgumentNullException(nameof(commissionCalculator));
        }

        /// <summary>
        /// Apply a conversion to the given facilitator + area leader. Mutates both aggregates in place
        /// (referral counts, rank, award balance) and returns the two referrals to be persisted.
        /// </summary>
        public ReferralAttributionResult Attribute(
            EnquiryConvertedEvent evt, Facilitator facilitator, AreaLeader areaLeader)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (facilitator == null) throw new ArgumentNullException(nameof(facilitator));
            if (areaLeader == null) throw new ArgumentNullException(nameof(areaLeader));
            if (evt.ReferredByFacilitatorId == null)
            {
                throw new ArgumentException("Referral attribution requires a sourcing facilitator.", nameof(evt));
            }

            // Direct referral → facilitator
            facilitator.RecordDirectReferral();
            var previousDirect = facilitator.DirectReferrals - 1;
            var currentDirect = facilitator.DirectReferrals;
            decimal facilitatorAward = 0m;
            var crossedRank = FacilitatorRankTable.HighestCrossedBetween(previousDirect, currentDirect);
            if (crossedRank != null)
            {
                var award = _commissionCalculator.ComputeFacilitatorAward(crossedRank.Rank).Amount;
                facilitator.AwardRank(crossedRank.Rank, award);
                facilitatorAward += award;
            }

            // Indirect referral → upline area leader
            areaLeader.RecordIndirectReferral();
            var areaLeaderAward = CommissionCalculator.AreaLeaderIndirectReferralCommission;

            var directReferral = Referral.CreateDirect(
                facilitator.TenantId, facilitator.Id, evt.CustomerId, evt.EnquiryId, facilitatorAward, evt.ConvertedAt);

            var indirectReferral = Referral.CreateIndirect(
                areaLeader.TenantId, areaLeader.Id, evt.CustomerId, evt.EnquiryId, areaLeaderAward, evt.ConvertedAt);

            return new ReferralAttributionResult(directReferral, indirectReferral, facilitatorAward, areaLeaderAward);
        }
    }

    /// <summary>Outcome of a single referral attribution.</summary>
    public sealed class ReferralAttributionResult
    {
        public Referral DirectReferral { get; }
        public Referral IndirectReferral { get; }
        public decimal FacilitatorAward { get; }
        public decimal AreaLeaderAward { get; }

        public ReferralAttributionResult(
            Referral directReferral, Referral indirectReferral, decimal facilitatorAward, decimal areaLeaderAward)
        {
            DirectReferral = directReferral ?? throw new ArgumentNullException(nameof(directReferral));
            IndirectReferral = indirectReferral ?? throw new ArgumentNullException(nameof(indirectReferral));
            FacilitatorAward = facilitatorAward;
            AreaLeaderAward = areaLeaderAward;
        }
    }
}
