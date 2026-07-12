namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Facilitator ranking tiers, ordered from entry (Bronze) to final (PremierT60).
    /// Thresholds and awards are defined in <see cref="FacilitatorRankConfiguration"/>.
    /// </summary>
    public enum FacilitatorRank
    {
        Bronze = 0,
        Gold = 1,
        Pearl = 2,
        Sapphire = 3,
        Ruby = 4,
        Platinum = 5,
        PremierT60 = 6
    }
}
