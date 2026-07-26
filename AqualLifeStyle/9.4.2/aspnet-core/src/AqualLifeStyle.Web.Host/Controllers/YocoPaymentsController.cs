using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AqualLifeStyle.Web.Host.Controllers
{
    [ApiController]
    [Route("api/payments/yoco")]
    public sealed class YocoPaymentsController : ControllerBase, ITransientDependency
    {
        private readonly IYocoWebhookSignatureVerifier _signatureVerifier;
        private readonly YocoPaymentNotificationProcessor _notificationProcessor;

        public YocoPaymentsController(
            IYocoWebhookSignatureVerifier signatureVerifier,
            YocoPaymentNotificationProcessor notificationProcessor)
        {
            _signatureVerifier = signatureVerifier;
            _notificationProcessor = notificationProcessor;
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
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return BadRequest();
            }

            if (webhookEvent?.Payload == null)
                return BadRequest();

            await _notificationProcessor.ProcessAsync(new VerifiedYocoPaymentNotification
            {
                EventType = webhookEvent.Type,
                PaymentId = webhookEvent.Payload.Id,
                AmountInCents = webhookEvent.Payload.Amount,
                Currency = webhookEvent.Payload.Currency,
                Mode = webhookEvent.Payload.Mode,
                ConfirmedAt = webhookEvent.Payload.CreatedDate,
                Metadata = webhookEvent.Payload.Metadata
            });
            return Ok();
        }

        private sealed class YocoPaymentWebhookEvent
        {
            public string Type { get; set; }
            public YocoPaymentWebhookPayload Payload { get; set; }
        }

        private sealed class YocoPaymentWebhookPayload
        {
            public int Amount { get; set; }
            public DateTime CreatedDate { get; set; }
            public string Currency { get; set; }
            public string Id { get; set; }
            public string Mode { get; set; }
            public Dictionary<string, JsonElement> Metadata { get; set; }
        }
    }
}
