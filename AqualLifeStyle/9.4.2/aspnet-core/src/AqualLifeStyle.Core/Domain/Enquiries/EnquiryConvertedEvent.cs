using System;
using Abp.Events.Bus;

namespace AqualLifeStyle.Domain.Enquiries
{
    /// <summary>
    /// Raised when an enquiry is converted to a customer. Carries enough context for the network
    /// bounded context to attribute direct/indirect referrals without coupling to <see cref="Enquiry"/>.
    /// Triggered after the converting unit of work completes so handlers can safely re-query the
    /// converted enquiry and related tenant data.
    /// </summary>
    [Serializable]
    public class EnquiryConvertedEvent : EventData
    {
        public int? TenantId { get; }
        public int EnquiryId { get; }
        public int CustomerId { get; }
        public int ProductId { get; }
        public int? ReferredByFacilitatorId { get; }
        public DateTime ConvertedAt { get; }

        public EnquiryConvertedEvent(int enquiryId, int customerId, int productId, int? referredByFacilitatorId, DateTime convertedAt, int? tenantId = null)
        {
            TenantId = tenantId;
            EnquiryId = enquiryId;
            CustomerId = customerId;
            ProductId = productId;
            ReferredByFacilitatorId = referredByFacilitatorId;
            ConvertedAt = convertedAt;
        }
    }
}
