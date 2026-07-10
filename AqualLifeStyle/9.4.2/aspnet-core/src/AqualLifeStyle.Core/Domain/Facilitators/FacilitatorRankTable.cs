using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Ordered, immutable table of facilitator rank configurations, lowest → highest.
    /// Open/Closed: adding a rank is a data change here, not a change to callers
    /// (<see cref="RankProgressionPolicy"/> reads this table).
    /// </summary>
    public static class FacilitatorRankTable
    {
        public static IReadOnlyList<FacilitatorRankConfiguration> All { get; } = new List<FacilitatorRankConfiguration>
        {
            new FacilitatorRankConfiguration(FacilitatorRank.Bronze, 10, Money.Of(50m), "Bronze"),
            new FacilitatorRankConfiguration(FacilitatorRank.Gold, 20, Money.Of(250m), "Gold"),
            new FacilitatorRankConfiguration(FacilitatorRank.Pearl, 25, Money.Of(1250m), "Pearl"),
            new FacilitatorRankConfiguration(FacilitatorRank.Sapphire, 30, Money.Of(2500m), "Sapphire"),
            new FacilitatorRankConfiguration(FacilitatorRank.Ruby, 50, Money.Of(11250m), "Ruby"),
            new FacilitatorRankConfiguration(FacilitatorRank.Platinum, 60, Money.Of(41250m), "Platinum"),
            new FacilitatorRankConfiguration(FacilitatorRank.PremierT60, 60, Money.Of(68750m), "Premier T/60")
        };

        public static FacilitatorRankConfiguration For(FacilitatorRank rank)
            => All.FirstOrDefault(c => c.Rank == rank)
                ?? throw new System.ArgumentException($"No facilitator rank configuration exists for rank '{rank}'.", nameof(rank));
    }
}
