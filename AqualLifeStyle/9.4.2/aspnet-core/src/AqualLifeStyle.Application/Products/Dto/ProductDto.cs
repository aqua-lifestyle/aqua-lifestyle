using System;

namespace AqualLifeStyle.Application.Products.Dto
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }
}
