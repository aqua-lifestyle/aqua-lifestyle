using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Models
{
    /// <summary>
    /// Non-sensitive identifiers used to prove which API contract is running.
    /// </summary>
    public static class DeploymentMetadata
    {
        public const string PaymentContractVersion = "aqua-payments-2026-08-01";

        public static readonly IReadOnlyList<string> ContractCapabilities = new[]
        {
            "aqgreen-joining-schedules-v1",
            "direct-onyx-checkout-v1",
            "admin-onyx-graduation-v1",
            "admin-checkout-recovery-v1"
        };

        public static string ResolveBuildId(IConfiguration configuration) =>
            FirstSafeIdentifier(
                configuration["Deployment:BuildId"],
                configuration["RENDER_GIT_COMMIT"],
                configuration["SOURCE_VERSION"],
                configuration["GIT_COMMIT"]);

        public static string ResolveImageId(IConfiguration configuration) =>
            FirstSafeIdentifier(
                configuration["Deployment:ImageId"],
                configuration["RENDER_IMAGE_ID"]);

        public static string ResolveEnvironmentId(IConfiguration configuration) =>
            FirstSafeIdentifier(configuration["Deployment:EnvironmentId"]);

        private static string FirstSafeIdentifier(params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var value = candidate?.Trim();
                if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
                    continue;

                var isSafe = true;
                foreach (var character in value)
                {
                    if (!char.IsLetterOrDigit(character) &&
                        character != '-' && character != '_' && character != '.')
                    {
                        isSafe = false;
                        break;
                    }
                }

                if (isSafe)
                    return value;
            }

            return "unavailable";
        }
    }
}
