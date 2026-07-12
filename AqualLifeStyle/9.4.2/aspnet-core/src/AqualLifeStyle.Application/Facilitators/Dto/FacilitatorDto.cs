namespace AqualLifeStyle.Application.Facilitators.Dto
{
    public class FacilitatorDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public int AreaLeaderId { get; set; }
        public int Rank { get; set; }
        public int DirectReferrals { get; set; }
        public int IndirectReferrals { get; set; }
        public decimal AwardBalance { get; set; }
    }
}
