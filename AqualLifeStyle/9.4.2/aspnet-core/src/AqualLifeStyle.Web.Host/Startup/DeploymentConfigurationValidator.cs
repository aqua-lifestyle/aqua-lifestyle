using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Email;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using AqualLifeStyle.Configuration;

namespace AqualLifeStyle.Web.Host.Startup
{
    public static class DeploymentConfigurationValidator
    {
        public static void Validate(IServiceProvider services)
        {
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var configuration = services.GetRequiredService<IConfiguration>();
            Validate(environment, configuration);
        }

        public static void Validate(IWebHostEnvironment environment, IConfiguration configuration)
        {
            var recovery = ApplicationRecoveryMode.IsEnabled(configuration);
            if (recovery)
            {
                foreach (var key in new[] { "Bird:Enabled", "App:WeeklyCommissions:Enabled",
                    "App:EntryMonthlyObligations:Enabled", "App:FinancialOperations:Enabled", "App:BackgroundJobs:Enabled",
                    "AQGreenV2Demo:Enabled", "AQGreenV2Pilot:Enabled", "SEED_DEMO_DATA" })
                    if (configuration.GetValue<bool>(key))
                        throw new InvalidOperationException("API recovery requires " + key.Replace(":", "__") + "=false.");
                foreach (var key in new[] { "Yoco:Mode", "Yoco:SecretKey", "Yoco:WebhookSecret", "Bird:ApiKey" })
                    if (IsConfigured(configuration[key]))
                        throw new InvalidOperationException("API recovery requires provider setting " + key.Replace(":", "__") + " to be absent.");
            }

            AQGreenV2DemoConfiguration.Validate(environment, configuration);
            if (AQGreenV2Pilot.AQGreenV2PilotConfiguration.Validate(environment, configuration))
                return; // The isolated checkout has no provider credentials or outbound provider call.

            var yocoMode = configuration["Yoco:Mode"]?.Trim().ToLowerInvariant();
            var yocoSecretKey = configuration["Yoco:SecretKey"]?.Trim();
            var yocoWebhookSecret = configuration["Yoco:WebhookSecret"]?.Trim();
            var hasYocoMode = IsConfigured(yocoMode);
            var hasYocoSecretKey = IsConfigured(yocoSecretKey);
            var hasYocoWebhookSecret = IsConfigured(yocoWebhookSecret);
            if (hasYocoWebhookSecret &&
                !YocoWebhookSignatureVerifier.HasValidSecretFormat(yocoWebhookSecret))
            {
                throw new InvalidOperationException(
                    "Yoco configuration is invalid. Yoco__WebhookSecret must be a valid whsec_ signing secret.");
            }

            if (hasYocoMode || hasYocoSecretKey)
            {
                var expectedConfiguredKeyPrefix = yocoMode == "live"
                    ? "sk_live_"
                    : yocoMode == "test"
                        ? "sk_test_"
                        : null;
                if (expectedConfiguredKeyPrefix == null ||
                    !hasYocoSecretKey ||
                    !yocoSecretKey.StartsWith(expectedConfiguredKeyPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Yoco configuration is invalid. Yoco__Mode must be test or live and must match the Yoco__SecretKey prefix.");
                }
            }

            if ((yocoMode == "live" || yocoMode == "test") && !hasYocoWebhookSecret)
                throw new InvalidOperationException(
                    "Yoco configuration is incomplete. Yoco__WebhookSecret is required when Yoco__Mode=" + yocoMode + ".");

            if (!environment.IsProduction())
            {
                return;
            }

            var requiredSettings = new Dictionary<string, string>
            {
                ["App:ServerRootAddress"] = "App__ServerRootAddress",
                ["App:ClientRootAddress"] = "App__ClientRootAddress",
                ["App:CorsOrigins"] = "App__CorsOrigins",
                ["Authentication:JwtBearer:SecurityKey"] = "Authentication__JwtBearer__SecurityKey",
                ["DataProtection:CertificateBase64"] = "DataProtection__CertificateBase64",
                ["DataProtection:CertificatePassword"] = "DataProtection__CertificatePassword",
                ["Redis:Configuration"] = "Redis__Configuration"
            };
            if (!recovery)
            {
                requiredSettings["Yoco:SecretKey"] = "Yoco__SecretKey";
                requiredSettings["Yoco:Mode"] = "Yoco__Mode";
            }
            var missing = new List<string>();
            if (!IsConfigured(configuration["ConnectionStrings:Default"]) &&
                !IsConfigured(configuration["DATABASE_URL"]))
            {
                missing.Add("ConnectionStrings__Default or DATABASE_URL");
            }

            foreach (var setting in requiredSettings)
            {
                if (!IsConfigured(configuration[setting.Key]))
                {
                    missing.Add(setting.Value);
                }
            }

            if (!recovery && !configuration.GetValue<bool>("Bird:Enabled"))
            {
                missing.Add("Bird__Enabled=true");
            }

            var birdSettings = new Dictionary<string, string>
            {
                ["Bird:ApiKey"] = "Bird__ApiKey",
                ["Bird:FromEmail"] = "Bird__FromEmail",
                ["Bird:FromName"] = "Bird__FromName",
                ["Bird:ReplyToEmail"] = "Bird__ReplyToEmail"
            };
            foreach (var setting in recovery ? new Dictionary<string, string>() : birdSettings)
            {
                if (!IsConfigured(configuration[setting.Key])) missing.Add(setting.Value);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Production configuration is incomplete. Set: " + string.Join(", ", missing));

            ValidateSecurePublicAddress(configuration, "App:ServerRootAddress", "App__ServerRootAddress");
            ValidateSecurePublicAddress(configuration, "App:ClientRootAddress", "App__ClientRootAddress");
            if (recovery) return; // Common security configuration remains mandatory; providers stay unavailable.

            TransactionalEmailDeliveryBoundary.Require(configuration);
            if (yocoMode == "test")
                foreach (var key in new[] { "App:FinancialOperations:Enabled", "App:WeeklyCommissions:Enabled",
                    "App:EntryMonthlyObligations:Enabled", "App:BackgroundJobs:Enabled" })
                    if (configuration.GetValue<bool>(key))
                        throw new InvalidOperationException("Production payment testing requires " + key.Replace(":", "__") + "=false.");

            if (!BirdOptions.TryResolveRegion(configuration["Bird:ApiKey"], out _))
            {
                throw new InvalidOperationException(
                    "Production Bird configuration is invalid. Bird__ApiKey must be a current Bird API key.");
            }

            var expectedYocoKeyPrefix = yocoMode == "live"
                ? "sk_live_"
                : yocoMode == "test"
                    ? "sk_test_"
                    : null;
            if (expectedYocoKeyPrefix == null ||
                !yocoSecretKey.StartsWith(expectedYocoKeyPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Production Yoco configuration is invalid. Yoco__Mode must be test or live and must match the Yoco__SecretKey prefix.");
            }

        }

        private static void ValidateSecurePublicAddress(
            IConfiguration configuration,
            string key,
            string environmentName)
        {
            if (!Uri.TryCreate(configuration[key], UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"Production configuration is invalid. {environmentName} must be a secure HTTPS URL.");
            }
        }

        private static bool IsConfigured(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !value.StartsWith("<set-via-", StringComparison.Ordinal);
        }
    }
}
