using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships.Dto
{
    public class MembershipDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public MembershipType MembershipType { get; set; }
        public string? ActivationDate { get; set; }
        public decimal MonthlyObligationAmount { get; set; }
        public string? LastObligationMetDate { get; set; }
    }
}
