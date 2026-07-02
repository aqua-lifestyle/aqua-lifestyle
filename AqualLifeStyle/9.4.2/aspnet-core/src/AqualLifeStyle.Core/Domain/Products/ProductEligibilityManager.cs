using System;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Domain.Products
{
    public class ProductEligibilityManager
    {
        private readonly IMembershipLookup _membershipLookup;

        public ProductEligibilityManager(IMembershipLookup membershipLookup)
        {
            _membershipLookup = membershipLookup ?? throw new ArgumentNullException(nameof(membershipLookup));
        }

        public async Task<bool> CanViewProductAsync(Customer customer, Product product)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (product == null) throw new ArgumentNullException(nameof(product));

            if (!customer.IsActive) return false;
            if (!product.IsActive) return false;
            if (product.MembershipId == null) return true;
            if (!customer.MembershipId.HasValue) return false;

            var membership = await _membershipLookup.GetAsync(customer.MembershipId.Value);
            return await CanViewProductAsync(customer, product, membership);
        }

        public Task<bool> CanViewProductAsync(Customer customer, Product product, Membership membership)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (product == null) throw new ArgumentNullException(nameof(product));

            if (!customer.IsActive) return Task.FromResult(false);
            if (!product.IsActive) return Task.FromResult(false);
            if (product.MembershipId == null) return Task.FromResult(true);
            if (!customer.MembershipId.HasValue) return Task.FromResult(false);
            if (membership == null || !membership.IsActive) return Task.FromResult(false);

            return Task.FromResult(IsMembershipEligible(membership.MembershipType, product.MembershipId.Value));
        }

        public bool CanViewProduct(Customer customer, Product product)
        {
            return CanViewProductAsync(customer, product).GetAwaiter().GetResult();
        }

        public bool CanViewProduct(Customer customer, Product product, Membership membership)
        {
            return CanViewProductAsync(customer, product, membership).GetAwaiter().GetResult();
        }

        private static bool IsMembershipEligible(MembershipType membershipType, int requiredMembershipId)
        {
            return membershipType switch
            {
                MembershipType.Premium => requiredMembershipId <= 2,
                MembershipType.Vip => requiredMembershipId <= 3,
                _ => requiredMembershipId == 1
            };
        }
    }
}
