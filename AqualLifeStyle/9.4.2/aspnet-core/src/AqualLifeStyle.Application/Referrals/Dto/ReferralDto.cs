using System;

namespace AqualLifeStyle.Application.Referrals.Dto
{
    public class ReferralDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? ReferrerFacilitatorId { get; set; }
        public int? ReferrerAreaLeaderId { get; set; }
        public int ReferredCustomerId { get; set; }
        public int SourceEnquiryId { get; set; }
        public int Type { get; set; }
        public decimal AwardAmount { get; set; }
        public bool AwardIssued { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ConvertedAt { get; set; }
    }
}
