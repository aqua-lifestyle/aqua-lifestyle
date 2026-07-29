using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AqualLifeStyle.Web.Host.Email;

namespace AqualLifeStyle.Web.Host.Startup
{
    public static class DeploymentConfigurationValidator
    {
        public static void Validate(IServiceProvider services)
        {
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            if (!environment.IsProduction())
            {
                return;
            }

            var configuration = services.GetRequiredService<IConfiguration>();
            var requiredSettings = new Dictionary<string, string>
            {
                ["App:ServerRootAddress"] = "App__ServerRootAddress",
                ["App:ClientRootAddress"] = "App__ClientRootAddress",
                ["App:CorsOrigins"] = "App__CorsOrigins",
                ["Authentication:JwtBearer:SecurityKey"] = "Authentication__JwtBearer__SecurityKey",
                ["Redis:Configuration"] = "Redis__Configuration",
                ["Yoco:SecretKey"] = "Yoco__SecretKey",
                ["Yoco:WebhookSecret"] = "Yoco__WebhookSecret",
                ["Yoco:Mode"] = "Yoco__Mode"
            };
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

            if (!configuration.GetValue<bool>("Bird:Enabled"))
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
            foreach (var setting in birdSettings)
            {
                if (!IsConfigured(configuration[setting.Key])) missing.Add(setting.Value);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Production configuration is incomplete. Set: " + string.Join(", ", missing));

            if (!BirdOptions.TryResolveRegion(configuration["Bird:ApiKey"], out _))
            {
                throw new InvalidOperationException(
                    "Production Bird configuration is invalid. Bird__ApiKey must be a current Bird API key.");
            }

            var yocoMode = configuration["Yoco:Mode"]?.Trim().ToLowerInvariant();
            var yocoSecretKey = configuration["Yoco:SecretKey"]?.Trim();
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

        private static bool IsConfigured(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !value.StartsWith("<set-via-", StringComparison.Ordinal);
        }
    }
}
