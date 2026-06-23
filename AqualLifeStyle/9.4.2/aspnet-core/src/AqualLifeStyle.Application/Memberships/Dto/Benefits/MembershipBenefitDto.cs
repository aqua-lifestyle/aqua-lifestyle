using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships.Dto.Benefits
{
    public class MembershipBenefitDto
    {
        public int Id { get; set; }
        public MembershipType MembershipType { get; set; }
        public string BenefitName { get; set; }
        public string Description { get; set; }
        public decimal? BenefitValue { get; set; }
        public bool IsActive { get; set; }
    }
}
