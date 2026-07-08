using System;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Orders
{
    public class OrderIntent : Entity<int>
    {
        public int CustomerId { get; private set; }
        public int ProductId { get; private set; }
        public int? EnquiryId { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal ReservedPrice { get; private set; }
        public OrderIntentStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ReservedAt { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        protected OrderIntent()
        {
        }

        private OrderIntent(int customerId, int productId, int? enquiryId, decimal unitPrice, decimal reservedPrice, DateTime createdAt)
        {
            if (customerId <= 0) throw new ArgumentException("CustomerId must be valid.", nameof(customerId));
            if (productId <= 0) throw new ArgumentException("ProductId must be valid.", nameof(productId));
            if (enquiryId.HasValue && enquiryId.Value <= 0) throw new ArgumentException("EnquiryId must be valid.", nameof(enquiryId));
            if (unitPrice <= 0) throw new ArgumentException("Unit price must be greater than zero.", nameof(unitPrice));
            if (reservedPrice <= 0) throw new ArgumentException("Reserved price must be greater than zero.", nameof(reservedPrice));
            if (createdAt == default) throw new ArgumentException("Created date must be valid.", nameof(createdAt));

            CustomerId = customerId;
            ProductId = productId;
            EnquiryId = enquiryId;
            UnitPrice = unitPrice;
            ReservedPrice = reservedPrice;
            CreatedAt = createdAt;
            Status = OrderIntentStatus.Draft;
        }

        public static OrderIntent CreateDraft(int customerId, int productId, int? enquiryId, decimal unitPrice, decimal reservedPrice, DateTime createdAt)
        {
            return new OrderIntent(customerId, productId, enquiryId, unitPrice, reservedPrice, createdAt);
        }

        public static OrderIntent CreateReserved(int customerId, int productId, int? enquiryId, decimal unitPrice, decimal reservedPrice, DateTime reservedAt)
        {
            var orderIntent = new OrderIntent(customerId, productId, enquiryId, unitPrice, reservedPrice, reservedAt);
            orderIntent.Reserve(reservedAt);
            return orderIntent;
        }

        public void Reserve(DateTime reservedAt)
        {
            if (Status != OrderIntentStatus.Draft)
            {
                throw new InvalidOperationException("Only draft order intents can be reserved.");
            }

            if (reservedAt == default)
            {
                throw new ArgumentException("Reservation date must be valid.", nameof(reservedAt));
            }

            Status = OrderIntentStatus.Reserved;
            ReservedAt = reservedAt;
        }

        public void Cancel(DateTime cancelledAt)
        {
            if (Status == OrderIntentStatus.Cancelled)
            {
                return;
            }

            if (Status == OrderIntentStatus.Completed)
            {
                throw new InvalidOperationException("Completed order intents cannot be cancelled.");
            }

            if (cancelledAt == default)
            {
                throw new ArgumentException("Cancellation date must be valid.", nameof(cancelledAt));
            }

            Status = OrderIntentStatus.Cancelled;
            CancelledAt = cancelledAt;
        }

        public void Complete(DateTime completedAt)
        {
            if (Status != OrderIntentStatus.Reserved)
            {
                throw new InvalidOperationException("Only reserved order intents can be completed.");
            }

            if (completedAt == default)
            {
                throw new ArgumentException("Completion date must be valid.", nameof(completedAt));
            }

            Status = OrderIntentStatus.Completed;
            CompletedAt = completedAt;
        }
    }
}
