using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Configuration for a single facilitator rank: the cumulative number of direct referrals
    /// required to attain it, and the one-off award issued when attained.
    ///
    /// Values are sourced from <c>docs/BusinessDocs/workflows.md</c> §7 / <c>domain-model.md</c>
    /// (FacilitatorRank table). They are intentionally centralized here (not scattered as magic
    /// numbers) and mirrored in seed data. See <c>docs/ValidationPlan.md</c> V-03 — the stage
    /// arithmetic (direct vs indirect, cumulative vs per-stage) is flagged as business-unconfirmed.
    /// </summary>
    public sealed class FacilitatorRankConfiguration
    {
        public FacilitatorRank Rank { get; }
        public int DirectReferralThreshold { get; }
        public Money Award { get; }
        public string Label { get; }

        public FacilitatorRankConfiguration(FacilitatorRank rank, int directReferralThreshold, Money award, string label)
        {
            if (directReferralThreshold < 0)
            {
                throw new System.ArgumentException("Direct referral threshold cannot be negative.", nameof(directReferralThreshold));
            }

            Rank = rank;
            DirectReferralThreshold = directReferralThreshold;
            Award = award ?? throw new System.ArgumentNullException(nameof(award));
            Label = label ?? rank.ToString();
        }
    }
}
