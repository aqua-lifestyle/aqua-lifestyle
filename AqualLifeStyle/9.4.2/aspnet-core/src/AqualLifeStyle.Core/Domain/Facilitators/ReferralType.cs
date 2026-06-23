namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// Whether a referral was generated directly by a facilitator, or indirectly via the
    /// facilitator's upline area leader.
    /// </summary>
    public enum ReferralType
    {
        Direct = 0,
        Indirect = 1
    }
}
