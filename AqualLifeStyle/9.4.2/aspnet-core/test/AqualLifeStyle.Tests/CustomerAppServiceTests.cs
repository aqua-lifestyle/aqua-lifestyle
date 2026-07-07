using System;
using System.Threading.Tasks;
using Abp.UI;
using NSubstitute;
using Xunit;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Tests
{
    public class CustomerAppServiceTests
    {
        [Fact]
        public async Task CreateAsync_ThrowsUserFriendlyException_WhenEmailAlreadyExists()
        {
            var repo = Substitute.For<ICustomerRepository>();
            var membershipRepo = Substitute.For<IMembershipRepository>();
            repo.ExistsByEmailAsync("Mathanda@gmail.com").Returns(true);

            var svc = new CustomerAppService(repo, membershipRepo);
            var input = new CreateCustomerDto { Name = "Thandaza", Email = "Mathanda@gmail.com" };

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => svc.CreateAsync(input));

            Assert.Equal("Customer creation failed.", ex.Message);
            Assert.Equal("A customer with that email already exists.", ex.Details);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsUserFriendlyException_WhenEmailAlreadyExistsOnAnotherCustomer()
        {
            var repo = Substitute.For<ICustomerRepository>();
            var membershipRepo = Substitute.For<IMembershipRepository>();
            repo.ExistsByEmailAsync("Mathanda@gmail.com", 5).Returns(true);
            repo.GetAsync(5).Returns(Customer.Create("Thandaza", new EmailAddress("other@example.com"), null));

            var svc = new CustomerAppService(repo, membershipRepo);
            var input = new CustomerDto { Id = 5, Name = "Thandaza", Email = "Mathanda@gmail.com", MembershipId = null, IsActive = true };

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => svc.UpdateAsync(input));

            Assert.Equal("Customer update failed.", ex.Message);
            Assert.Equal("A customer with that email already exists.", ex.Details);
        }
    }
}
