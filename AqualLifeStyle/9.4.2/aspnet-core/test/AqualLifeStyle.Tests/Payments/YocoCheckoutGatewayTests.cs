using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abp.UI;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Payments
{
    public class YocoCheckoutGatewayTests
    {
        [Fact]
        public async Task Checkout_UsesServerCredentialAndStableIdempotencyReference()
        {
            var intentId = Guid.NewGuid();
            var handler = new RecordingHandler(
                "{\"id\":\"checkout-safe\",\"redirectUrl\":\"https://payments.example.test/checkout\",\"processingMode\":\"test\"}");
            var gateway = CreateGateway(handler, "test", "sk_test_not-a-real-key");

            var result = await gateway.CreateAsync(CreateRequest(intentId));

            result.Id.ShouldBe("checkout-safe");
            handler.AuthorizationScheme.ShouldBe("Bearer");
            handler.AuthorizationParameter.ShouldBe("sk_test_not-a-real-key");
            handler.IdempotencyKey.ShouldBe(intentId.ToString("N"));
            handler.Body.ShouldContain("\"amount\":612000");
            handler.Body.ShouldNotContain("customerId");
        }

        [Fact]
        public async Task Checkout_WithUnexpectedProviderMode_IsRejected()
        {
            var handler = new RecordingHandler(
                "{\"id\":\"checkout-safe\",\"redirectUrl\":\"https://payments.example.test/checkout\",\"processingMode\":\"live\"}");
            var gateway = CreateGateway(handler, "test", "sk_test_not-a-real-key");

            var exception = await Should.ThrowAsync<UserFriendlyException>(
                () => gateway.CreateAsync(CreateRequest(Guid.NewGuid())));

            exception.Details.ShouldContain("different payment mode");
        }

        [Fact]
        public async Task Checkout_WithInsecureReturnUrl_IsRejectedBeforeCallingProvider()
        {
            var handler = new RecordingHandler(
                "{\"id\":\"checkout-safe\",\"redirectUrl\":\"https://payments.example.test/checkout\",\"processingMode\":\"test\"}");
            var gateway = CreateGateway(handler, "test", "sk_test_not-a-real-key");
            var request = CreateRequest(Guid.NewGuid());
            request.SuccessUrl = "http://club.example.test/member/programmes?payment=success";

            await Should.ThrowAsync<ArgumentException>(
                () => gateway.CreateAsync(request));

            handler.Body.ShouldBeNull();
        }

        private static YocoCheckoutGateway CreateGateway(
            HttpMessageHandler handler,
            string mode,
            string secretKey)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Yoco:Mode"] = mode,
                    ["Yoco:SecretKey"] = secretKey
                })
                .Build();
            return new YocoCheckoutGateway(new HttpClient(handler), configuration);
        }

        private static CreateYocoCheckout CreateRequest(Guid intentId) => new()
        {
            ReferenceId = intentId,
            ReferenceMetadataKey = YocoCheckoutMetadata.DirectOnyxCheckoutIntentId,
            Purpose = "OnyxDirectEntry",
            Amount = 6120m,
            Currency = "ZAR",
            SuccessUrl = "https://club.example.test/member/programmes?payment=success",
            CancelUrl = "https://club.example.test/member/programmes?payment=cancelled",
            FailureUrl = "https://club.example.test/member/programmes?payment=failed",
            Description = "Direct Onyx participation"
        };

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly string _responseBody;

            public string AuthorizationScheme { get; private set; }
            public string AuthorizationParameter { get; private set; }
            public string IdempotencyKey { get; private set; }
            public string Body { get; private set; }

            public RecordingHandler(string responseBody)
            {
                _responseBody = responseBody;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                AuthorizationScheme = request.Headers.Authorization?.Scheme;
                AuthorizationParameter = request.Headers.Authorization?.Parameter;
                IdempotencyKey = string.Join(",", request.Headers.GetValues("Idempotency-Key"));
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
