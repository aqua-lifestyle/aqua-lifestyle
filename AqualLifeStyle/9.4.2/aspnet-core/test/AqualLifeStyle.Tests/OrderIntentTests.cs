using System;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Orders;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class OrderIntentTests
    {
        [Fact]
        public void CreateReserved_WithValidInput_ReservesOrderIntent()
        {
            var now = DateTime.UtcNow;

            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, now);

            Assert.Equal(1, orderIntent.CustomerId);
            Assert.Equal(2, orderIntent.ProductId);
            Assert.Equal(3, orderIntent.EnquiryId);
            Assert.Equal(100m, orderIntent.UnitPrice);
            Assert.Equal(95m, orderIntent.ReservedPrice);
            Assert.Equal(OrderIntentStatus.Reserved, orderIntent.Status);
            Assert.Equal(now, orderIntent.ReservedAt);
        }

        [Fact]
        public void Complete_WithReservedOrderIntent_CompletesOrderIntent()
        {
            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, DateTime.UtcNow);
            var completedAt = DateTime.UtcNow.AddMinutes(5);

            orderIntent.Complete(completedAt);

            Assert.Equal(OrderIntentStatus.Completed, orderIntent.Status);
            Assert.Equal(completedAt, orderIntent.CompletedAt);
        }

        [Fact]
        public void Cancel_WithCompletedOrderIntent_Throws()
        {
            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, DateTime.UtcNow);
            orderIntent.Complete(DateTime.UtcNow.AddMinutes(5));

            Assert.Throws<InvalidOperationException>(() => orderIntent.Cancel(DateTime.UtcNow.AddMinutes(10)));
        }
    }
}
