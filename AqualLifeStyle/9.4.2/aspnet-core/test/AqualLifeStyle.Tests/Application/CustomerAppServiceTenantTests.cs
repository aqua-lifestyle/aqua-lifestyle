using System.Threading.Tasks;
using Abp.Authorization;
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
        public async Task CreateAsync_ThrowsAuthorizationException_WhenTenantContextIsMissing()
        {
            using (UsingTenantId(null))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _customerAppService.CreateAsync(new CreateCustomerDto
                    {
                        Name = "Host Customer",
                        Email = "host-customer@example.com"
                    }));
            }
        }

        [Fact]
        public async Task GetAllAsync_ThrowsAuthorizationException_WhenTenantContextIsMissing()
        {
            using (UsingTenantId(null))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _customerAppService.GetAllAsync());
            }
        }

        [Fact]
        public async Task GetAsync_ThrowsAuthorizationException_WhenTenantContextIsMissing()
        {
            var customerId = 0;

            await UsingDbContextAsync(1, async ctx =>
            {
                var customer = Customer.Create(1, "Tenant Customer", new EmailAddress("tenant-customer@example.com"));
                ctx.Customers.Add(customer);
                await ctx.SaveChangesAsync();
                customerId = customer.Id;
            });

            using (UsingTenantId(null))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _customerAppService.GetAsync(customerId));
            }
        }
    }
}
