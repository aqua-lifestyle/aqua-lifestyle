using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AqualLifeStyle.Tests.Payments
{
    public class YocoWebhookSignatureVerifierTests
    {
        // TODO: Remove this temporary missing-secret case once webhook registration is complete.
        [Fact]
        public void MissingWebhookSecret_RejectsWebhook()
        {
            var configuration = new ConfigurationBuilder().Build();
            var verifier = new YocoWebhookSignatureVerifier(configuration);

            Assert.False(verifier.IsValid(
                "event_123",
                "1",
                "v1,unsigned",
                Encoding.UTF8.GetBytes("{}")));
        }

        [Fact]
        public void ValidSignature_IsAccepted_AndTamperingIsRejected()
        {
            var secretBytes = RandomNumberGenerator.GetBytes(32);
            var secret = "whsec_" + Convert.ToBase64String(secretBytes);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Yoco:WebhookSecret"] = secret
                })
                .Build();
            var verifier = new YocoWebhookSignatureVerifier(configuration);
            var webhookId = "event_123";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var body = "{\"type\":\"payment.succeeded\"}";
            var signature = Sign(secretBytes, $"{webhookId}.{timestamp}.{body}");

            Assert.True(verifier.IsValid(
                webhookId,
                timestamp,
                $"v1,{signature}",
                Encoding.UTF8.GetBytes(body)));
            Assert.False(verifier.IsValid(
                webhookId,
                timestamp,
                $"v1,{signature}",
                Encoding.UTF8.GetBytes(body + " ")));
            Assert.False(verifier.IsValid(
                webhookId,
                timestamp,
                "v1,invalid",
                Encoding.UTF8.GetBytes(body)));
        }

        [Fact]
        public void StaleTimestamp_IsRejected()
        {
            var secretBytes = RandomNumberGenerator.GetBytes(32);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Yoco:WebhookSecret"] = "whsec_" + Convert.ToBase64String(secretBytes)
                })
                .Build();
            var verifier = new YocoWebhookSignatureVerifier(configuration);
            var timestamp = DateTimeOffset.UtcNow.AddMinutes(-4).ToUnixTimeSeconds().ToString();
            var body = "{}";
            var signature = Sign(secretBytes, $"event_stale.{timestamp}.{body}");

            Assert.False(verifier.IsValid(
                "event_stale",
                timestamp,
                $"v1,{signature}",
                Encoding.UTF8.GetBytes(body)));
        }

        [Fact]
        public void SignatureVerification_UsesExactRequestBytes()
        {
            var secretBytes = RandomNumberGenerator.GetBytes(32);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Yoco:WebhookSecret"] = "whsec_" + Convert.ToBase64String(secretBytes)
                })
                .Build();
            var verifier = new YocoWebhookSignatureVerifier(configuration);
            var webhookId = "event_raw_bytes";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var rawBody = new byte[] { 0x7b, 0x22, 0xff, 0x22, 0x7d };
            var prefix = Encoding.UTF8.GetBytes($"{webhookId}.{timestamp}.");
            var signedContent = new byte[prefix.Length + rawBody.Length];
            Buffer.BlockCopy(prefix, 0, signedContent, 0, prefix.Length);
            Buffer.BlockCopy(rawBody, 0, signedContent, prefix.Length, rawBody.Length);
            using var hmac = new HMACSHA256(secretBytes);
            var signature = Convert.ToBase64String(hmac.ComputeHash(signedContent));

            Assert.True(verifier.IsValid(
                webhookId,
                timestamp,
                $"v1,{signature}",
                rawBody));
        }

        private static string Sign(byte[] secret, string value)
        {
            using var hmac = new HMACSHA256(secret);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
    }
}
