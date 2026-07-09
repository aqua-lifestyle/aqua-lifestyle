using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.AreaLeaders;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Stateless domain policy that maps a facilitator's cumulative direct-referral count to a rank.
    /// Bronze is the floor (entry rank) even before any referrals; ranks then advance as the
    /// cumulative direct-referral count crosses each configured threshold.
    /// </summary>
    public sealed class RankProgressionPolicy
    {
        private readonly IReadOnlyList<FacilitatorRankConfiguration> _table;

        public RankProgressionPolicy()
            : this(FacilitatorRankTable.All)
        {
        }

        public RankProgressionPolicy(IReadOnlyList<FacilitatorRankConfiguration> table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        /// <summary>
        /// Highest rank whose threshold is satisfied by <paramref name="directReferrals"/>.
        /// Returns <see cref="FacilitatorRank.Bronze"/> when below the first threshold.
        /// </summary>
        public FacilitatorRank EvaluateFacilitatorRank(int directReferrals)
        {
            if (directReferrals < 0)
            {
                throw new ArgumentException("Direct referral count cannot be negative.", nameof(directReferrals));
            }

            var best = FacilitatorRank.Bronze;
            foreach (var config in _table.OrderBy(c => c.DirectReferralThreshold))
            {
                if (directReferrals >= config.DirectReferralThreshold)
                {
                    best = config.Rank;
                }
            }

            return best;
        }

        /// <summary>
        /// The next rank above <paramref name="current"/>, or null if already at the top.
        /// </summary>
        public FacilitatorRank? NextRank(FacilitatorRank current)
        {
            var ordered = _table.OrderBy(c => c.DirectReferralThreshold).ToList();
            var index = ordered.FindIndex(c => c.Rank == current);
            if (index < 0 || index + 1 >= ordered.Count)
            {
                return null;
            }

            return ordered[index + 1].Rank;
        }

        /// <summary>
        /// Highest area-leader rank whose order-target threshold is satisfied by <paramref name="orderTarget"/>.
        /// Returns <see cref="AreaLeaderRank.Ruby"/> when below the first threshold.
        /// </summary>
        public AreaLeaderRank EvaluateAreaLeaderRank(int orderTarget)
        {
            if (orderTarget < 0)
            {
                throw new ArgumentException("Order target cannot be negative.", nameof(orderTarget));
            }

            var best = AreaLeaderRank.Ruby;
            foreach (var config in AreaLeaderRankTable.All.OrderBy(c => c.OrderTargetThreshold))
            {
                if (orderTarget >= config.OrderTargetThreshold)
                {
                    best = config.Rank;
                }
            }

            return best;
        }
    }
}
