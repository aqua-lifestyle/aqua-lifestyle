using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AqualLifeStyle.Email;
using AqualLifeStyle.Web.Host.Email;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Email
{
    public class BirdTransactionalEmailDeliveryGatewayTests
    {
        [Fact]
        public async Task Send_UsesCurrentBirdEmailApiContract()
        {
            var handler = new RecordingHandler(HttpStatusCode.Accepted, "{\"id\":\"bird-message-1\",\"status\":\"accepted\"}");
            var gateway = Create(handler, "bk_eu1_access-key-value");

            var result = await gateway.SendAsync(new TransactionalEmail(
                "member@example.test", "Welcome", "<p>Hello</p>", "Hello", "email-verification:1"));

            result.ShouldBe("bird-message-1");
            handler.Request.RequestUri.ToString().ShouldBe(
                "https://eu1.platform.bird.com/v1/email/messages");
            handler.Authorization.ShouldBe("Bearer bk_eu1_access-key-value");
            handler.IdempotencyKey.ShouldBe("email-verification:1");
            using (var json = JsonDocument.Parse(handler.Body))
            {
                var root = json.RootElement;
                root.GetProperty("from").GetProperty("email").GetString().ShouldBe("hello@example.test");
                root.GetProperty("from").GetProperty("name").GetString().ShouldBe("Aqua Lifestyle Club");
                root.GetProperty("to")[0].GetString().ShouldBe("member@example.test");
                root.GetProperty("subject").GetString().ShouldBe("Welcome");
                root.GetProperty("html").GetString().ShouldBe("<p>Hello</p>");
                root.GetProperty("text").GetString().ShouldBe("Hello");
                root.GetProperty("reply_to")[0].GetString().ShouldBe("help@example.test");
                root.GetProperty("category").GetString().ShouldBe("transactional");
            }
        }

        [Fact]
        public async Task Send_InfersRegionFromApiKey()
        {
            var handler = new RecordingHandler(HttpStatusCode.Accepted, "{\"id\":\"bird-message-1\"}");
            var gateway = Create(handler, "bk_us1_test-token");

            await gateway.SendAsync(new TransactionalEmail(
                "member@example.test", "Subject", "<p>Body</p>", "Body", "reference"));

            handler.Request.RequestUri.Host.ShouldBe("us1.platform.bird.com");
        }

        [Fact]
        public async Task Send_RejectsMalformedApiKeyWithoutMakingRequestOrLeakingIt()
        {
            const string malformedKey = "not-a-current-bird-key";
            var handler = new RecordingHandler(HttpStatusCode.Accepted, "{\"id\":\"unused\"}");
            var gateway = Create(handler, malformedKey);

            var error = await Should.ThrowAsync<BirdEmailDeliveryException>(() => gateway.SendAsync(
                new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference")));

            error.Message.ShouldBe("Bird API key format is invalid.");
            error.Message.ShouldNotContain(malformedKey);
            handler.Request.ShouldBeNull();
        }

        [Fact]
        public async Task Send_RedactsSecretWhenBirdRejectsCredentials()
        {
            const string secret = "bk_eu1_do-not-leak-this-key";
            var gateway = Create(new RecordingHandler(HttpStatusCode.Unauthorized, "secret response"), secret);

            var error = await Should.ThrowAsync<BirdEmailDeliveryException>(() => gateway.SendAsync(
                new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference")));

            error.Message.ShouldNotContain(secret);
            error.Message.ShouldNotContain("member@example.test");
        }

        [Fact]
        public async Task Send_FailsClosedWhenDisabled()
        {
            var handler = new RecordingHandler(HttpStatusCode.Accepted, "{\"id\":\"unused\"}");
            var options = ValidOptions();
            options.Enabled = false;
            var gateway = new BirdTransactionalEmailDeliveryGateway(
                new HttpClient(handler), Options.Create(options));

            await Should.ThrowAsync<InvalidOperationException>(() => gateway.SendAsync(
                new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference")));
            handler.Request.ShouldBeNull();
        }

        [Fact]
        public async Task Send_RejectsMalformedSuccessResponse()
        {
            var gateway = Create(new RecordingHandler(HttpStatusCode.Accepted, "{\"status\":\"accepted\"}"), "bk_eu1_key");

            var error = await Should.ThrowAsync<BirdEmailDeliveryException>(() => gateway.SendAsync(
                new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference")));

            error.Message.ShouldBe("Bird returned an invalid success response.");
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, "Bird temporarily rate-limited email delivery.")]
        [InlineData(HttpStatusCode.InternalServerError, "Bird is temporarily unavailable.")]
        [InlineData(HttpStatusCode.BadRequest, "Bird rejected the email request.")]
        public async Task Send_ReturnsSafeActionableProviderErrors(HttpStatusCode status, string expectedMessage)
        {
            var gateway = Create(new RecordingHandler(status, "provider response must not be exposed"), "bk_eu1_key");

            var error = await Should.ThrowAsync<BirdEmailDeliveryException>(() => gateway.SendAsync(
                new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference")));

            error.Message.ShouldBe(expectedMessage);
            error.Message.ShouldNotContain("provider response");
        }

        [Fact]
        public async Task Send_ObservesCallerCancellation()
        {
            var gateway = Create(new DelayedHandler(), "bk_eu1_key");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => gateway.SendAsync(
                    new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference"),
                    cancellation.Token));
            }
        }

        [Fact]
        public async Task Send_ObservesHttpClientTimeout()
        {
            var options = ValidOptions();
            var client = new HttpClient(new DelayedHandler())
            {
                Timeout = TimeSpan.FromMilliseconds(20)
            };
            var gateway = new BirdTransactionalEmailDeliveryGateway(client, Options.Create(options));

            await Should.ThrowAsync<TaskCanceledException>(() => gateway.SendAsync(
                new TransactionalEmail("member@example.test", "Subject", "<p>Body</p>", "Body", "reference")));
        }

        private static BirdTransactionalEmailDeliveryGateway Create(HttpMessageHandler handler, string key)
        {
            var options = ValidOptions();
            options.ApiKey = key;
            return new BirdTransactionalEmailDeliveryGateway(
                new HttpClient(handler), Options.Create(options));
        }

        private static BirdOptions ValidOptions() => new BirdOptions
        {
            Enabled = true,
            ApiKey = "bk_eu1_key",
            FromEmail = "hello@example.test",
            FromName = "Aqua Lifestyle Club",
            ReplyToEmail = "help@example.test"
        };

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _response;
            public HttpRequestMessage Request { get; private set; }
            public string Authorization { get; private set; }
            public string IdempotencyKey { get; private set; }
            public string Body { get; private set; }

            public RecordingHandler(HttpStatusCode status, string response)
            {
                _status = status;
                _response = response;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                Authorization = request.Headers.GetValues("Authorization").ShouldHaveSingleItem();
                IdempotencyKey = request.Headers.GetValues("Idempotency-Key").ShouldHaveSingleItem();
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_response, Encoding.UTF8, "application/json")
                };
            }
        }

        private sealed class DelayedHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
        }
    }
}
