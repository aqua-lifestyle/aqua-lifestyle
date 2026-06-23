using System;
using System.Threading.Tasks;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using Moq;
using Abp.ObjectMapping;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class CustomerAppServiceErrorHandlingTests
    {
        [Fact]
        public async Task CreateAsync_ThrowsUserFriendlyException_WhenMembershipIsInvalid()
        {
            var customerRepository = new Mock<ICustomerRepository>();
            var objectMapperMock = new Mock<IObjectMapper>();
            var membershipRepository = new Mock<IMembershipRepository>();
            membershipRepository
                .Setup(x => x.GetAsync(It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("Membership not found."));

            var service = new CustomerAppService(customerRepository.Object, membershipRepository.Object, objectMapperMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1 && s.UserId == 1)
            };

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
