using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.Payments
{
    public enum DirectOnyxCheckoutIntentStatus
    {
        PreparingCheckout = 0,
        AwaitingPayment = 1,
        Completed = 2
    }

    /// <summary>
    /// Captures the customer's intended direct-Onyx placement while payment is
    /// pending. It is deliberately not a programme participation or network
    /// placement; those are created only after verified payment confirmation.
    /// </summary>
    public class DirectOnyxCheckoutIntent : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public const int MaxInviteCodeLength = 32;
        public const int MaxProviderCheckoutIdLength = 128;
        public const int MaxCheckoutUrlLength = 2048;

        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public int? RecruiterCustomerId { get; private set; }
        public string InviteCode { get; private set; }
        public int OnyxMembershipId { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public decimal Amount { get; private set; }
        public string Currency { get; private set; }
        public DirectOnyxCheckoutIntentStatus Status { get; private set; }
        public string ProviderCheckoutId { get; private set; }
        public string CheckoutUrl { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? CheckoutCreatedAt { get; private set; }
        public Guid? PaymentId { get; private set; }
        public Guid? ParticipationId { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        protected DirectOnyxCheckoutIntent()
        {
        }

        public static DirectOnyxCheckoutIntent Create(
            int tenantId,
            int customerId,
            int? recruiterCustomerId,
            string inviteCode,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime createdAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (recruiterCustomerId == customerId)
                throw new ArgumentException("A customer cannot recruit themselves.", nameof(recruiterCustomerId));
            if (onyxMembershipId <= 0) throw new ArgumentOutOfRangeException(nameof(onyxMembershipId));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (createdAt == default) throw new ArgumentException("A creation time is required.", nameof(createdAt));

            var normalizedInviteCode = string.IsNullOrWhiteSpace(inviteCode)
                ? null
                : inviteCode.Trim().ToUpperInvariant();
            if (normalizedInviteCode?.Length > MaxInviteCodeLength)
                throw new ArgumentException($"Invitation code cannot exceed {MaxInviteCodeLength} characters.", nameof(inviteCode));

            return new DirectOnyxCheckoutIntent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                RecruiterCustomerId = recruiterCustomerId,
                InviteCode = normalizedInviteCode,
                OnyxMembershipId = onyxMembershipId,
                TermsVersion = terms.Version,
                TermsEffectiveFrom = terms.EffectiveFrom,
                Amount = terms.DirectEntryAmount,
                Currency = terms.Currency,
                Status = DirectOnyxCheckoutIntentStatus.PreparingCheckout,
                CreatedAt = createdAt
            };
        }

        public void RecordCheckout(string providerCheckoutId, string checkoutUrl, DateTime createdAt)
        {
            if (Status == DirectOnyxCheckoutIntentStatus.Completed)
                throw new InvalidOperationException("A completed Onyx checkout cannot be replaced.");

            var normalizedId = RequireText(
                providerCheckoutId,
                nameof(providerCheckoutId),
                MaxProviderCheckoutIdLength);
            var normalizedUrl = RequireText(checkoutUrl, nameof(checkoutUrl), MaxCheckoutUrlLength);
            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var parsedUrl) ||
                parsedUrl.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("The checkout URL must be a secure absolute URL.", nameof(checkoutUrl));
            if (createdAt == default || createdAt < CreatedAt)
                throw new ArgumentException("Checkout creation cannot precede the intent.", nameof(createdAt));

            if (!string.IsNullOrWhiteSpace(ProviderCheckoutId) &&
                (!string.Equals(ProviderCheckoutId, normalizedId, StringComparison.Ordinal) ||
                 !string.Equals(CheckoutUrl, normalizedUrl, StringComparison.Ordinal)))
                throw new InvalidOperationException("This Onyx intent is already linked to another checkout.");

            ProviderCheckoutId = normalizedId;
            CheckoutUrl = normalizedUrl;
            CheckoutCreatedAt = createdAt;
            Status = DirectOnyxCheckoutIntentStatus.AwaitingPayment;
        }

        public void Complete(Guid paymentId, Guid participationId, DateTime completedAt)
        {
            if (PaymentId == paymentId && ParticipationId == participationId)
                return;
            if (Status == DirectOnyxCheckoutIntentStatus.Completed)
                throw new InvalidOperationException("This Onyx checkout has already been completed.");
            if (paymentId == Guid.Empty) throw new ArgumentException("A payment is required.", nameof(paymentId));
            if (participationId == Guid.Empty) throw new ArgumentException("A participation is required.", nameof(participationId));
            if (completedAt == default || completedAt < CreatedAt)
                throw new ArgumentException("Completion cannot precede the checkout intent.", nameof(completedAt));

            PaymentId = paymentId;
            ParticipationId = participationId;
            CompletedAt = completedAt;
            Status = DirectOnyxCheckoutIntentStatus.Completed;
        }

        public OnyxPlanTerms RestoreTerms() => OnyxPlanTerms.Create(
            TermsVersion,
            TermsEffectiveFrom,
            Amount,
            Currency);

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
