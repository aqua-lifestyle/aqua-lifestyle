using System.Threading.Tasks;
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
            var customer = Customer.Create("Old Name", new EmailAddress("old@example.com"), 1);
            var membership = Membership.Create("Premium", "Premium membership", MembershipType.Premium);

            var customerRepo = new Mock<ICustomerRepository>();
            customerRepo.Setup(r => r.GetAsync(10)).ReturnsAsync(customer);
            customerRepo.Setup(r => r.UpdateAsync(customer)).ReturnsAsync(customer);

            var membershipRepo = new Mock<IMembershipRepository>();
            membershipRepo.Setup(r => r.GetAsync(2)).ReturnsAsync(membership);

            var appService = new CustomerAppService(customerRepo.Object, membershipRepo.Object);

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
    }
}
