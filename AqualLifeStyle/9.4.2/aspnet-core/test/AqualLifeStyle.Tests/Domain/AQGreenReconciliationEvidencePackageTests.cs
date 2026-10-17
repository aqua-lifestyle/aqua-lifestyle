using System;
using System.Collections.Generic;
using System.Text.Json;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AQGreenReconciliationEvidencePackageTests
    {
        private static readonly DateTime ObservedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(false, "default-cutoff")]
        [InlineData(true, "default-cutoff")]
        [InlineData(false, "default-both")]
        [InlineData(true, "default-both")]
        [InlineData(false, "unspecified-observation")]
        [InlineData(true, "unspecified-observation")]
        [InlineData(false, "local-observation")]
        [InlineData(true, "local-observation")]
        [InlineData(false, "missing-boundary")]
        [InlineData(true, "missing-boundary")]
        [InlineData(false, "future-cutoff")]
        [InlineData(true, "future-cutoff")]
        [InlineData(false, "unspecified-cutoff")]
        [InlineData(true, "unspecified-cutoff")]
        public void FreshChecksumsCannotMakeAnInvalidObservationBoundaryValid(bool legacy, string failure)
        {
            var manifest = Manifest(legacy);
            switch (failure)
            {
                case "default-cutoff": manifest.AnalysisSnapshotCutoffUtc = DateTime.SpecifyKind(default, DateTimeKind.Utc); break;
                case "default-both":
                    manifest.AnalysisSnapshotCutoffUtc = DateTime.SpecifyKind(default, DateTimeKind.Utc);
                    manifest.SourceBoundary.SourceObservedAtUtc = DateTime.SpecifyKind(default, DateTimeKind.Utc);
                    break;
                case "unspecified-observation": manifest.SourceBoundary.SourceObservedAtUtc = DateTime.SpecifyKind(ObservedAt, DateTimeKind.Unspecified); break;
                case "local-observation": manifest.SourceBoundary.SourceObservedAtUtc = DateTime.SpecifyKind(ObservedAt, DateTimeKind.Local); break;
                case "missing-boundary": manifest.SourceBoundary = null; break;
                case "future-cutoff": manifest.AnalysisSnapshotCutoffUtc = ObservedAt.AddTicks(1); break;
                case "unspecified-cutoff": manifest.AnalysisSnapshotCutoffUtc = DateTime.SpecifyKind(ObservedAt, DateTimeKind.Unspecified); break;
            }

            // Freeze recomputes every checksum before calling the offline verifier.
            // Rejection must be semantic, not merely a stale-hash failure.
            Should.Throw<InvalidOperationException>(() => AQGreenReconciliationEvidencePackage.Freeze(
                manifest, new List<AQGreenReconciliationEvidenceRecord>()))
                .Message.ShouldBe("Reconciliation evidence observation boundary is invalid.");

            // Direct offline verification enforces the same boundary before hash
            // checks, even when the caller does not use the Freeze entrypoint.
            var offline = AQGreenReconciliationEvidencePackage.Freeze(Manifest(legacy),
                new List<AQGreenReconciliationEvidenceRecord>());
            offline.Manifest = manifest;
            Should.Throw<InvalidOperationException>(offline.Verify)
                .Message.ShouldBe("Reconciliation evidence observation boundary is invalid.");
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ValidUtcBoundaryRoundTripsWithoutChangingEvidenceBytes(bool legacy, bool earlierCutoff)
        {
            var manifest = Manifest(legacy);
            if (earlierCutoff) manifest.AnalysisSnapshotCutoffUtc = ObservedAt.AddDays(-1);
            var package = AQGreenReconciliationEvidencePackage.Freeze(manifest,
                new List<AQGreenReconciliationEvidenceRecord>());
            var bytes = AQGreenB6CanonicalEvidence.Bytes(package);
            var offline = JsonSerializer.Deserialize<AQGreenReconciliationEvidencePackage>(bytes, AQGreenB6CanonicalEvidence.Options);

            offline.Verify();
            offline.Manifest.AnalysisSnapshotCutoffUtc.ShouldBe(manifest.AnalysisSnapshotCutoffUtc);
            offline.Manifest.SourceBoundary.SourceObservedAtUtc.ShouldBe(ObservedAt);
            AQGreenB6CanonicalEvidence.Bytes(offline).ShouldBe(bytes);
        }

        private static AQGreenReconciliationEvidenceManifest Manifest(bool legacy) => new()
        {
            PackageVersion = legacy ? AQGreenReconciliationEvidenceManifest.LegacyVersion : AQGreenReconciliationEvidenceManifest.Version,
            TenantId = 1,
            WholeTenant = true,
            AuthorizedAreaIds = Array.Empty<Guid>(),
            AnalysisSnapshotCutoffUtc = ObservedAt,
            ExportImplementationDigest = new string('a', 64),
            SourceBoundary = new AQGreenReconciliationSourceBoundary
            {
                SourceObservedAtUtc = ObservedAt,
                SourcePostgreSqlVersion = "16.0-synthetic",
                SourceMvccSnapshotEvidence = "synthetic-snapshot",
                SourceDatabaseIdentityEvidence = "synthetic-database",
                SourceSchemaVersion = "synthetic-schema",
                SourceAssemblyDigest = new string('b', 64),
                TransactionMode = "REPEATABLE READ READ ONLY"
            }
        };
    }
}
