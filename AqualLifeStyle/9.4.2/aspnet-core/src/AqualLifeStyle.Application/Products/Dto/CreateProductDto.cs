using System;

namespace AqualLifeStyle.Application.Products.Dto
{
    public class CreateProductDto
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int? MembershipId { get; set; }
    }
}
