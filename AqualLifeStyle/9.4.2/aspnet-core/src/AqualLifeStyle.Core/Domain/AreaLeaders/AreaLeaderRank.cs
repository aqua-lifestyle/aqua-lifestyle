namespace AqualLifeStyle.Domain.AreaLeaders
{
    /// <summary>
    /// Area Leader ranking tiers, ordered from entry (Ruby) to top (Ambassador).
    /// Thresholds are cumulative order targets (per <c>docs/BusinessDocs/workflows.md</c> §6).
    /// </summary>
    public enum AreaLeaderRank
    {
        Ruby = 0,
        Emerald = 1,
        Premier = 2,
        Diamond = 3,
        VIP = 4,
        Presidential = 5,
        ChairmansCircle = 6,
        Ambassador = 7
    }
}
