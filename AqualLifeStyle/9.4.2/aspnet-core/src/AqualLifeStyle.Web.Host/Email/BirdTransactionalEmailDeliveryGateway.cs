using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Email;
using Microsoft.Extensions.Options;

namespace AqualLifeStyle.Web.Host.Email
{
    /// <summary>
    /// Bird Email API adapter. The region is encoded in Bird API keys as
    /// bk_{region}_{token}; no workspace or channel identifier is required.
    /// </summary>
    public sealed class BirdTransactionalEmailDeliveryGateway : ITransactionalEmailDeliveryGateway
    {
        private readonly HttpClient _httpClient;
        private readonly BirdOptions _options;

        public BirdTransactionalEmailDeliveryGateway(HttpClient httpClient, IOptions<BirdOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<string> SendAsync(TransactionalEmail email, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled) throw new InvalidOperationException("Transactional email delivery is disabled.");
            var endpoint = ResolveEndpoint(_options.ApiKey);
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _options.ApiKey);
                request.Headers.TryAddWithoutValidation("Idempotency-Key", email.Reference);
                request.Content = JsonContent.Create(new
                {
                    from = new
                    {
                        email = _options.FromEmail,
                        name = _options.FromName
                    },
                    to = new[] { email.Recipient },
                    subject = email.Subject,
                    html = email.HtmlBody,
                    text = email.TextBody,
                    reply_to = string.IsNullOrWhiteSpace(_options.ReplyToEmail)
                        ? null
                        : new[] { _options.ReplyToEmail },
                    category = "transactional"
                });

                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                        throw CreateSafeException(response.StatusCode);

                    BirdMessageResponse result;
                    try
                    {
                        result = await response.Content.ReadFromJsonAsync<BirdMessageResponse>(cancellationToken: cancellationToken);
                    }
                    catch (JsonException exception)
                    {
                        throw new BirdEmailDeliveryException("Bird returned an invalid success response.", exception);
                    }
                    if (result == null || string.IsNullOrWhiteSpace(result.Id))
                        throw new BirdEmailDeliveryException("Bird returned an invalid success response.");
                    return result.Id;
                }
            }
        }

        private static Uri ResolveEndpoint(string apiKey)
        {
            if (!BirdOptions.TryResolveRegion(apiKey, out var region))
                throw new BirdEmailDeliveryException("Bird API key format is invalid.");

            return new Uri($"https://{region}.platform.bird.com/v1/email/messages");
        }

        private static BirdEmailDeliveryException CreateSafeException(HttpStatusCode statusCode)
        {
            string message;
            if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
                message = "Bird rejected the email provider credentials.";
            else if ((int)statusCode == 429)
                message = "Bird temporarily rate-limited email delivery.";
            else if ((int)statusCode >= 500)
                message = "Bird is temporarily unavailable.";
            else
                message = "Bird rejected the email request.";
            return new BirdEmailDeliveryException(message);
        }

        private sealed class BirdMessageResponse
        {
            public string Id { get; set; }
        }
    }

    public sealed class BirdEmailDeliveryException : Exception
    {
        public BirdEmailDeliveryException(string message) : base(message) { }
        public BirdEmailDeliveryException(string message, Exception innerException) : base(message, innerException) { }
    }
}
