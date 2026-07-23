using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Payments
{
    public enum MemberPaymentPurpose
    {
        EntryRegistration = 0,
        EntryActivation = 1,
        OnyxDirectEntry = 2,
        EntryMonthlyCommitment = 3,
        OnyxFundingRepayment = 4,
        OnyxRental = 5,
        SavingsContribution = 6
    }

    public enum MemberPaymentStatus
    {
        Pending = 0,
        Confirmed = 1
    }

    public class MemberPayment : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public MemberPaymentPurpose Purpose { get; private set; }
        public decimal Amount { get; private set; }
        public string Currency { get; private set; }
        public string Provider { get; private set; }
        public string ExternalReference { get; private set; }
        public MemberPaymentStatus Status { get; private set; }
        public DateTime InitiatedAt { get; private set; }
        public DateTime? ConfirmedAt { get; private set; }

        protected MemberPayment()
        {
        }

        private MemberPayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string currency,
            string provider,
            string externalReference,
            DateTime initiatedAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
            if (initiatedAt == default) throw new ArgumentException("An initiation time is required.", nameof(initiatedAt));

            TenantId = tenantId;
            CustomerId = customerId;
            Purpose = purpose;
            Amount = amount;
            Currency = NormalizeCurrency(currency);
            Provider = RequireText(provider, nameof(provider), 64).ToUpperInvariant();
            ExternalReference = RequireText(externalReference, nameof(externalReference), 128);
            InitiatedAt = initiatedAt;
            Status = MemberPaymentStatus.Pending;
        }

        public static MemberPayment CreatePending(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string provider,
            string externalReference,
            DateTime initiatedAt,
            string currency = "ZAR")
        {
            return new MemberPayment(
                tenantId,
                customerId,
                purpose,
                amount,
                currency,
                provider,
                externalReference,
                initiatedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public void Confirm(DateTime confirmedAt)
        {
            if (Status == MemberPaymentStatus.Confirmed)
            {
                return;
            }

            if (confirmedAt == default || confirmedAt < InitiatedAt)
            {
                throw new ArgumentException("Confirmation time cannot be before payment initiation.", nameof(confirmedAt));
            }

            Status = MemberPaymentStatus.Confirmed;
            ConfirmedAt = confirmedAt;
        }

        private static string NormalizeCurrency(string currency)
        {
            var normalized = RequireText(currency, nameof(currency), 3).ToUpperInvariant();
            if (normalized.Length != 3)
            {
                throw new ArgumentException("A three-letter currency code is required.", nameof(currency));
            }

            return normalized;
        }

        private static string RequireText(string value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            }

            var normalized = value.Trim();
            if (normalized.Length > maxLength)
            {
                throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
            }

            return normalized;
        }
    }
}
