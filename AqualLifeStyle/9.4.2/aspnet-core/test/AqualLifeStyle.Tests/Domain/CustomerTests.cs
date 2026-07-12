using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Shouldly;

namespace AqualLifeStyle.Tests.Domain
{
    public class CustomerTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_WithInvalidTenantId_Throws(int tenantId)
        {
            Should.Throw<System.ArgumentException>(() =>
                Customer.Create(tenantId, "Jane Doe", new EmailAddress("jane@example.com")));
        }

        [Fact]
        public void Create_WithNullTenantId_AllowsCustomer()
        {
            var customer = Customer.Create(null, "Jane Doe", new EmailAddress("jane@example.com"));

            customer.TenantId.ShouldBeNull();
            customer.Name.ShouldBe("Jane Doe");
            customer.Email.Value.ShouldBe("jane@example.com");
            customer.IsActive.ShouldBeTrue();
        }

        [Fact]
        public void LinkUser_WithValidUserId_CreatesImmutableLink()
        {
            var customer = Customer.Create(1, "Jane Doe", new EmailAddress("jane@example.com"));
            customer.LinkUser(42);

            customer.UserId.ShouldBe(42);
            Should.Throw<System.InvalidOperationException>(() => customer.LinkUser(43));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void LinkUser_WithInvalidUserId_Throws(long userId)
        {
            var customer = Customer.Create(1, "Jane Doe", new EmailAddress("jane@example.com"));
            Should.Throw<System.ArgumentException>(() => customer.LinkUser(userId));
        }
    }
}
