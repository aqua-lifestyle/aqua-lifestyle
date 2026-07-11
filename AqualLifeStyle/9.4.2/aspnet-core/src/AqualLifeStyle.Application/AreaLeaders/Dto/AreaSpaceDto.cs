using System;

namespace AqualLifeStyle.Application.AreaLeaders.Dto
{
    public class AreaSpaceDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int AreaLeaderId { get; set; }
        public string AddressLine { get; set; }
        public string Capacity { get; set; }
        public int InterestedMembers { get; set; }
        public int Status { get; set; }
        public DateTime? ReviewStartedAt { get; set; }
        public int PresentationsCompleted { get; set; }
        public int StartupOrdersCompleted { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
