using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Abp.Dependency;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Payments.Yoco
{
    public interface IYocoWebhookSignatureVerifier
    {
        bool IsValid(string webhookId, string timestamp, string signature, string rawBody);
    }

    public sealed class YocoWebhookSignatureVerifier
        : IYocoWebhookSignatureVerifier, ITransientDependency
    {
        internal static readonly TimeSpan MaximumTimestampDifference = TimeSpan.FromMinutes(3);
        private readonly IConfiguration _configuration;

        public YocoWebhookSignatureVerifier(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool IsValid(string webhookId, string timestamp, string signature, string rawBody) =>
            IsValid(
                _configuration["Yoco:WebhookSecret"],
                webhookId,
                timestamp,
                signature,
                rawBody,
                DateTimeOffset.UtcNow);

        internal static bool IsValid(
            string secret,
            string webhookId,
            string timestamp,
            string signature,
            string rawBody,
            DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(secret) ||
                !secret.StartsWith("whsec_", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(webhookId) ||
                string.IsNullOrWhiteSpace(timestamp) ||
                string.IsNullOrWhiteSpace(signature) ||
                rawBody == null ||
                !long.TryParse(timestamp, out var unixTimestamp))
                return false;

            DateTimeOffset signedAt;
            try
            {
                signedAt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            if ((now - signedAt).Duration() > MaximumTimestampDifference)
                return false;

            byte[] secretBytes;
            try
            {
                secretBytes = Convert.FromBase64String(secret.Substring("whsec_".Length));
            }
            catch (FormatException)
            {
                return false;
            }

            var signedContent = $"{webhookId}.{timestamp}.{rawBody}";
            byte[] expected;
            using (var hmac = new HMACSHA256(secretBytes))
            {
                expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
            }

            return signature.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(candidate => candidate.StartsWith("v1,", StringComparison.Ordinal))
                .Select(candidate => candidate.Substring(3))
                .Any(candidate => ConstantTimeMatches(expected, candidate));
        }

        private static bool ConstantTimeMatches(byte[] expected, string encodedCandidate)
        {
            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    expected,
                    Convert.FromBase64String(encodedCandidate));
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
