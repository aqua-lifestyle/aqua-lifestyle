using System;
using System.Threading.Tasks;
using Abp.UI;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using Moq;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class CustomerAppServiceErrorHandlingTests
    {
        [Fact]
        public async Task CreateAsync_ThrowsUserFriendlyException_WhenMembershipIsInvalid()
        {
            var customerRepository = new Mock<ICustomerRepository>();
            var membershipRepository = new Mock<IMembershipRepository>();
            membershipRepository
                .Setup(x => x.GetAsync(It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("Membership not found."));

            var service = new CustomerAppService(customerRepository.Object, membershipRepository.Object);

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => service.CreateAsync(new CreateCustomerDto
            {
                Name = "Thabang",
                Email = "molape@example.com",
                MembershipId = 1
            }));

            Assert.Contains("Customer creation failed", ex.Message);
            Assert.Contains("Membership not found", ex.Details);
        }
    }
}
