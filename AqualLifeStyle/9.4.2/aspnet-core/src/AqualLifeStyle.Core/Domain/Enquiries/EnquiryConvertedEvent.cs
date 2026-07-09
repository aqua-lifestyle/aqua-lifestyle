using System;
using Abp.Events.Bus;

namespace AqualLifeStyle.Domain.Enquiries
{
    /// <summary>
    /// Raised when an enquiry is converted to a customer. Carries enough context for the network
    /// bounded context to attribute direct/indirect referrals without coupling to <see cref="Enquiry"/>.
    /// Triggered by the application layer (the existing <c>Enquiry</c> aggregate is intentionally
    /// not retrofitted to <c>AggregateRoot</c> — see ADR-001).
    /// </summary>
    [Serializable]
    public class EnquiryConvertedEvent : EventData
    {
        public int EnquiryId { get; }
        public int CustomerId { get; }
        public int ProductId { get; }
        public int? ReferredByFacilitatorId { get; }
        public DateTime ConvertedAt { get; }

        public EnquiryConvertedEvent(int enquiryId, int customerId, int productId, int? referredByFacilitatorId, DateTime convertedAt)
        {
            EnquiryId = enquiryId;
            CustomerId = customerId;
            ProductId = productId;
            ReferredByFacilitatorId = referredByFacilitatorId;
            ConvertedAt = convertedAt;
        }
    }
}
