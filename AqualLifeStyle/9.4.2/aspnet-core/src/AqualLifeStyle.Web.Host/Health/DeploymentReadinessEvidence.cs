using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Web.Host.Startup;
using AqualLifeStyle.Web.Host.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System.IO;
using System.Text.Json.Serialization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.EntityFrameworkCore;

namespace AqualLifeStyle.Web.Host.Health
{
    public sealed class DeploymentReadinessEvidence
    {
        public static readonly IReadOnlyList<string> HeldSettings = Array.AsReadOnly(new[] {
            "App:WeeklyCommissions:Enabled", "App:EntryMonthlyObligations:Enabled",
            "App:FinancialOperations:Enabled", "App:BackgroundJobs:Enabled", "AQGreenV2Demo:Enabled",
            "AQGreenV2Pilot:Enabled", "App:AQGreenV2:Commissioning:Enabled", "SEED_DEMO_DATA"
        });
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly IChangeToken reload;

        public DeploymentReadinessEvidence(IWebHostEnvironment environment, IConfiguration configuration)
        {
            // Reuse the actual validator, inside the application runtime only.
            // No secret or hash of a secret is retained in this evidence object.
            DeploymentConfigurationValidator.Validate(environment, configuration);
            this.environment = environment;
            this.configuration = configuration;
            reload = configuration.GetReloadToken();
        }

        public DeploymentReadinessResponse Read(string expectedRevision)
        {
            var revision = DeploymentMetadata.ResolveBuildId(configuration);
            var response = new DeploymentReadinessResponse { BuildId = revision };
            var liveCleanStart = configuration["App:AQGreenV2:Commissioning:AdmissionMode"] == "CLEAN_START" &&
                configuration["Yoco:Mode"] == "live";
            if (liveCleanStart) response.Version = 2;
            bool recovery, bird, jwt;
            if (!environment.IsProduction() || reload.HasChanged || !IsSha(expectedRevision) ||
                revision != expectedRevision || !bool.TryParse(configuration["App:Recovery:Enabled"], out recovery) || recovery ||
                !bool.TryParse(configuration["Bird:Enabled"], out bird) || !bird ||
                !bool.TryParse(configuration["Authentication:JwtBearer:IsEnabled"], out jwt) || !jwt ||
                !HeldSettings.All(key => bool.TryParse(configuration[key], out var enabled) && !enabled))
                return response;
            try
            {
                DeploymentConfigurationValidator.Validate(environment, configuration);
                if (liveCleanStart)
                {
                    var efDigest = AQGreenB6CanonicalEvidence.Hash(File.ReadAllBytes(typeof(AQGreenReconciliationEvidenceSnapshot).Assembly.Location));
                    response.D10Bindings = new D10RuntimeBindingResponse
                    {
                        RequiredAssemblyDigest = efDigest,
                        RequiredRuntimeAssemblyDigest = AQGreenD10Runtime.RuntimeAssemblyDigest(efDigest),
                        CurrentConfigurationDigest = AQGreenD10Commissioning.ConfigurationDigest(configuration),
                        IntendedConfigurationDigest = AQGreenD10Commissioning.IntendedLiveJoiningConfigurationDigest(configuration, "production-release", revision),
                        IntendedEnvironmentId = "production-release", IntendedBuildId = revision
                    };
                }
            }
            catch { return response; } // Never return validator exception/configuration text.
            response.Status = "Ready";
            response.BirdConfiguration = "StartupValidated";
            response.YocoConfiguration = "StartupValidated";
            response.Profile = "normal-integrated";
            response.RestrictedOperations = "ConfigurationHeld";
            return response;
        }

        private static bool IsSha(string value) => value != null && value.Length == 40 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }

    // A closed response shape: no environment values, credentials, user data or
    // claim that configuration flags establish database/IAM privilege isolation.
    public sealed class DeploymentReadinessResponse
    {
        public string Contract { get; } = "aqua.production-readiness";
        public int Version { get; set; } = 1;
        public string Status { get; set; } = "Unavailable";
        public string BuildId { get; set; }
        public string BirdConfiguration { get; set; } = "Unavailable";
        public string YocoConfiguration { get; set; } = "Unavailable";
        public string Profile { get; set; } = "Unavailable";
        public string RestrictedOperations { get; set; } = "Unavailable";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public D10RuntimeBindingResponse D10Bindings { get; set; }
    }

    // Protected metadata only: no configuration values, private paths, secrets or
    // hashes of secrets. The planned digest is explicitly not current enablement.
    public sealed class D10RuntimeBindingResponse
    {
        public string RequiredAssemblyDigest { get; set; }
        public string RequiredRuntimeAssemblyDigest { get; set; }
        public string CurrentConfigurationDigest { get; set; }
        public string IntendedConfigurationDigest { get; set; }
        public string IntendedEnvironmentId { get; set; }
        public string IntendedBuildId { get; set; }
    }
}
