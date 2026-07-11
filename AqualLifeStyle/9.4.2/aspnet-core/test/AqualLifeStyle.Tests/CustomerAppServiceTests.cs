using System.Threading.Tasks;
using System.Linq.Expressions;
using Abp.Runtime.Session;
using Moq;
using Xunit;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Tests
{
    public class CustomerAppServiceTests
    {
        [Fact]
        public async Task UpdateAsync_ChangesNameEmailMembershipAndStatus()
        {
            var customer = Customer.Create(1, "Old Name", new EmailAddress("old@example.com"), 1);
            customer.Id = 10;
            var membership = Membership.Create(1, "Onyx", "Onyx membership", MembershipType.Onyx);

            var customerRepo = new Mock<ICustomerRepository>();
            customerRepo
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<Customer, bool>>>()))
                .ReturnsAsync(customer);
            customerRepo.Setup(r => r.UpdateAsync(customer)).ReturnsAsync(customer);

            var membershipRepo = new Mock<IMembershipRepository>();
            membershipRepo.Setup(r => r.GetAsync(2)).ReturnsAsync(membership);

            var appService = new CustomerAppService(customerRepo.Object, membershipRepo.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1)
            };

            var input = new CustomerDto
            {
                Id = 10,
                Name = "New Name",
                Email = "new@example.com",
                MembershipId = 2,
                IsActive = false
            };

            var result = await appService.UpdateAsync(input);

            Assert.Equal("New Name", result.Name);
            Assert.Equal("new@example.com", result.Email);
            Assert.Equal(2, result.MembershipId);
            Assert.False(result.IsActive);
            customerRepo.Verify(r => r.UpdateAsync(It.Is<Customer>(c => c.Name == "New Name" && c.Email.Value == "new@example.com" && c.MembershipId == 2 && c.IsActive == false)), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_CreatesHostCustomer_WhenTenantContextIsMissing()
        {
            var customerRepo = new Mock<ICustomerRepository>();
            var membershipRepo = new Mock<IMembershipRepository>();
            Customer insertedCustomer = null;

            customerRepo.Setup(r => r.ExistsByEmailAsync("host@example.com")).ReturnsAsync(false);
            customerRepo
                .Setup(r => r.InsertAsync(It.IsAny<Customer>()))
                .Callback<Customer>(customer => insertedCustomer = customer)
                .ReturnsAsync((Customer customer) => customer);

            var appService = new CustomerAppService(customerRepo.Object, membershipRepo.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == (int?)null)
            };

            await appService.CreateAsync(new CreateCustomerDto
            {
                Name = "Host Customer",
                Email = "host@example.com"
            });

            Assert.NotNull(insertedCustomer);
            Assert.Null(insertedCustomer.TenantId);
            Assert.Equal("Host Customer", insertedCustomer.Name);
            Assert.Equal("host@example.com", insertedCustomer.Email.Value);
            customerRepo.Verify(r => r.ExistsByEmailAsync("host@example.com"), Times.Once);
            customerRepo.Verify(r => r.InsertAsync(It.IsAny<Customer>()), Times.Once);
        }
    }
}
