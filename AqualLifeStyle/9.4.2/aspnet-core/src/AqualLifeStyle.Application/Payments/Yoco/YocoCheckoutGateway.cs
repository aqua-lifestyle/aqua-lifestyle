using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.UI;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Payments.Yoco
{
    public sealed class CreateYocoCheckout
    {
        public Guid ReferenceId { get; set; }
        public string ReferenceMetadataKey { get; set; }
        public string Purpose { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string SuccessUrl { get; set; }
        public string CancelUrl { get; set; }
        public string FailureUrl { get; set; }
        public string Description { get; set; }
    }

    public sealed class YocoCheckout
    {
        public string Id { get; set; }
        public string RedirectUrl { get; set; }
    }

    public interface IYocoCheckoutGateway
    {
        Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout);
    }

    public sealed class YocoCheckoutGateway : IYocoCheckoutGateway
    {
        private const string CheckoutEndpoint = "https://payments.yoco.com/api/checkouts";
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public YocoCheckoutGateway(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout)
        {
            if (checkout == null) throw new ArgumentNullException(nameof(checkout));
            if (checkout.ReferenceId == Guid.Empty)
                throw new ArgumentException("A checkout reference is required.", nameof(checkout));
            if (!YocoCheckoutMetadata.IsSupportedReference(checkout.ReferenceMetadataKey))
                throw new ArgumentException("The checkout reference type is not supported.", nameof(checkout));
            if (string.IsNullOrWhiteSpace(checkout.Purpose))
                throw new ArgumentException("A checkout purpose is required.", nameof(checkout));
            var secretKey = _configuration["Yoco:SecretKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new UserFriendlyException(
                    "Online payment is temporarily unavailable.",
                    "The club's payment service has not been configured.");
            var mode = _configuration["Yoco:Mode"]?.Trim().ToLowerInvariant();
            var expectedPrefix = mode == "live"
                ? "sk_live_"
                : mode == "test"
                    ? "sk_test_"
                    : null;
            if (expectedPrefix == null || !secretKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new UserFriendlyException(
                    "Online payment is temporarily unavailable.",
                    "The club's payment mode and secure key do not match.");
            ValidateCallbackUrl(checkout.SuccessUrl, nameof(checkout.SuccessUrl), mode);
            ValidateCallbackUrl(checkout.CancelUrl, nameof(checkout.CancelUrl), mode);
            ValidateCallbackUrl(checkout.FailureUrl, nameof(checkout.FailureUrl), mode);

            var amountInCents = ToCents(checkout.Amount);
            var reference = checkout.ReferenceId.ToString("N");
            var requestBody = new
            {
                amount = amountInCents,
                currency = checkout.Currency,
                successUrl = checkout.SuccessUrl,
                cancelUrl = checkout.CancelUrl,
                failureUrl = checkout.FailureUrl,
                metadata = new Dictionary<string, object>
                {
                    [checkout.ReferenceMetadataKey] = reference,
                    ["purpose"] = checkout.Purpose
                },
                subtotalAmount = amountInCents,
                lineItems = new[]
                {
                    new
                    {
                        displayName = checkout.Description,
                        quantity = 1,
                        pricingDetails = new { price = amountInCents }
                    }
                },
                clientReferenceId = reference,
                externalId = reference
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, CheckoutEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            request.Headers.Add("Idempotency-Key", reference);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new UserFriendlyException(
                    "Online payment could not be started.",
                    "Yoco could not create the secure checkout. Please try again.");

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<YocoCheckoutResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (string.IsNullOrWhiteSpace(result?.Id) || string.IsNullOrWhiteSpace(result.RedirectUrl))
                throw new UserFriendlyException(
                    "Online payment could not be started.",
                    "Yoco returned an incomplete checkout response. Please try again.");
            if (!string.Equals(result.ProcessingMode, mode, StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException(
                    "Online payment could not be started.",
                    "Yoco returned a checkout for a different payment mode. Please try again.");

            return new YocoCheckout { Id = result.Id, RedirectUrl = result.RedirectUrl };
        }

        private static int ToCents(decimal amount)
        {
            var cents = amount * 100m;
            if (amount <= 0 || cents != decimal.Truncate(cents) || cents > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(amount), "The payment amount cannot be represented in cents.");
            return decimal.ToInt32(cents);
        }

        private static void ValidateCallbackUrl(
            string value,
            string parameterName,
            string mode)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new ArgumentException(
                    "Yoco checkout return URLs must be absolute URLs.",
                    parameterName);

            var isSecure = uri.Scheme == Uri.UriSchemeHttps;
            var isTestLoopback = mode == "test" &&
                                 uri.Scheme == Uri.UriSchemeHttp &&
                                 uri.IsLoopback;
            if (!isSecure && !isTestLoopback)
            {
                throw new ArgumentException(
                    "Yoco checkout return URLs must use HTTPS except for test-mode loopback URLs.",
                    parameterName);
            }
        }

        private sealed class YocoCheckoutResponse
        {
            public string Id { get; set; }
            public string RedirectUrl { get; set; }
            public string ProcessingMode { get; set; }
        }
    }
}
