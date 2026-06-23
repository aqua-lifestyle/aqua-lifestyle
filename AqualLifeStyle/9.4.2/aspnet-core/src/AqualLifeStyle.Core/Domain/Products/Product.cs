using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.Products
{
    public class Product : Entity<int>
    {
        public string Name { get; private set; }
        public decimal Price { get; private set; }
        public bool IsActive { get; private set; }
        public int? MembershipId { get; private set; }

        protected Product() { }

        private Product(string name, decimal price, int? membershipId = null, bool isActive = true)
        {
            SetName(name);
            SetPrice(price);
            MembershipId = membershipId;
            IsActive = isActive;
        }

        public static Product Create(string name, decimal price, int? membershipId = null)
            => new Product(name, price, membershipId, true);

        public void UpdatePrice(decimal price) => SetPrice(price);

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;

        private void SetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Product name is required.", nameof(name));
            Name = name.Trim();
        }

        private void SetPrice(decimal price)
        {
            if (price <= 0) throw new ArgumentException("Price must be greater than zero.", nameof(price));
            Price = price;
        }
    }
}
