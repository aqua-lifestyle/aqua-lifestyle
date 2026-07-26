using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Dependency;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Payments.Yoco
{
    public sealed class VerifiedYocoPaymentNotification
    {
        public string EventType { get; set; }
        public string PaymentId { get; set; }
        public int AmountInCents { get; set; }
        public string Currency { get; set; }
        public string Mode { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public IReadOnlyDictionary<string, JsonElement> Metadata { get; set; }
    }

    public sealed class YocoPaymentNotificationProcessor : ITransientDependency
    {
        internal const string CheckoutIntentMetadataKey = "directOnyxCheckoutIntentId";
        private readonly ProgrammePaymentConfirmationProcessor _confirmationProcessor;
        private readonly IConfiguration _configuration;

        public YocoPaymentNotificationProcessor(
            ProgrammePaymentConfirmationProcessor confirmationProcessor,
            IConfiguration configuration)
        {
            _confirmationProcessor = confirmationProcessor;
            _configuration = configuration;
        }

        public async Task ProcessAsync(VerifiedYocoPaymentNotification notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            if (!string.Equals(notification.EventType, "payment.succeeded", StringComparison.Ordinal))
                return;

            var configuredMode = _configuration["Yoco:Mode"]?.Trim();
            if (string.IsNullOrWhiteSpace(configuredMode) ||
                !string.Equals(configuredMode, notification.Mode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Yoco payment mode does not match this deployment.");
            if (notification.AmountInCents <= 0)
                throw new InvalidOperationException("The Yoco payment amount is invalid.");
            if (string.IsNullOrWhiteSpace(notification.PaymentId))
                throw new InvalidOperationException("The Yoco payment reference is missing.");
            if (notification.ConfirmedAt == default)
                throw new InvalidOperationException("The Yoco payment confirmation time is missing.");
            if (notification.Metadata == null ||
                !notification.Metadata.TryGetValue(CheckoutIntentMetadataKey, out var intentValue) ||
                intentValue.ValueKind != JsonValueKind.String ||
                !Guid.TryParseExact(intentValue.GetString(), "N", out var intentId))
                throw new InvalidOperationException("The Yoco payment is missing its Onyx checkout reference.");

            await _confirmationProcessor.ProcessDirectOnyxCheckoutAsync(
                intentId,
                "Yoco",
                notification.PaymentId,
                notification.AmountInCents / 100m,
                notification.Currency,
                DateTime.SpecifyKind(notification.ConfirmedAt, DateTimeKind.Utc));
        }
    }
}
