using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                ["ConnectionStrings:Default"] = "ConnectionStrings__Default or DATABASE_URL",
                ["App:ServerRootAddress"] = "App__ServerRootAddress",
                ["App:ClientRootAddress"] = "App__ClientRootAddress",
                ["App:CorsOrigins"] = "App__CorsOrigins",
                ["Authentication:JwtBearer:SecurityKey"] = "Authentication__JwtBearer__SecurityKey",
                ["Redis:Configuration"] = "Redis__Configuration"
            };
            var missing = new List<string>();
            foreach (var setting in requiredSettings)
            {
                var value = configuration[setting.Key];
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith("<set-via-", StringComparison.Ordinal))
                {
                    missing.Add(setting.Value);
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production configuration is incomplete. Set: " + string.Join(", ", missing));
            }
        }
    }
}
