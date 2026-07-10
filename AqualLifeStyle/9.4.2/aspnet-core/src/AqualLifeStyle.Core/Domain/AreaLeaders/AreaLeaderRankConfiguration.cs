using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.AreaLeaders
{
    /// <summary>
    /// Configuration for a single area-leader rank: the cumulative order target required to attain it.
    /// Values from <c>docs/BusinessDocs/workflows.md</c> §6 (Ruby 20 → Ambassador 18,000).
    /// Open/Closed: adding a rank is a data change, not a change to <see cref="RankProgressionPolicy"/>.
    /// </summary>
    public sealed class AreaLeaderRankConfiguration
    {
        public AreaLeaderRank Rank { get; }
        public int OrderTargetThreshold { get; }
        public string Label { get; }

        public AreaLeaderRankConfiguration(AreaLeaderRank rank, int orderTargetThreshold, string label)
        {
            if (orderTargetThreshold < 0)
            {
                throw new System.ArgumentException("Order target threshold cannot be negative.", nameof(orderTargetThreshold));
            }

            Rank = rank;
            OrderTargetThreshold = orderTargetThreshold;
            Label = label ?? rank.ToString();
        }
    }

    /// <summary>Ordered, immutable table of area-leader rank configurations, lowest → highest.</summary>
    public static class AreaLeaderRankTable
    {
        public static IReadOnlyList<AreaLeaderRankConfiguration> All { get; } = new List<AreaLeaderRankConfiguration>
        {
            new AreaLeaderRankConfiguration(AreaLeaderRank.Ruby, 20, "Ruby"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.Emerald, 60, "Emerald"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.Premier, 100, "Premier"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.Dimond, 200, "Dimond"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.VIP, 400, "VIP"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.Presidential, 1200, "Presidential"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.ChairmansCircle, 3600, "Chairman's Circle"),
            new AreaLeaderRankConfiguration(AreaLeaderRank.Ambassador, 18000, "Ambassador")
        };

        public static AreaLeaderRankConfiguration For(AreaLeaderRank rank)
            => All.First(c => c.Rank == rank);
    }
}
