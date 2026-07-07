using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Products;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class ProductEligibilityTests
    {
        [Fact]
        public async Task CanViewProduct_AllowsStandardProductForStandardMember()
        {
            var membership = Membership.Create("Jasper", "Base access", MembershipType.Jasper);
            var customer = Customer.Create("Alicia", new EmailAddress("alicia@example.com"), 1);
            var product = Product.Create("Basic Plan", 20m, 1);
            var manager = new ProductEligibilityManager(new StubMembershipRepository(membership));

            Assert.True(await manager.CanViewProductAsync(customer, product));
        }

        [Fact]
        public async Task CanViewProduct_DeniesProductForInactiveMembership()
        {
            var membership = Membership.Create("Onyx", "Onyx access", MembershipType.Onyx);
            membership.Deactivate();
            var customer = Customer.Create("Alicia", new EmailAddress("alicia@example.com"), 1);
            var product = Product.Create("Premium Plan", 50m, 2);
            var manager = new ProductEligibilityManager(new StubMembershipRepository(membership));

            Assert.False(await manager.CanViewProductAsync(customer, product));
        }

        [Fact]
        public async Task CanViewProduct_DeniesMembershipRestrictedProductWithoutMembership()
        {
            var customer = Customer.Create("Alicia", new EmailAddress("alicia@example.com"));
            var product = Product.Create("Premium Plan", 50m, 2);
            var manager = new ProductEligibilityManager(new StubMembershipRepository(Membership.Create("Jasper", "Base access", MembershipType.Jasper)));

            Assert.False(await manager.CanViewProductAsync(customer, product));
        }

        #nullable disable
        private sealed class StubMembershipRepository : IMembershipLookup
        {
            private readonly Membership _membership;

            public StubMembershipRepository(Membership membership)
            {
                _membership = membership;
            }

            public Task<Membership> GetAsync(int id) => Task.FromResult(_membership);
        }
        #nullable restore
    }
}
