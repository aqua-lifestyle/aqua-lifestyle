using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    /// <summary>
    /// Post-Normal backend successor acceptance. The running AQGreen D10 runtime
    /// accepts a software or additive schema transition away from the originally
    /// commissioned D10 pins only through this input, which is staged by the
    /// protected release pipeline (owner-approved GitHub merge, qualified source
    /// and executor) through the existing environment delivery channel and a
    /// raw SHA-256 pin. Every recomputable binding is re-derived here from the
    /// runtime's own commissioned decision, live configuration and observed
    /// source boundary; nothing in the input is trusted on its own.
    /// </summary>
    public static class AQGreenD10BackendSuccessor
    {
        public const string ContractVersion = "AQGreenD10BackendSuccessorV1";
        public const string PayloadKey = "App:AQGreenV2:D10:BackendSuccessorJsonBase64";
        public const string PayloadPinKey = "App:AQGreenV2:D10:BackendSuccessorSha256";
        public const string RevisionEnvironmentKey = "RENDER_GIT_COMMIT";
        public const string SchemaTransitionNone = "none";
        public const string SchemaTransitionAdditive = "additive";

        private static readonly string[] PayloadProperties =
        {
            "contractVersion", "predecessorDecisionSha256", "predecessorSchemaSha256",
            "predecessorAssemblySha256", "predecessorRuntimeAssemblySha256",
            "predecessorDatabaseIdentitySha256", "predecessorBuildId", "successorRevisionSha256",
            "successorSchemaSha256", "successorAssemblySha256", "successorApplicationSha256",
            "successorCoreSha256", "schemaTransition", "releaseRunId", "releaseRunAttempt", "ciRunId", "ciRunAttempt",
            "executorSha256"
        };

        private const int MaxPayloadBytes = 16384;

        /// <summary>
        /// Absence of a staged successor input returns false and the existing
        /// D10_DATABASE_SCHEMA_OR_RELEASE_MISMATCH gate applies unchanged. A
        /// staged but unverifiable input throws a specific D10 error code, so
        /// the fail-closed boundary never silently degrades to the historical
        /// rejection code while an authorization is being replaced.
        /// </summary>
        public static bool Accept(AQGreenD10Decision decision, AQGreenReconciliationSourceBoundary boundary,
            IConfiguration configuration)
        {
            var payload = configuration[PayloadKey];
            if (payload == null && configuration[PayloadPinKey] == null) return false;
            Require(decision != null && boundary != null && configuration != null, "D10_BACKEND_SUCCESSOR_SCOPE_UNAVAILABLE");

            var pin = configuration[PayloadPinKey];
            Require(!string.IsNullOrWhiteSpace(pin) && Regex.IsMatch(pin, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant),
                "D10_BACKEND_SUCCESSOR_PIN_INVALID");
            Require(!string.IsNullOrWhiteSpace(payload), "D10_BACKEND_SUCCESSOR_PAYLOAD_MISSING");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(payload); }
            catch (FormatException) { throw new InvalidOperationException("D10_BACKEND_SUCCESSOR_PAYLOAD_ENCODING_INVALID"); }
            Require(bytes.Length > 0 && bytes.Length <= MaxPayloadBytes, "D10_BACKEND_SUCCESSOR_PAYLOAD_SIZE_INVALID");
            Require(AQGreenB6CanonicalEvidence.Hash(bytes) == pin, "D10_BACKEND_SUCCESSOR_PINNED_INPUT_CHANGED");

            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4
            });
            var root = document.RootElement;
            Require(root.ValueKind == JsonValueKind.Object &&
                root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
                    .SequenceEqual(PayloadProperties.OrderBy(name => name, StringComparer.Ordinal)),
                "D10_BACKEND_SUCCESSOR_INPUT_SHAPE_INVALID");

            Require(String(root, "contractVersion") == ContractVersion, "D10_BACKEND_SUCCESSOR_CONTRACT_CHANGED");
            var predecessorDecisionSha256 = Hash(root, "predecessorDecisionSha256");
            Require(AQGreenB6CanonicalEvidence.Digest(decision) == predecessorDecisionSha256,
                "D10_BACKEND_SUCCESSOR_PREDECESSOR_DECISION_CHANGED");
            Require(Hash(root, "predecessorSchemaSha256") == decision.RequiredSchemaDigest &&
                Hash(root, "predecessorAssemblySha256") == decision.RequiredAssemblyDigest &&
                Hash(root, "predecessorRuntimeAssemblySha256") == decision.RequiredRuntimeAssemblyDigest &&
                Hash(root, "predecessorDatabaseIdentitySha256") == decision.RequiredDatabaseIdentityDigest,
                "D10_BACKEND_SUCCESSOR_PREDECESSOR_BINDING_MISMATCH");
            var predecessorBuildId = String(root, "predecessorBuildId");
            Require(predecessorBuildId == configuration["Deployment:BuildId"] &&
                Regex.IsMatch(predecessorBuildId, "\\A[A-Za-z0-9._:+/@-]{1,128}\\z", RegexOptions.CultureInvariant),
                "D10_BACKEND_SUCCESSOR_PREDECESSOR_BUILD_MISMATCH");

            var successorRevisionSha256 = String(root, "successorRevisionSha256");
            Require(successorRevisionSha256 == configuration[RevisionEnvironmentKey] &&
                Regex.IsMatch(successorRevisionSha256, "\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant),
                "D10_BACKEND_SUCCESSOR_REVISION_MISMATCH");

            var schemaTransition = String(root, "schemaTransition");
            Require(schemaTransition is SchemaTransitionNone or SchemaTransitionAdditive,
                "D10_BACKEND_SUCCESSOR_SCHEMA_TRANSITION_INVALID");
            Require(schemaTransition == (boundary.SourceSchemaVersion == decision.RequiredSchemaDigest ?
                SchemaTransitionNone : SchemaTransitionAdditive),
                "D10_BACKEND_SUCCESSOR_SCHEMA_TRANSITION_MISMATCH");

            Require(Positive(root, "releaseRunId") && Positive(root, "releaseRunAttempt") &&
                Positive(root, "ciRunId") && Positive(root, "ciRunAttempt") &&
                Regex.IsMatch(String(root, "executorSha256"), "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant),
                "D10_BACKEND_SUCCESSOR_RELEASE_PROVENANCE_INVALID");
            Require(Hash(root, "successorSchemaSha256") == boundary.SourceSchemaVersion &&
                Hash(root, "successorAssemblySha256") == boundary.SourceAssemblyDigest &&
                Hash(root, "successorApplicationSha256") == AQGreenB6CanonicalEvidence.Hash(
                    File.ReadAllBytes(typeof(AQGreenD10BackendSuccessor).Assembly.Location)) &&
                Hash(root, "successorCoreSha256") == AQGreenB6CanonicalEvidence.Hash(
                    File.ReadAllBytes(typeof(AQGreenD10Decision).Assembly.Location)),
                "D10_BACKEND_SUCCESSOR_OBSERVED_RELEASE_MISMATCH");


            return true;
        }

        private static string String(JsonElement root, string name)
        {
            var value = root.GetProperty(name);
            Require(value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 512 },
                "D10_BACKEND_SUCCESSOR_INPUT_SHAPE_INVALID");
            return value.GetString();
        }

        private static string Hash(JsonElement root, string name) => String(root, name);

        private static bool Positive(JsonElement root, string name)
        {
            var value = root.GetProperty(name);
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number > 0;
        }

        private static void Require(bool condition, string code)
        { if (!condition) throw new InvalidOperationException(code); }
    }
}
