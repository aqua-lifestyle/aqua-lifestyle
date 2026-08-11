using System;

namespace AqualLifeStyle.Application.Customers.Dto
{
    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
        public Guid? AreaId { get; set; }
        public string AreaName { get; set; }
    }
}
