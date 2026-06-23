using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Stateless domain service that computes commission/award amounts.
    /// Amounts are sourced from <c>FacilitatorRankTable</c> (direct facilitator awards) and a
    /// documented, seeded per-indirect-referral amount for area leaders. All values are flagged
    /// against <c>docs/ValidationPlan.md</c> V-03 (business-unconfirmed arithmetic).
    /// </summary>
    public sealed class CommissionCalculator
    {
        /// <summary>
        /// One-off award for attaining a facilitator rank (from the rank table).
        /// </summary>
        public Money ComputeFacilitatorAward(FacilitatorRank rank)
            => FacilitatorRankTable.For(rank).Award;

        /// <summary>
        /// Documented area-leader indirect-referral commission. Seeded constant (V-03): each
        /// indirect referral credited to an area leader is worth this amount. Revisit once the
        /// area-leader income table basis is confirmed with leadership.
        /// </summary>
        public const decimal AreaLeaderIndirectReferralCommission = 250m;

        public Money ComputeAreaLeaderIndirectAward(int indirectReferrals)
        {
            if (indirectReferrals < 0)
            {
                throw new System.ArgumentException("Indirect referral count cannot be negative.", nameof(indirectReferrals));
            }

            return Money.Of(AreaLeaderIndirectReferralCommission * indirectReferrals);
        }
    }
}
