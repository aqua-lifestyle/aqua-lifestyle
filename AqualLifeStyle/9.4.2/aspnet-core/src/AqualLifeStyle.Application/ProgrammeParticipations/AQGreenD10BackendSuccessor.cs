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
        public const string IndependentRootMigration = "20261125202325_UseLiveEntryParticipationUniqueIndex";
        public const string PreviousMigration = "20261115113914_AddAQGreenIndependentRootRequests";
        public const string AdministratorAdmissionMigration = "20261128123707_AddAQGreenAdministratorAdmission";
        public const string AdministratorAdmissionSchemaSha256 = "68aa40dd42169d4c52ed03011bc070bea4b319af7015cadf7e84c8981d788159";
        public const string HistoricalReviewCancellationMigration = "20261128142657_GuardAQGreenHistoricalReviewCancellation";
        public const string HistoricalReviewCancellationSchemaSha256 = "e61faf284d84b64278f4c808c2fbc858916bcb76ace9f436b04a2139059a1dd8";

        // Read-only SHA-256 of the production public-column snapshot. Migration
        // 57 changes only an index predicate, so it preserves this column-schema
        // fingerprint while the exact migration-history pin advances.
        public const string IndependentRootSchemaSha256 =
            "27c55cd3dfbb93319aeeb17b463912fc7c366367a056aee7d0fc7aa629ad3664";

        private static readonly Regex RenderRevision = new("\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant);

        public static bool Accept(AQGreenD10Decision decision, AQGreenReconciliationSourceBoundary boundary,
            IConfiguration configuration)
        {
            Require(decision != null && boundary != null && configuration != null,
                "D10_BACKEND_SUCCESSOR_SCOPE_UNAVAILABLE");
            Require(boundary.SourceDatabaseIdentityEvidence == decision.RequiredDatabaseIdentityDigest,
                "D10_BACKEND_SUCCESSOR_DATABASE_IDENTITY_MISMATCH");
            var expected = boundary.ExpectedMigrations;
            var applied = boundary.AppliedMigrations;
            var cancellation = expected?.Length == 59 && expected[^4] == PreviousMigration &&
                expected[^3] == IndependentRootMigration && expected[^2] == AdministratorAdmissionMigration &&
                expected[^1] == HistoricalReviewCancellationMigration;
            var admission = expected?.Length == 58 && expected[^3] == PreviousMigration &&
                expected[^2] == IndependentRootMigration && expected[^1] == AdministratorAdmissionMigration;
            var independent = expected?.Length == 57 && expected[^2] == PreviousMigration &&
                expected[^1] == IndependentRootMigration;
            Require(boundary.SourceSchemaVersion == (cancellation ? HistoricalReviewCancellationSchemaSha256 :
                    admission ? AdministratorAdmissionSchemaSha256 : IndependentRootSchemaSha256),
                "D10_BACKEND_SUCCESSOR_SCHEMA_MISMATCH");
            Require((cancellation || admission || independent) && applied != null && expected.SequenceEqual(applied, StringComparer.Ordinal),
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
