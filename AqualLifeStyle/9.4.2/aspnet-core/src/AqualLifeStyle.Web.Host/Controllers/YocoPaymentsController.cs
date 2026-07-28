using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AqualLifeStyle.Web.Host.Controllers
{
    /// <summary>
    /// Explicit protocol adapter for Yoco webhooks. ABP conventional controllers cannot
    /// preserve the exact raw request body, provider signature headers, and unwrapped
    /// HTTP status codes required for webhook signature verification and retry semantics.
    /// </summary>
    [ApiController]
    [Route("api/payments/yoco")]
    public sealed class YocoPaymentsController : ControllerBase, ITransientDependency
        {
            private readonly IYocoWebhookSignatureVerifier _signatureVerifier;
            private readonly YocoPaymentNotificationProcessor _notificationProcessor;
            private readonly JsonSerializerOptions _jsonOptions;

            public YocoPaymentsController(
                IYocoWebhookSignatureVerifier signatureVerifier,
                YocoPaymentNotificationProcessor notificationProcessor,
                IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> jsonOptions)
            {
                _signatureVerifier = signatureVerifier;
                _notificationProcessor = notificationProcessor;
                _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
            }

        [HttpPost("webhook")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> WebhookAsync()
        {
            string rawBody;
            using (var reader = new StreamReader(
                       Request.Body,
                       Encoding.UTF8,
                       detectEncodingFromByteOrderMarks: false,
                       leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            if (!_signatureVerifier.IsValid(
                    Request.Headers["webhook-id"],
                    Request.Headers["webhook-timestamp"],
                    Request.Headers["webhook-signature"],
                    rawBody))
                return StatusCode(StatusCodes.Status403Forbidden);

            YocoPaymentWebhookEvent webhookEvent;
            try
            {
                webhookEvent = JsonSerializer.Deserialize<YocoPaymentWebhookEvent>(
                    rawBody,
                    _jsonOptions);
            }
            catch (JsonException)
            {
                return BadRequest();
            }

            if (webhookEvent?.Payload == null)
                return BadRequest();

            try
            {
                await _notificationProcessor.ProcessAsync(new VerifiedYocoPaymentNotification
                {
                    EventId = webhookEvent.Id,
                    EventType = webhookEvent.Type,
                    PaymentId = webhookEvent.Payload.Id,
                    AmountInCents = webhookEvent.Payload.Amount,
                    Currency = webhookEvent.Payload.Currency,
                    Mode = webhookEvent.Payload.Mode,
                    ConfirmedAt = webhookEvent.Payload.CreatedDate,
                    PayloadHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))),
                    Metadata = webhookEvent.Payload.Metadata
                });
                return Ok();
            }
            catch (AqualLifeStyle.Payments.Yoco.YocoWebhookTransientException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            catch (AqualLifeStyle.Payments.Yoco.YocoWebhookValidationException)
            {
                return BadRequest();
            }
        }

        private sealed class YocoPaymentWebhookEvent
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public YocoPaymentWebhookPayload Payload { get; set; }
        }

        private sealed class YocoPaymentWebhookPayload
        {
            public int Amount { get; set; }
            public DateTimeOffset CreatedDate { get; set; }
            public string Currency { get; set; }
            public string Id { get; set; }
            public string Mode { get; set; }
            public Dictionary<string, JsonElement> Metadata { get; set; }
        }
    }
}
