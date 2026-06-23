using System;

namespace AqualLifeStyle.Application.Orders.Dto
{
    public class OrderIntentDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public int? EnquiryId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ReservedPrice { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReservedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
