using System.Threading.Tasks;
using System.Linq.Expressions;
using Abp.Authorization;
using Abp.Runtime.Session;
using Moq;
using Abp.ObjectMapping;
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
            var customer = Customer.Create(1, 50, "Old Name", new EmailAddress("old@example.com"), 1);
            customer.Id = 10;
            var membership = Membership.Create(1, "Onyx", "Onyx membership", MembershipType.Onyx);

            var customerRepo = new Mock<ICustomerRepository>();
            customerRepo
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<Customer, bool>>>()))
                .ReturnsAsync(customer);
            customerRepo.Setup(r => r.UpdateAsync(customer)).ReturnsAsync(customer);

            var membershipRepo = new Mock<IMembershipRepository>();
            membershipRepo.Setup(r => r.GetAsync(2)).ReturnsAsync(membership);

            var objectMapperMock = new Mock<IObjectMapper>();
            objectMapperMock
                .Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>()))
                .Returns((Customer c) => new CustomerDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email.Value,
                    MembershipId = c.MembershipId,
                    IsActive = c.IsActive
                });

            var appService = new CustomerAppService(customerRepo.Object, membershipRepo.Object, objectMapperMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1 && s.UserId == 50)
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
        public async Task CreateAsync_Throws_WhenTenantContextIsMissing()
        {
            var customerRepo = new Mock<ICustomerRepository>();
            var membershipRepo = new Mock<IMembershipRepository>();
            var objectMapperMock = new Mock<IObjectMapper>();

            var appService = new CustomerAppService(customerRepo.Object, membershipRepo.Object, objectMapperMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == (int?)null)
            };

            var ex = await Assert.ThrowsAsync<AbpAuthorizationException>(() => appService.CreateAsync(new CreateCustomerDto
            {
                Name = "Host Customer",
                Email = "host@example.com"
            }));

            Assert.Equal("Customer creation failed. A tenant context is required.", ex.Message);
            customerRepo.Verify(r => r.ExistsByEmailAsync(It.IsAny<string>()), Times.Never);
            customerRepo.Verify(r => r.InsertAsync(It.IsAny<Customer>()), Times.Never);
        }
    }
}
