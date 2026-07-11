using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        public async Task CreateAsync_CreatesHostCustomer_WhenTenantContextIsMissing()
        {
            using (UsingTenantId(null))
            {
                await _customerAppService.CreateAsync(new CreateCustomerDto
                {
                    Name = "Host Customer",
                    Email = "host-customer@example.com"
                });
            }

            await UsingDbContextAsync(null, async ctx =>
            {
                var hostCustomer = await ctx.Customers.SingleAsync(c => c.Email.Value == "host-customer@example.com");
                hostCustomer.TenantId.ShouldBeNull();
            });
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyHostCustomers_WhenTenantContextIsMissing()
        {
            var hostEmail = "host-scope-customer@example.com";
            var tenantEmail = "tenant-scope-customer@example.com";

            await UsingDbContextAsync(null, async ctx =>
            {
                ctx.Customers.Add(Customer.Create(null, "Host Scope Customer", new EmailAddress(hostEmail)));
                await ctx.SaveChangesAsync();
            });

            await UsingDbContextAsync(1, async ctx =>
            {
                ctx.Customers.Add(Customer.Create(1, "Tenant Scope Customer", new EmailAddress(tenantEmail)));
                await ctx.SaveChangesAsync();
            });

            using (UsingTenantId(null))
            {
                var customers = await _customerAppService.GetAllAsync();

                customers.ShouldContain(c => c.Email == hostEmail);
                customers.ShouldNotContain(c => c.Email == tenantEmail);
            }
        }
    }
}
