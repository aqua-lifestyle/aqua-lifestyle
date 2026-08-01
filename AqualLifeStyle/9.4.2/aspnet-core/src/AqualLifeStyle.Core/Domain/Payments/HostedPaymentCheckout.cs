using System;
using System.ComponentModel.DataAnnotations.Schema;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Payments
{
    public enum HostedPaymentCheckoutStatus
    {
        PreparingCheckout = 0,
        AwaitingPayment = 1,
        Completed = 2,
        Failed = 3,
        Expired = 4,
        AdministrativelyTerminated = 5
    }

    /// <summary>
    /// Shared provider-hosted checkout lifecycle. Programme-specific aggregates
    /// retain responsibility for deciding what a confirmed payment activates.
    /// </summary>
    [NotMapped]
    public abstract class HostedPaymentCheckout
        : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public const int MaxProviderCheckoutIdLength = 128;
        public const int MaxCheckoutUrlLength = 2048;

        public int TenantId { get; set; }
        public int CustomerId { get; protected set; }
        public decimal Amount { get; protected set; }
        public string Currency { get; protected set; }
        public HostedPaymentCheckoutStatus Status { get; protected set; }
        public string ProviderCheckoutId { get; protected set; }
        public string CheckoutUrl { get; protected set; }
        public DateTime CreatedAt { get; protected set; }
        public DateTime? CheckoutCreatedAt { get; protected set; }
        public Guid? PaymentId { get; protected set; }
        public DateTime? CompletedAt { get; protected set; }
        public DateTime? TerminatedAt { get; protected set; }
        public long? TerminatedByAdministratorUserId { get; protected set; }
        public string TerminalEvidence { get; protected set; }

        protected void Initialize(
            int tenantId,
            int customerId,
            decimal amount,
            string currency,
            DateTime createdAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
            if (createdAt == default) throw new ArgumentException("A creation time is required.", nameof(createdAt));
            var normalizedCurrency = currency?.Trim().ToUpperInvariant();
            if (normalizedCurrency?.Length != 3)
                throw new ArgumentException("A three-letter currency code is required.", nameof(currency));

            Id = Guid.NewGuid();
            TenantId = tenantId;
            CustomerId = customerId;
            Amount = amount;
            Currency = normalizedCurrency;
            CreatedAt = createdAt;
            Status = HostedPaymentCheckoutStatus.PreparingCheckout;
        }

        public void RecordCheckout(string providerCheckoutId, string checkoutUrl, DateTime createdAt)
        {
            if (Status == HostedPaymentCheckoutStatus.Completed)
                throw new InvalidOperationException("A completed payment checkout cannot be replaced.");

            var normalizedId = RequireText(providerCheckoutId, nameof(providerCheckoutId), MaxProviderCheckoutIdLength);
            var normalizedUrl = RequireText(checkoutUrl, nameof(checkoutUrl), MaxCheckoutUrlLength);
            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var parsedUrl) ||
                parsedUrl.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("The checkout URL must be a secure absolute URL.", nameof(checkoutUrl));
            if (createdAt == default || createdAt < CreatedAt)
                throw new ArgumentException("Checkout creation cannot precede the payment request.", nameof(createdAt));
            if (!string.IsNullOrWhiteSpace(ProviderCheckoutId) &&
                (!string.Equals(ProviderCheckoutId, normalizedId, StringComparison.Ordinal) ||
                 !string.Equals(CheckoutUrl, normalizedUrl, StringComparison.Ordinal)))
                throw new InvalidOperationException("This payment request is already linked to another checkout.");

            ProviderCheckoutId = normalizedId;
            CheckoutUrl = normalizedUrl;
            CheckoutCreatedAt = createdAt;
            Status = HostedPaymentCheckoutStatus.AwaitingPayment;
        }

        protected bool CompletePayment(Guid paymentId, DateTime completedAt)
        {
            if (PaymentId == paymentId) return false;
            if (Status == HostedPaymentCheckoutStatus.Completed)
                throw new InvalidOperationException("This payment checkout has already been completed.");
            if (Status != HostedPaymentCheckoutStatus.PreparingCheckout &&
                Status != HostedPaymentCheckoutStatus.AwaitingPayment)
                throw new InvalidOperationException(
                    "Only a payable checkout can be completed.");
            if (paymentId == Guid.Empty) throw new ArgumentException("A payment is required.", nameof(paymentId));
            if (completedAt == default || completedAt < CreatedAt)
                throw new ArgumentException("Completion cannot precede the payment request.", nameof(completedAt));

            PaymentId = paymentId;
            CompletedAt = completedAt;
            Status = HostedPaymentCheckoutStatus.Completed;
            return true;
        }

        protected void Terminate(
            HostedPaymentCheckoutStatus terminalStatus,
            DateTime terminatedAt,
            string evidence,
            long? administratorUserId = null)
        {
            if (terminalStatus != HostedPaymentCheckoutStatus.Failed &&
                terminalStatus != HostedPaymentCheckoutStatus.Expired &&
                terminalStatus != HostedPaymentCheckoutStatus.AdministrativelyTerminated)
                throw new ArgumentOutOfRangeException(nameof(terminalStatus));
            var normalizedEvidence = RequireText(evidence, nameof(evidence), 1000);
            if (Status == HostedPaymentCheckoutStatus.Completed)
                throw new InvalidOperationException("A completed checkout cannot be terminated.");
            if (Status == terminalStatus)
            {
                if (!string.Equals(TerminalEvidence, normalizedEvidence, StringComparison.Ordinal) ||
                    TerminatedByAdministratorUserId != administratorUserId)
                    throw new InvalidOperationException(
                        "This checkout was already terminated using different evidence.");
                return;
            }
            if (Status != HostedPaymentCheckoutStatus.AwaitingPayment)
                throw new InvalidOperationException("Only an awaiting checkout can be terminated.");
            if (terminatedAt == default || terminatedAt < CreatedAt)
                throw new ArgumentException("A valid terminal time is required.", nameof(terminatedAt));
            if (terminalStatus == HostedPaymentCheckoutStatus.AdministrativelyTerminated &&
                (!administratorUserId.HasValue || administratorUserId.Value <= 0))
                throw new ArgumentException("An administrator is required.", nameof(administratorUserId));

            Status = terminalStatus;
            TerminatedAt = terminatedAt;
            TerminalEvidence = normalizedEvidence;
            TerminatedByAdministratorUserId = administratorUserId;
        }

        private static string RequireText(string value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            var normalized = value.Trim();
            if (normalized.Length > maxLength)
                throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
            return normalized;
        }
    }
}
