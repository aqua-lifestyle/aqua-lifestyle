using System;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Payments
{
    /// <summary>
    /// Provider-neutral payment facts produced only after an integration adapter
    /// has verified the payment provider's callback.
    /// </summary>
    public sealed class ConfirmedProgrammePayment
    {
        public int TenantId { get; }
        public int CustomerId { get; }
        public MemberPaymentPurpose Purpose { get; }
        public decimal Amount { get; }
        public string Currency { get; }
        public string Provider { get; }
        public string ExternalReference { get; }
        public DateTime InitiatedAt { get; }
        public DateTime ConfirmedAt { get; }

        public ConfirmedProgrammePayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string currency,
            string provider,
            string externalReference,
            DateTime initiatedAt,
            DateTime confirmedAt)
        {
            TenantId = tenantId;
            CustomerId = customerId;
            Purpose = purpose;
            Amount = amount;
            Currency = currency;
            Provider = provider;
            ExternalReference = externalReference;
            InitiatedAt = initiatedAt;
            ConfirmedAt = confirmedAt;
        }
    }
}
