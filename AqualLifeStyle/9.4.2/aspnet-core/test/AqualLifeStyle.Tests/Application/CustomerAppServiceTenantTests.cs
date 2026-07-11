using System.Threading.Tasks;
using Abp.UI;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class CustomerAppServiceTenantTests : AqualLifeStyleTestBase
    {
        private readonly ICustomerAppService _customerAppService;

        public CustomerAppServiceTenantTests()
        {
            _customerAppService = Resolve<ICustomerAppService>();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentTenantCustomers()
        {
            var tenantOneEmail = "tenant1-customer@example.com";
            var tenantTwoEmail = "tenant2-customer@example.com";

            await UsingDbContextAsync(1, async ctx =>
            {
                ctx.Customers.Add(Customer.Create(1, "Tenant One Customer", new EmailAddress(tenantOneEmail)));
                await ctx.SaveChangesAsync();
            });

            await UsingDbContextAsync(2, async ctx =>
            {
                ctx.Customers.Add(Customer.Create(2, "Tenant Two Customer", new EmailAddress(tenantTwoEmail)));
                await ctx.SaveChangesAsync();
            });

            using (UsingTenantId(1))
            {
                var customers = await _customerAppService.GetAllAsync();

                customers.ShouldContain(c => c.Email == tenantOneEmail);
                customers.ShouldNotContain(c => c.Email == tenantTwoEmail);
            }
        }

        [Fact]
        public async Task CreateAsync_Throws_WhenTenantContextIsMissing()
        {
            UserFriendlyException ex;

            using (UsingTenantId(null))
            {
                ex = await Should.ThrowAsync<UserFriendlyException>(() => _customerAppService.CreateAsync(new CreateCustomerDto
                {
                    Name = "Host Customer",
                    Email = "host-customer@example.com"
                }));
            }

            ex.Message.ShouldBe("Customer creation failed.");
            ex.Details.ShouldBe("A tenant context is required.");
        }

        [Fact]
        public async Task GetAllAsync_Throws_WhenTenantContextIsMissing()
        {
            UserFriendlyException ex;

            using (UsingTenantId(null))
            {
                ex = await Should.ThrowAsync<UserFriendlyException>(() => _customerAppService.GetAllAsync());
            }

            ex.Message.ShouldBe("Customer lookup failed.");
            ex.Details.ShouldBe("A tenant context is required.");
        }
    }
}
