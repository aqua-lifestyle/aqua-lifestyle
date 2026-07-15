using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Shouldly;

namespace AqualLifeStyle.Tests.Domain
{
    public class CustomerTests
    {
        [Fact]
        public void Customer_IsAuditedAndSoftDeletable()
        {
            typeof(FullAuditedAggregateRoot<int>).IsAssignableFrom(typeof(Customer)).ShouldBeTrue();
            typeof(ISoftDelete).IsAssignableFrom(typeof(Customer)).ShouldBeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_WithInvalidTenantId_Throws(int tenantId)
        {
            Should.Throw<System.ArgumentException>(() =>
                Customer.Create(tenantId, 42, "Jane Doe", new EmailAddress("jane@example.com")));
        }

        [Fact]
        public void Create_WithNullTenantId_AllowsCustomer()
        {
            var customer = Customer.Create(null, 43, "Jane Doe", new EmailAddress("jane@example.com"));

            customer.TenantId.ShouldBeNull();
            customer.Name.ShouldBe("Jane Doe");
            customer.Email.Value.ShouldBe("jane@example.com");
            customer.IsActive.ShouldBeTrue();
        }

        [Fact]
        public void LinkUser_WithValidUserId_CreatesImmutableLink()
        {
            var customer = Customer.Create(1, 44, "Jane Doe", new EmailAddress("jane@example.com"));
            customer.LinkUser(44);
            customer.UserId.ShouldBe(44);

            var anotherCustomer = Customer.Create(1, 45, "Jane Doe", new EmailAddress("jane@example.com"));
            Should.Throw<System.InvalidOperationException>(() => anotherCustomer.LinkUser(42));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void LinkUser_WithInvalidUserId_Throws(long userId)
        {
            var customer = Customer.Create(1, 45, "Jane Doe", new EmailAddress("jane@example.com"));
            Should.Throw<System.ArgumentException>(() => customer.LinkUser(userId));
        }
    }
}
