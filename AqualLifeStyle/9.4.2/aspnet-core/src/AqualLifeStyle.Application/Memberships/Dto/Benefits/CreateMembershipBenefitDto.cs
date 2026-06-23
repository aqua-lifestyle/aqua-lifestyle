using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships.Dto.Benefits
{
    public class CreateMembershipBenefitDto
    {
        public MembershipType MembershipType { get; set; }
        public string BenefitName { get; set; }
        public string Description { get; set; }
        public decimal? BenefitValue { get; set; }
    }
}
