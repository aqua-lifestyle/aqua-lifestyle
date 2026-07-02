using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships.Dto
{
    public class CreateMembershipDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public MembershipType MembershipType { get; set; } = MembershipType.Standard;
    }

    public class SetMembershipActivationDto
    {
        public string ActivationDate { get; set; }
    }

    public class SetMonthlyObligationDto
    {
        public decimal Amount { get; set; }
    }

    public class MarkObligationMetDto
    {
        public string AsOfDate { get; set; }
    }
}
