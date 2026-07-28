using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Payments
{
    public enum YocoCheckoutProgramme
    {
        AQGreen = 0,
        Onyx = 1
    }

    /// <summary>
    /// Immutable evidence that one authenticated Yoco event completed processing.
    /// The raw webhook body is deliberately not persisted.
    /// </summary>
    public class YocoWebhookReceipt : CreationAuditedEntity<Guid>, IMustHaveTenant
    {
        public const int MaxEventIdLength = 128;
        public const int MaxPaymentIdLength = 128;
        public const int Sha256HexLength = 64;

        public int TenantId { get; set; }
        public string EventId { get; private set; }
        public string PaymentId { get; private set; }
        public string ProviderCheckoutId { get; private set; }
        public string PayloadHash { get; private set; }
        public YocoCheckoutProgramme Programme { get; private set; }
        public Guid CheckoutReferenceId { get; private set; }
        public DateTime ProcessedAt { get; private set; }

        protected YocoWebhookReceipt()
        {
        }

        public static YocoWebhookReceipt Record(
            int tenantId,
            string eventId,
            string paymentId,
            string providerCheckoutId,
            string payloadHash,
            YocoCheckoutProgramme programme,
            Guid checkoutReferenceId,
            DateTime processedAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (checkoutReferenceId == Guid.Empty)
                throw new ArgumentException("A checkout reference is required.", nameof(checkoutReferenceId));
            if (processedAt == default)
                throw new ArgumentException("A processing time is required.", nameof(processedAt));

            return new YocoWebhookReceipt
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventId = RequireText(eventId, nameof(eventId), MaxEventIdLength),
                PaymentId = RequireText(paymentId, nameof(paymentId), MaxPaymentIdLength),
                ProviderCheckoutId = RequireText(
                    providerCheckoutId,
                    nameof(providerCheckoutId),
                    HostedPaymentCheckout.MaxProviderCheckoutIdLength),
                PayloadHash = RequireHash(payloadHash),
                Programme = programme,
                CheckoutReferenceId = checkoutReferenceId,
                ProcessedAt = processedAt
            };
        }

        public bool Matches(
            string paymentId,
            string providerCheckoutId,
            string payloadHash,
            YocoCheckoutProgramme programme,
            Guid checkoutReferenceId) =>
            string.Equals(PaymentId, paymentId?.Trim(), StringComparison.Ordinal) &&
            string.Equals(ProviderCheckoutId, providerCheckoutId?.Trim(), StringComparison.Ordinal) &&
            string.Equals(PayloadHash, payloadHash?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            Programme == programme &&
            CheckoutReferenceId == checkoutReferenceId;

        private static string RequireHash(string value)
        {
            var normalized = RequireText(value, nameof(value), Sha256HexLength).ToUpperInvariant();
            if (normalized.Length != Sha256HexLength)
                throw new ArgumentException("A SHA-256 payload hash is required.", nameof(value));
            for (var index = 0; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (!char.IsDigit(character) && (character < 'A' || character > 'F'))
                    throw new ArgumentException("A SHA-256 payload hash is required.", nameof(value));
            }
            return normalized;
        }

        private static string RequireText(string value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            var normalized = value.Trim();
            if (normalized.Length > maxLength)
                throw new ArgumentException(
                    $"{parameterName} cannot exceed {maxLength} characters.",
                    parameterName);
            return normalized;
        }
    }
}
