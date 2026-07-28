using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Payments
{
    public class YocoPaymentsControllerTests
    {
        private sealed class TestYocoPaymentNotificationProcessor : YocoPaymentNotificationProcessor
        {
            public TestYocoPaymentNotificationProcessor()
                : base(null!, null!, null!, null!, null!, null!)
            {
            }

            public VerifiedYocoPaymentNotification? LastNotification { get; private set; }
            public Func<VerifiedYocoPaymentNotification, Task>? OnProcessAsync { get; set; }

            public override async Task ProcessAsync(VerifiedYocoPaymentNotification notification)
            {
                LastNotification = notification;
                if (OnProcessAsync != null)
                {
                    await OnProcessAsync(notification);
                }
            }
        }

        private static string Sign(string secret, string value)
        {
            var secretBytes = Convert.FromBase64String(secret.Substring("whsec_".Length));
            using var hmac = new HMACSHA256(secretBytes);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static YocoPaymentsController CreateController(
            string secret,
            out Mock<IYocoWebhookSignatureVerifier> verifierMock,
            out TestYocoPaymentNotificationProcessor testProcessor,
            JsonSerializerOptions? jsonOptions = null,
            TestLogger<YocoPaymentsController>? logger = null)
        {
            verifierMock = new Mock<IYocoWebhookSignatureVerifier>();
            verifierMock.Setup(v => v.IsValid(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string webhookId, string timestamp, string signature, string rawBody) =>
                {
                    var expectedSignature = $"v1,{Sign(secret, $"{webhookId}.{timestamp}.{rawBody}")}";
                    return signature == expectedSignature;
                });

            testProcessor = new TestYocoPaymentNotificationProcessor();

            var controller = new YocoPaymentsController(
                verifierMock.Object,
                testProcessor,
                new OptionsWrapper<Microsoft.AspNetCore.Mvc.JsonOptions>(new Microsoft.AspNetCore.Mvc.JsonOptions()),
                logger ?? new TestLogger<YocoPaymentsController>());

            if (jsonOptions != null)
            {
                typeof(YocoPaymentsController)
                    .GetField("_jsonOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(controller, jsonOptions);
            }

            return controller;
        }

        private static DefaultHttpContext CreateHttpContext(string rawBody, string webhookId, string timestamp, string signature)
        {
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            context.Request.Headers["webhook-id"] = webhookId;
            context.Request.Headers["webhook-timestamp"] = timestamp;
            context.Request.Headers["webhook-signature"] = signature;
            return context;
        }

        [Fact]
        public async Task WebhookAsync_ValidCamelCasePayload_ReturnsOk()
        {
            var secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var webhookId = "event_" + Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var payload = new
            {
                id = webhookId,
                type = "payment.succeeded",
                payload = new
                {
                    id = "pay_" + Guid.NewGuid().ToString("N"),
                    amount = 1200,
                    currency = "ZAR",
                    mode = "test",
                    createdDate = DateTimeOffset.UtcNow,
                    metadata = new Dictionary<string, JsonElement>()
                }
            };
            var rawBody = JsonSerializer.Serialize(payload);
            var signature = $"v1,{Sign(secret, $"{webhookId}.{timestamp}.{rawBody}")}";

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var logger = new TestLogger<YocoPaymentsController>();
            var controller = CreateController(
                secret,
                out var verifierMock,
                out var testProcessor,
                jsonOptions,
                logger);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(rawBody, webhookId, timestamp, signature)
            };

            var result = await controller.WebhookAsync();

            result.ShouldBeOfType<OkResult>();
            verifierMock.Verify(v => v.IsValid(webhookId, timestamp, signature, rawBody), Times.Once);
            testProcessor.LastNotification.ShouldNotBeNull();
            testProcessor.LastNotification.EventId.ShouldBe(webhookId);
            testProcessor.LastNotification.EventType.ShouldBe("payment.succeeded");
            testProcessor.LastNotification.PaymentId.ShouldBe(payload.payload.id);
            testProcessor.LastNotification.AmountInCents.ShouldBe(payload.payload.amount);
            testProcessor.LastNotification.Currency.ShouldBe(payload.payload.currency);
            testProcessor.LastNotification.Mode.ShouldBe(payload.payload.mode);
            testProcessor.LastNotification.PayloadHash.ShouldBe(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))));
        }

        [Fact]
        public async Task WebhookAsync_TamperedBody_ReturnsForbidden()
        {
            var secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var webhookId = "event_" + Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var originalBody = "{\"type\":\"payment.succeeded\"}";
            var signature = $"v1,{Sign(secret, $"{webhookId}.{timestamp}.{originalBody}")}";
            var tamperedBody = originalBody + " ";

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var logger = new TestLogger<YocoPaymentsController>();
            var controller = CreateController(
                secret,
                out var verifierMock,
                out var testProcessor,
                jsonOptions,
                logger);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(tamperedBody, webhookId, timestamp, signature)
            };

            var result = await controller.WebhookAsync();

            result.ShouldBeOfType<StatusCodeResult>();
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
            verifierMock.Verify(v => v.IsValid(webhookId, timestamp, signature, tamperedBody), Times.Once);
            testProcessor.LastNotification.ShouldBeNull();
            logger.Entries.ShouldContain(entry =>
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("yoco_webhook_signature_rejected"));
        }

        [Fact]
        public async Task WebhookAsync_TransientProcessingFailure_Returns500()
        {
            var secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var webhookId = "event_" + Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var payload = new
            {
                id = webhookId,
                type = "payment.succeeded",
                payload = new
                {
                    id = "pay_" + Guid.NewGuid().ToString("N"),
                    amount = 1200,
                    currency = "ZAR",
                    mode = "test",
                    createdDate = DateTimeOffset.UtcNow,
                    metadata = new Dictionary<string, JsonElement>()
                }
            };
            var rawBody = JsonSerializer.Serialize(payload);
            var signature = $"v1,{Sign(secret, $"{webhookId}.{timestamp}.{rawBody}")}";

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var logger = new TestLogger<YocoPaymentsController>();
            var controller = CreateController(
                secret,
                out var verifierMock,
                out var testProcessor,
                jsonOptions,
                logger);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(rawBody, webhookId, timestamp, signature)
            };

            testProcessor.OnProcessAsync = notification => Task.FromException(new YocoWebhookTransientException("Database temporarily unavailable"));

            var result = await controller.WebhookAsync();

            result.ShouldBeOfType<StatusCodeResult>();
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);
            logger.Entries.ShouldContain(entry =>
                entry.Level == LogLevel.Error &&
                entry.Message.Contains("yoco_webhook_processing_deferred"));
        }

        [Fact]
        public async Task WebhookAsync_PermanentValidationFailure_Returns400()
        {
            var secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var webhookId = "event_" + Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var payload = new
            {
                id = webhookId,
                type = "payment.succeeded",
                payload = new
                {
                    id = "pay_" + Guid.NewGuid().ToString("N"),
                    amount = 1200,
                    currency = "ZAR",
                    mode = "test",
                    createdDate = DateTimeOffset.UtcNow,
                    metadata = new Dictionary<string, JsonElement>()
                }
            };
            var rawBody = JsonSerializer.Serialize(payload);
            var signature = $"v1,{Sign(secret, $"{webhookId}.{timestamp}.{rawBody}")}";

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var logger = new TestLogger<YocoPaymentsController>();
            var controller = CreateController(
                secret,
                out var verifierMock,
                out var testProcessor,
                jsonOptions,
                logger);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(rawBody, webhookId, timestamp, signature)
            };

            testProcessor.OnProcessAsync = notification => Task.FromException(new YocoWebhookValidationException("Missing checkout reference."));

            var result = await controller.WebhookAsync();

            result.ShouldBeOfType<BadRequestResult>();
            logger.Entries.ShouldContain(entry =>
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("yoco_webhook_validation_rejected"));
        }
    }
}
