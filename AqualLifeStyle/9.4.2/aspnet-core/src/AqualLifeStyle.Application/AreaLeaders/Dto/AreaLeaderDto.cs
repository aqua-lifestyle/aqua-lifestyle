namespace AqualLifeStyle.Application.AreaLeaders.Dto
{
    public class AreaLeaderDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public int LicenseType { get; set; }
        public decimal LicenseFee { get; set; }
        public int Rank { get; set; }
        public int? AreaSpaceId { get; set; }
        public decimal MonthlySubscription { get; set; }
        public int DirectReferrals { get; set; }
        public int IndirectReferrals { get; set; }
        public int OrderTarget { get; set; }
    }
}
