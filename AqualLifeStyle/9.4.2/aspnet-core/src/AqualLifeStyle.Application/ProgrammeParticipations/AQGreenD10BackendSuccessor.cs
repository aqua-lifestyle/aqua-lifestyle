using System;
using System.Linq;
using System.Text.RegularExpressions;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    /// <summary>
    /// Recognizes the one reviewed additive schema transition required by the
    /// independent-root release. Release provenance is established before a
    /// Render deployment by protected CI and the selected Render commit; it is
    /// not reconstructed from mutable runtime configuration on every request.
    /// The live database identity, exact migration history and observed schema
    /// fingerprint remain runtime-enforced by the D10 operation gates.
    /// </summary>
    public static class AQGreenD10BackendSuccessor
    {
        public const string IndependentRootMigration = "20261115113914_AddAQGreenIndependentRootRequests";
        public const string PreviousMigration = "20261021051549_AddAQGreenSalesEvidenceInvalidation";

        // Read-only SHA-256 of the production public-column snapshot after the
        // independently authorized migration 56. It is a schema pin, not an
        // authorization credential. A future schema release must add its own
        // reviewed migration and expected fingerprint.
        public const string IndependentRootSchemaSha256 =
            "48be3ea9e597fe7f5a5474472a757ef20143574f7114029189dba65cb7e95301";

        private static readonly Regex RenderRevision = new("\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant);

        public static bool Accept(AQGreenD10Decision decision, AQGreenReconciliationSourceBoundary boundary,
            IConfiguration configuration)
        {
            Require(decision != null && boundary != null && configuration != null,
                "D10_BACKEND_SUCCESSOR_SCOPE_UNAVAILABLE");
            Require(boundary.SourceDatabaseIdentityEvidence == decision.RequiredDatabaseIdentityDigest,
                "D10_BACKEND_SUCCESSOR_DATABASE_IDENTITY_MISMATCH");
            Require(boundary.SourceSchemaVersion == IndependentRootSchemaSha256,
                "D10_BACKEND_SUCCESSOR_SCHEMA_MISMATCH");

            var expected = boundary.ExpectedMigrations;
            var applied = boundary.AppliedMigrations;
            Require(expected != null && applied != null && expected.Length == 56 &&
                expected[^2] == PreviousMigration && expected[^1] == IndependentRootMigration &&
                expected.SequenceEqual(applied, StringComparer.Ordinal),
                "D10_BACKEND_SUCCESSOR_MIGRATION_HISTORY_MISMATCH");

            // Render supplies the immutable source revision for the selected
            // deployment. The D10 commissioning BuildId remains bound by the
            // original configuration digest and is intentionally not rewritten.
            Require(RenderRevision.IsMatch(configuration["RENDER_GIT_COMMIT"] ?? string.Empty),
                "D10_BACKEND_SUCCESSOR_DEPLOYMENT_IDENTITY_MISSING");
            return true;
        }

        private static void Require(bool condition, string code)
        { if (!condition) throw new InvalidOperationException(code); }
    }
}
