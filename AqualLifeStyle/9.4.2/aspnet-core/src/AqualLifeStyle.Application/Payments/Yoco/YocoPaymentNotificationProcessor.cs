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
        public DateTimeOffset ConfirmedAt { get; set; }
        public IReadOnlyDictionary<string, JsonElement> Metadata { get; set; }
    }

    public sealed class YocoWebhookValidationException : InvalidOperationException
    {
        public YocoWebhookValidationException(string message) : base(message) { }
        public YocoWebhookValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class YocoPaymentNotificationProcessor : ITransientDependency
    {
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
                throw new YocoWebhookValidationException("The Yoco payment mode does not match this deployment.");
            if (notification.AmountInCents <= 0)
                throw new YocoWebhookValidationException("The Yoco payment amount is invalid.");
            if (string.IsNullOrWhiteSpace(notification.PaymentId))
                throw new YocoWebhookValidationException("The Yoco payment reference is missing.");
            if (notification.ConfirmedAt == default)
                throw new YocoWebhookValidationException("The Yoco payment confirmation time is missing.");
            var providerCheckoutId = GetRequiredMetadataText(
                notification.Metadata,
                YocoCheckoutMetadata.ProviderCheckoutId,
                "The Yoco payment is missing its provider checkout reference.");
            var confirmedAt = notification.ConfirmedAt.UtcDateTime;

            try
            {
                if (TryGetReference(
                        notification.Metadata,
                        YocoCheckoutMetadata.DirectOnyxCheckoutIntentId,
                        out var onyxCheckoutId))
                {
                    if (TryGetReference(
                            notification.Metadata,
                            YocoCheckoutMetadata.AQGreenJoiningCheckoutId,
                            out _))
                        throw new YocoWebhookValidationException(
                            "The Yoco payment contains conflicting programme checkout references.");
                    await _confirmationProcessor.ProcessDirectOnyxCheckoutAsync(
                        onyxCheckoutId,
                        "Yoco",
                        notification.PaymentId,
                        providerCheckoutId,
                        notification.AmountInCents / 100m,
                        notification.Currency,
                        confirmedAt);
                    return;
                }

                if (TryGetReference(
                        notification.Metadata,
                        YocoCheckoutMetadata.AQGreenJoiningCheckoutId,
                        out var aqGreenCheckoutId))
                {
                    await _confirmationProcessor.ProcessAQGreenJoiningCheckoutAsync(
                        aqGreenCheckoutId,
                        "Yoco",
                        notification.PaymentId,
                        providerCheckoutId,
                        notification.AmountInCents / 100m,
                        notification.Currency,
                        confirmedAt);
                    return;
                }

                throw new YocoWebhookValidationException(
                    "The Yoco payment is missing a supported programme checkout reference.");
            }
            catch (YocoWebhookValidationException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                throw new YocoWebhookValidationException("The Yoco webhook is invalid.", ex);
            }
        }

        private static bool TryGetReference(
            IReadOnlyDictionary<string, JsonElement> metadata,
            string key,
            out Guid reference)
        {
            reference = Guid.Empty;
            return metadata != null &&
                   metadata.TryGetValue(key, out var value) &&
                   value.ValueKind == JsonValueKind.String &&
                   Guid.TryParseExact(value.GetString(), "N", out reference);
        }

        private static string GetRequiredMetadataText(
            IReadOnlyDictionary<string, JsonElement> metadata,
            string key,
            string errorMessage)
        {
            if (metadata != null &&
                metadata.TryGetValue(key, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString().Trim();
            throw new YocoWebhookValidationException(errorMessage);
        }
    }
}
