using System;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Orders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Orders;
using AqualLifeStyle.Domain.Products;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class OrderIntentAppServiceTenantTests : AqualLifeStyleTestBase
    {
        private readonly IOrderIntentAppService _orderIntentAppService;

        public OrderIntentAppServiceTenantTests()
            => _orderIntentAppService = Resolve<IOrderIntentAppService>();

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyOrdersForCustomersInTheCurrentArea()
        {
            var productId = await CreateProductAsync();
            var areaOneOrderId = await CreateOrderAsync(1, productId);
            var areaTwoOrderId = await CreateOrderAsync(2, productId);

            using (UsingTenantId(1))
            {
                var orders = await _orderIntentAppService.GetAllAsync();

                orders.ShouldContain(order => order.Id == areaOneOrderId);
                orders.ShouldNotContain(order => order.Id == areaTwoOrderId);
            }
        }

        [Fact]
        public async Task GetAsync_WhenOrderBelongsToAnotherArea_ReturnsNotFound()
        {
            var productId = await CreateProductAsync();
            var areaTwoOrderId = await CreateOrderAsync(2, productId);

            using (UsingTenantId(1))
            {
                await Should.ThrowAsync<AqualLifeStyleNotFoundException>(() =>
                    _orderIntentAppService.GetAsync(areaTwoOrderId));
            }
        }

        private async Task<int> CreateProductAsync()
        {
            return await UsingDbContextAsync(1, async context =>
            {
                var product = Product.Create($"Area-scoped order test {Guid.NewGuid():N}", 100m);
                context.Products.Add(product);
                await context.SaveChangesAsync();
                return product.Id;
            });
        }

        private async Task<int> CreateOrderAsync(int tenantId, int productId)
        {
            var userId = await CreateTestUserAsync(
                tenantId,
                $"order-user-{Guid.NewGuid():N}",
                $"order-user-{Guid.NewGuid():N}@example.com");

            return await UsingDbContextAsync(tenantId, async context =>
            {
                var customer = Customer.Create(
                    tenantId,
                    userId,
                    $"Area {tenantId} customer",
                    new EmailAddress($"area-{tenantId}-{Guid.NewGuid():N}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var order = OrderIntent.CreateReserved(
                    customer.Id,
                    productId,
                    enquiryId: null,
                    unitPrice: 100m,
                    reservedPrice: 100m,
                    reservedAt: DateTime.UtcNow);
                context.OrderIntents.Add(order);
                await context.SaveChangesAsync();
                return order.Id;
            });
        }
    }
}
