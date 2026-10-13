using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AqualLifeStyle.Domain.AQGreen
{
    // Restricted operator evidence: technical source identifiers, never a sanitised
    // or approved B6 BASE_SNAPSHOT. Historical acceptance is a separate operation.
    public sealed class AQGreenReconciliationEvidencePackage
    {
        public AQGreenReconciliationEvidenceManifest Manifest { get; set; }
        public List<AQGreenReconciliationEvidenceRecord> Records { get; set; }
        public string Sha256Sums { get; set; }

        public static AQGreenReconciliationEvidencePackage Freeze(
            AQGreenReconciliationEvidenceManifest manifest, List<AQGreenReconciliationEvidenceRecord> records)
        {
            var package = new AQGreenReconciliationEvidencePackage { Manifest = manifest, Records = records };
            foreach (var record in records) record.RecordDigest = AQGreenB6CanonicalEvidence.Digest(record, "recordDigest");
            manifest.RecordCount = records.Count;
            manifest.ClassificationCounts = Counts(records);
            manifest.SourceRecordCounts = SourceCounts(records);
            manifest.CandidatePopulationDigest = PopulationDigest(records);
            manifest.RecordsDigest = AQGreenB6CanonicalEvidence.Digest(records);
            manifest.OverallSnapshotDigest = ContentDigest(manifest);
            manifest.ManifestHash = AQGreenB6CanonicalEvidence.Digest(manifest, "manifestHash");
            package.Sha256Sums = package.Checksums();
            package.Verify();
            return package;
        }

        public void Verify()
        {
            void Require(bool condition) { if (!condition) throw new InvalidOperationException("Reconciliation evidence integrity check failed."); }
            Require(Manifest != null && Records != null);
            Require(Manifest.PackageVersion == AQGreenReconciliationEvidenceManifest.Version &&
                Manifest.TenantId > 0 && Manifest.AnalysisSnapshotCutoffUtc.Kind == DateTimeKind.Utc &&
                Manifest.AnalysisSnapshotCutoffUtc <= Manifest.SourceBoundary.SourceObservedAtUtc &&
                Manifest.SourceBoundary.TransactionMode == "REPEATABLE READ READ ONLY");
            Require(Manifest.AuthorizedAreaIds != null && Manifest.AuthorizedAreaIds.SequenceEqual(
                Manifest.AuthorizedAreaIds.Distinct().OrderBy(id => id)));
            Require(Records.Count == Manifest.RecordCount && Records.Select(r => r.DiscoveryId).Distinct().Count() == Records.Count);
            Require(Records.Select(r => r.CustomerId).SequenceEqual(Records.Select(r => r.CustomerId).Distinct().OrderBy(id => id)));
            Require(Records.Where(r => r.ParticipationId.HasValue).Select(r => r.ParticipationId).Distinct().Count() ==
                Records.Count(r => r.ParticipationId.HasValue));
            foreach (var record in Records)
            {
                Require(record.TenantId == Manifest.TenantId && record.CustomerId > 0 &&
                    record.DiscoveryId == FormattableString.Invariant($"aqgreen:{record.TenantId}:{record.CustomerId}") &&
                    (Manifest.WholeTenant || (record.CurrentAreaId.HasValue && Manifest.AuthorizedAreaIds.Contains(record.CurrentAreaId.Value))) &&
                    record.RecordDigest == AQGreenB6CanonicalEvidence.Digest(record, "recordDigest"));
            }
            Require(Manifest.RecordsDigest == AQGreenB6CanonicalEvidence.Digest(Records) &&
                Manifest.CandidatePopulationDigest == PopulationDigest(Records) &&
                AQGreenB6CanonicalEvidence.Digest(Manifest.ClassificationCounts) == AQGreenB6CanonicalEvidence.Digest(Counts(Records)) &&
                AQGreenB6CanonicalEvidence.Digest(Manifest.SourceRecordCounts) == AQGreenB6CanonicalEvidence.Digest(SourceCounts(Records)) &&
                Manifest.OverallSnapshotDigest == ContentDigest(Manifest) &&
                Manifest.ManifestHash == AQGreenB6CanonicalEvidence.Digest(Manifest, "manifestHash") && Sha256Sums == Checksums());
        }

        // Write these exact bytes as records.json / manifest.json, and Sha256Sums
        // as UTF-8 SHA256SUMS. No filesystem or database writes in this library.
        public byte[] RecordsBytes() => AQGreenB6CanonicalEvidence.Bytes(Records);
        public byte[] ManifestBytes() => AQGreenB6CanonicalEvidence.Bytes(Manifest);
        private string Checksums() => $"{AQGreenB6CanonicalEvidence.Hash(RecordsBytes())}  records.json\n{AQGreenB6CanonicalEvidence.Hash(ManifestBytes())}  manifest.json\n";
        private static SortedDictionary<string, int> Counts(IEnumerable<AQGreenReconciliationEvidenceRecord> records) =>
            new(records.GroupBy(r => r.DiscoveryKind).ToDictionary(g => g.Key, g => g.Count()), StringComparer.Ordinal);
        private static SortedDictionary<string, int> SourceCounts(IReadOnlyCollection<AQGreenReconciliationEvidenceRecord> records) => new(StringComparer.Ordinal)
        {
            ["Customers"] = records.Count,
            ["LegacyAQGreenAssignments"] = records.Count(r => r.LegacyMembershipId.HasValue),
            ["EntryParticipations"] = records.Count(r => r.ParticipationId.HasValue),
            ["MemberPayments"] = records.Sum(r => r.JoiningPayments.Count),
            ["AQGreenJoiningCheckouts"] = records.Sum(r => r.CheckoutAttempts.Count),
            ["EntryParticipationApprovalDecisions"] = records.Sum(r => r.ApprovalDecisions.Count),
            ["EntryRecruiterCorrections"] = records.Sum(r => r.RecruiterCorrections.Count),
            ["AQGreenRecruitmentAttributions"] = records.Count(r => r.Attribution != null),
            ["AQGreenRecruitmentAttributionConfirmations"] = records.Count(r => r.AttributionConfirmation != null),
            ["AQGreenNetworkPlacements"] = records.Count(r => r.Placement != null)
        };
        private static string PopulationDigest(IEnumerable<AQGreenReconciliationEvidenceRecord> records) =>
            AQGreenB6CanonicalEvidence.Tuples(records.Select(r => new[] { r.TenantId.ToString(CultureInfo.InvariantCulture),
                r.CustomerId.ToString(CultureInfo.InvariantCulture), r.ParticipationId?.ToString("D") ?? @"\N", r.RecordDigest }));
        private static string ContentDigest(AQGreenReconciliationEvidenceManifest m) => AQGreenB6CanonicalEvidence.Digest(new
        {
            m.PackageVersion, m.TenantId, m.WholeTenant, m.AuthorizedAreaIds, m.AnalysisSnapshotCutoffUtc,
            m.PopulationDefinition, m.SourceTables, m.SourceBoundary.SourceSchemaVersion, m.ExportImplementationDigest,
            m.RecordCount, m.ClassificationCounts, m.SourceRecordCounts, m.CandidatePopulationDigest, m.RecordsDigest
        });

        // The caller supplies reviewed, source-bound B6 candidate classifications.
        // This never derives roots, sponsors, ordering, eligibility or missing dates.
        // A full inventory must be accounted for, including legacy-only records.
        public AQGreenLegacyCanonicalisationResult SimulateWholeScope(
            Guid reviewedScopeSnapshotKey, IReadOnlyCollection<AQGreenReconciliationSimulationInput> review)
        {
            Verify();
            if (!Manifest.WholeTenant || review == null || review.Count != Records.Count ||
                review.Select(r => r.DiscoveryId).Distinct().Count() != Records.Count)
                throw new InvalidOperationException("Whole-scope simulation requires the complete Tenant inventory and explicit review of every source record.");
            var sources = Records.ToDictionary(r => r.DiscoveryId);
            foreach (var item in review)
            {
                if (!sources.TryGetValue(item.DiscoveryId, out var source) || item.Candidate == null ||
                    item.Candidate.TenantSnapshotKey != Manifest.TenantId ||
                    item.Candidate.SourceEvidenceDigest != source.RecordDigest)
                    throw new InvalidOperationException("Simulation review does not bind to frozen source evidence.");
            }
            return AQGreenLegacyCanonicalisationSimulator.Simulate(new AQGreenLegacyCanonicalisationRequest
            {
                TenantSnapshotKey = Manifest.TenantId, PlacementTreeScopeSnapshotKey = reviewedScopeSnapshotKey,
                AnalysisSnapshotCutoffUtc = Manifest.AnalysisSnapshotCutoffUtc,
                Candidates = review.Select(r => r.Candidate).ToArray()
            });
        }
    }

    public sealed class AQGreenReconciliationSimulationInput
    {
        public string DiscoveryId { get; set; }
        public AQGreenLegacyCanonicalisationCandidate Candidate { get; set; }
    }

    public sealed class AQGreenReconciliationEvidenceManifest
    {
        public const string Version = "AQGreenB6ReconciliationDiscoveryEvidenceV1";
        public string PackageVersion { get; set; } = Version;
        public int TenantId { get; set; }
        public bool WholeTenant { get; set; }
        public Guid[] AuthorizedAreaIds { get; set; }
        public DateTime AnalysisSnapshotCutoffUtc { get; set; }
        public AQGreenReconciliationSourceBoundary SourceBoundary { get; set; }
        public string ExportImplementationDigest { get; set; }
        public string PopulationDefinition { get; set; } = "Current nondeleted customers with AQGreen membership assignment, unverifiable assignment, or AQGreen participation; observed inventory, not historical eligibility.";
        public string[] SourceTables { get; set; } = new[] { "Customers", "Memberships", "EntryParticipations", "EntryParticipationApprovalDecisions", "EntryRecruiterCorrections", "MemberPayments", "AQGreenJoiningCheckouts", "AQGreenRecruitmentAttributions", "AQGreenRecruitmentAttributionConfirmations", "AQGreenNetworkPlacements", "Areas", "AreaAdminAssignments", "AbpTenants" };
        public int RecordCount { get; set; }
        public SortedDictionary<string, int> ClassificationCounts { get; set; }
        public SortedDictionary<string, int> SourceRecordCounts { get; set; }
        public string CandidatePopulationDigest { get; set; }
        public string RecordsDigest { get; set; }
        public string OverallSnapshotDigest { get; set; }
        public string ManifestHash { get; set; }
    }

    public sealed class AQGreenReconciliationEvidenceRecord
    {
        public string DiscoveryId { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public Guid? CurrentAreaId { get; set; }
        public int? LegacyMembershipId { get; set; }
        public Guid? ParticipationId { get; set; }
        public string DiscoveryKind { get; set; }
        public string ParticipationVersionEvidence { get; set; }
        public string[] HistoricalEvidenceIssues { get; set; }
        public string CurrentParticipationStatus { get; set; }
        public DateTime? RecordedStartedAt { get; set; }
        public DateTime? RecordedActivatedAt { get; set; }
        public int? CurrentRecruiterCustomerId { get; set; }
        public string TermsVersion { get; set; }
        public decimal? JoiningAmount { get; set; }
        public decimal? RegistrationAmount { get; set; }
        public decimal? ActivationAmount { get; set; }
        public string Currency { get; set; }
        public Guid? JoiningPaymentId { get; set; }
        public Guid? RegistrationPaymentId { get; set; }
        public Guid? ActivationPaymentId { get; set; }
        public List<AQGreenJoiningPaymentEvidence> JoiningPayments { get; set; }
        public List<AQGreenCheckoutEvidence> CheckoutAttempts { get; set; }
        public List<AQGreenApprovalEvidence> ApprovalDecisions { get; set; }
        public List<AQGreenRecruiterCorrectionEvidence> RecruiterCorrections { get; set; }
        public AQGreenAttributionEvidence Attribution { get; set; }
        public AQGreenConfirmationEvidence AttributionConfirmation { get; set; }
        public AQGreenPlacementEvidence Placement { get; set; }
        public string RecordDigest { get; set; }
    }
    public sealed class AQGreenJoiningPaymentEvidence
    {
        public Guid PaymentId { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }
    public sealed class AQGreenCheckoutEvidence
    {
        public Guid CheckoutId { get; set; }
        public string Status { get; set; }
        public Guid? PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }
    public sealed class AQGreenApprovalEvidence
    {
        public Guid DecisionId { get; set; }
        public DateTime DecidedAt { get; set; }
        public bool Approved { get; set; }
    }
    public sealed class AQGreenRecruiterCorrectionEvidence
    {
        public Guid CorrectionId { get; set; }
        public int? PreviousRecruiterCustomerId { get; set; }
        public int? NewRecruiterCustomerId { get; set; }
        public DateTime CorrectedAt { get; set; }
    }
    public sealed class AQGreenAttributionEvidence
    {
        public Guid AttributionId { get; set; }
        public Guid? CreditedSponsorParticipantId { get; set; }
        public string Kind { get; set; }
        public string AcquisitionSource { get; set; }
        public Guid SourceReferenceId { get; set; }
        public DateTime AttributedAt { get; set; }
        public string RulesVersion { get; set; }
    }
    public sealed class AQGreenConfirmationEvidence
    {
        public Guid ConfirmationId { get; set; }
        public Guid EvidenceReferenceId { get; set; }
        public string Method { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string RulesVersion { get; set; }
    }
    public sealed class AQGreenPlacementEvidence
    {
        public Guid PlacementId { get; set; }
        public Guid PlacementTreeScopeId { get; set; }
        public Guid? ParentParticipantId { get; set; }
        public int? Slot { get; set; }
        public DateTime PlacedAt { get; set; }
        public string RulesVersion { get; set; }
    }
}
