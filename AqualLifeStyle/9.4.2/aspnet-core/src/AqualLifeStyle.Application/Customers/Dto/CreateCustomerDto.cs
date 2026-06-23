namespace AqualLifeStyle.Application.Customers.Dto
{
    public class CreateCustomerDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public int? MembershipId { get; set; }
    }
}
