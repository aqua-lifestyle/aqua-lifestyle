using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// An immutable topology-only plan from the exact owner-accepted new-lineage artifacts.
    /// These digests are authority pins, not configurable operator hash assertions.
    /// Production identities and source facts are loaded from protected input files,
    /// never copied into source-controlled fixtures. No attribution or approval is created.
    /// </summary>
    public sealed class AQGreenCustomer11BackfillPlan
    {
        public const string InputSha256 = "74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416";
        public const string OutputSha256 = "a1bcf510b41fa97e449e0dcb938d2b492ad08f215f6d4e5d75ff117f8b6af11d";
        public const string CaptureSha256 = "b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494";
        public const string RecoveryServiceId = "dpg-davoaabncjis73f56rj0-a";
        public const string RecoveryDatabase = "aqualifestyle_1t0a";
        public const string RecoveryUser = "aqualifestyle";
        public const int ServerVersionNumber = 180006;
        public const string PlacementSource = "LegacyCanonicalisation";
        public int TenantId { get; }
        public string Programme { get; }
        public Guid ScopeId { get; }
        public Guid RootParticipantId { get; }
        public DateTime EffectiveAtUtc { get; }
        public string SourceSnapshotSha256 { get; }
        public string SourceServiceId { get; }
        public JsonElement SourceProvenance { get; }
        public JsonElement CustodyDisclosure { get; }
        public IReadOnlyList<AQGreenAcceptedBackfillMember> Members { get; }

        private AQGreenCustomer11BackfillPlan(JsonElement input, JsonElement output, byte[] capturedBytes)
        {
            TenantId = input.GetProperty("tenantId").GetInt32();
            Programme = input.GetProperty("programme").GetString();
            ScopeId = input.GetProperty("candidatePlacementTreeScopeId").GetGuid();
            RootParticipantId = input.GetProperty("rootParticipationId").GetGuid();
            EffectiveAtUtc = Timestamp(input, "canonicalisationEffectiveAtUtc");
            var snapshot = input.GetProperty("sourceSnapshot");
            SourceSnapshotSha256 = snapshot.GetProperty("boundedCaptureSha256").GetString();
            SourceServiceId = snapshot.GetProperty("serviceId").GetString();
            SourceProvenance = snapshot.Clone();
            CustodyDisclosure = input.GetProperty("custody").Clone();
            Ensure(SourceSnapshotSha256 == CaptureSha256 && SourceServiceId == RecoveryServiceId &&
                   snapshot.GetProperty("database").GetString() == RecoveryDatabase &&
                   snapshot.GetProperty("databaseUser").GetString() == RecoveryUser &&
                   snapshot.GetProperty("captureLabel").GetString() == "NEW RECOVERY-CLONE EVIDENCE CAPTURE" &&
                   CustodyDisclosure.GetProperty("uninterruptedCustody").GetString() == "NOT CLAIMED",
                "Accepted recovery-clone provenance differs.");
            Ensure(capturedBytes != null && Digest(capturedBytes) == CaptureSha256,
                "Protected source capture hash differs from accepted snapshot binding.");
            var captured = ReadCapturedEvidence(capturedBytes);
            Ensure(TenantId == 1 && Programme == "AQGreen" && ScopeId == Guid.Parse("06a452ea-9958-5911-8c5b-5d350e516095") &&
                   EffectiveAtUtc == DateTime.Parse("2026-10-02T05:37:10Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal) &&
                   input.GetProperty("placementSource").GetString() == PlacementSource &&
                   output.GetProperty("inputManifestSha256").GetString() == InputSha256 &&
                   output.GetProperty("sourceCaptureSha256").GetString() == CaptureSha256 &&
                   output.GetProperty("placementTreeScopeId").GetGuid() == ScopeId &&
                   output.GetProperty("rootParticipationId").GetGuid() == RootParticipantId &&
                   Timestamp(output, "canonicalisationEffectiveAtUtc") == EffectiveAtUtc,
                "Accepted manifest/output ownership, scope or timestamp disagree.");
            var sources = input.GetProperty("participants").EnumerateArray().ToDictionary(p => p.GetProperty("participationId").GetGuid());
            var members = new List<AQGreenAcceptedBackfillMember>();
            foreach (var row in output.GetProperty("placements").EnumerateArray())
            {
                var id = row.GetProperty("participationId").GetGuid();
                Ensure(sources.TryGetValue(id, out var source), "Output substitutes a participant.");
                var parent = NullableGuid(row, "placementParentParticipationId");
                var sponsor = NullableGuid(source, "acceptedLegacySponsorParticipationId");
                var capturedParticipant = captured.GetProperty("participants").EnumerateArray().Single(p => p.GetProperty("Id").GetGuid() == id);
                var paymentId = capturedParticipant.GetProperty("JoiningPaymentId").GetGuid();
                var capturedCheckout = captured.GetProperty("joining_checkouts").EnumerateArray().Single(c => c.GetProperty("ParticipationId").GetGuid() == id);
                var capturedReceipt = captured.GetProperty("webhook_receipts").EnumerateArray().Single(r => r.GetProperty("CheckoutReferenceId").GetGuid() == capturedCheckout.GetProperty("Id").GetGuid());
                var security = captured.GetProperty("customer_security").EnumerateArray().Single(c => c.GetProperty("ParticipationId").GetGuid() == id);
                var approval = captured.GetProperty("approval_decisions").EnumerateArray().SingleOrDefault(d => d.GetProperty("EntryParticipationId").GetGuid() == id);
                Ensure(source.GetProperty("tenantId").GetInt32() == TenantId &&
                       source.GetProperty("programme").GetString() == Programme &&
                       row.GetProperty("tenantId").GetInt32() == TenantId &&
                       row.GetProperty("placementTreeScopeId").GetGuid() == ScopeId &&
                       row.GetProperty("rulesVersion").GetString() == AQGreenPlacementRules.CurrentVersion &&
                       row.GetProperty("placementSource").GetString() == PlacementSource &&
                       NullableGuid(row, "acceptedLegacySponsorParticipationId") == sponsor,
                    "Accepted output topology/source binding disagrees.");
                var impact = output.GetProperty("structuralImpacts").EnumerateArray().Single(p => p.GetProperty("participationId").GetGuid() == id);
                Ensure(source.GetProperty("effectiveV1StructuralLevel").GetString() == "UNKNOWN" &&
                       impact.GetProperty("effectiveV1StructuralLevel").ValueKind == JsonValueKind.Null &&
                       impact.GetProperty("classification").GetString() == "Ambiguous" &&
                       source.GetProperty("historicalAdministratorApproval").GetString() == (id == RootParticipantId ? "UNKNOWN" : "RETAINED_POSITIVE_DECISION") &&
                       source.GetProperty("acquisitionProvenance").GetString() == "LegacyUnknownAcquisition" &&
                       source.GetProperty("historicalInvitation").GetString() == "UNKNOWN" &&
                       source.GetProperty("historicalParticipantConfirmation").GetString() == "UNKNOWN",
                    "Accepted UNKNOWN evidence dispositions differ.");
                members.Add(new AQGreenAcceptedBackfillMember(
                    id, source.GetProperty("customerId").GetInt32(),
                    NullableInt(source, "legacyRecruiterCustomerId"),
                    Timestamp(source, "startedAtUtc"), Timestamp(source, "activatedAtUtc"),
                    Timestamp(source, "paymentConfirmedAtUtc"), source.GetProperty("joiningAmount").GetDecimal(),
                    source.GetProperty("currency").GetString(), parent, NullableInt(row, "placementSlot"),
                    row.GetProperty("canonicalPath").GetString(), row.GetProperty("placementSequence").GetInt64(),
                    id != RootParticipantId, paymentId, capturedCheckout.GetProperty("Id").GetGuid(),
                    capturedReceipt.GetProperty("Id").GetGuid(), Timestamp(capturedReceipt, "ProcessedAt"),
                    security.GetProperty("UserId").GetInt64(),
                    approval.ValueKind == JsonValueKind.Undefined ? null : approval.GetProperty("Id").GetGuid(),
                    approval.ValueKind == JsonValueKind.Undefined ? null : approval.GetProperty("AdministratorUserId").GetInt64(),
                    approval.ValueKind == JsonValueKind.Undefined ? null : Timestamp(approval,"ActorCreationTime"),
                    null, source.GetProperty("historicalAdministratorApproval").GetString(),
                    source.GetProperty("acquisitionProvenance").GetString(),
                    source.GetProperty("historicalInvitation").GetString(), source.GetProperty("historicalParticipantConfirmation").GetString()));
            }
            Ensure(members.Count == 6 && sources.Count == 6 && members.Select(m => m.ParticipantId).Distinct().Count() == 6 &&
                   members.Select(m => m.CustomerId).Distinct().Count() == 6 &&
                   members.Single(m => m.ParticipantId == RootParticipantId).ParentId == null &&
                   members.Where(m => m.ParticipantId != RootParticipantId).All(m => m.ParentId == RootParticipantId),
                "Accepted exact six-person/root topology disagrees.");
            Members = members.OrderBy(m => m.Sequence).ToList().AsReadOnly();
        }

        public static AQGreenCustomer11BackfillPlan Load(byte[] inputBytes, byte[] outputBytes, byte[] capturedBytes)
        {
            Ensure(inputBytes != null && inputBytes.Length == 20131 && Digest(inputBytes) == InputSha256,
                "Input manifest hash/size is not owner-accepted.");
            Ensure(outputBytes != null && Digest(outputBytes) == OutputSha256,
                "Canonical output hash is not owner-accepted.");
            using var input = JsonDocument.Parse(inputBytes);
            using var output = JsonDocument.Parse(outputBytes);
            return new AQGreenCustomer11BackfillPlan(input.RootElement, output.RootElement, capturedBytes);
        }

        // Independently testable transport validation; cannot construct a plan or bypass byte pins.
        public static void VerifyCaptureStructure(byte[] bytes) => ReadCapturedEvidence(bytes);

        private static JsonElement ReadCapturedEvidence(byte[] bytes)
        {
            var lines = new UTF8Encoding(false, true).GetString(bytes).Split('\n').ToList();
            if (lines.Last() == "") lines.RemoveAt(lines.Count - 1);
            Ensure(lines.Count == 7 && lines.All(s => !string.IsNullOrWhiteSpace(s)), "CaptureRecordCount");
            var records = lines.Select(s => { using var d = JsonDocument.Parse(s); return d.RootElement.Clone(); }).ToArray();
            string[] categories = { "DatabaseIdentity", "DatabaseUser", "ServerVersionNumber", "ReadOnlyMode" };
            string[] values = { RecoveryDatabase, RecoveryUser, null, "on" };
            for (int i = 0; i < 4; i++)
            {
                var g = records[i];
                Ensure(g.GetProperty("kind").GetString() == "identity_guard" &&
                       g.GetProperty("category").GetString() == categories[i] &&
                       g.GetProperty("result").GetString() == "PASS", "CaptureGuard:" + categories[i]);
                Ensure(i == 2 ? g.GetProperty("expected").GetInt32() == ServerVersionNumber && g.GetProperty("observed").GetInt32() == ServerVersionNumber :
                       g.GetProperty("expected").GetString() == values[i] && g.GetProperty("observed").GetString() == values[i], "CaptureGuardValue:" + categories[i]);
            }
            var identity = records[4];
            Ensure(identity.GetProperty("kind").GetString() == "identity" &&
                   identity.GetProperty("database").GetString() == RecoveryDatabase && identity.GetProperty("user").GetString() == RecoveryUser &&
                   identity.GetProperty("clone_service_id").GetString() == RecoveryServiceId &&
                   identity.GetProperty("capture_label").GetString() == "NEW RECOVERY-CLONE EVIDENCE CAPTURE" &&
                   identity.GetProperty("transaction_read_only").GetString() == "on" && identity.GetProperty("isolation").GetString() == "repeatable read" &&
                   Timestamp(identity, "snapshot_utc") == DateTime.Parse("2026-10-03T07:17:10.764589Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal) &&
                   Timestamp(identity, "historical_pitr_cutoff") == Timestamp(identity, "historical_canonicalisation_effective_at") &&
                   Timestamp(identity, "historical_pitr_cutoff") == DateTime.Parse("2026-10-02T05:37:10Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal) &&
                   !string.IsNullOrWhiteSpace(identity.GetProperty("version").GetString()) &&
                   !string.IsNullOrWhiteSpace(records[2].GetProperty("server_version").GetString()), "CaptureIdentity");
            var evidence = records[5]; var supplement = records[6];
            Ensure(evidence.GetProperty("kind").GetString() == "bounded_evidence" &&
                   supplement.GetProperty("kind").GetString() == "bounded_supplement", "CaptureRecordOrder");
            foreach (var name in new[] { "participants", "expected", "checks", "customer_security", "joining_checkouts", "payments", "webhook_receipts" })
                Ensure(evidence.GetProperty(name).GetArrayLength() == 6, "CapturePopulation:" + name);
            Ensure(evidence.GetProperty("approval_decisions").GetArrayLength() == 5 && Empty(evidence.GetProperty("recruiter_corrections")), "CaptureApprovalCorrections");
            foreach (var prefix in new[] { "scope", "placement", "attribution", "confirmation" })
                Ensure(supplement.GetProperty(prefix + "_count").GetInt32() == 0, "CaptureExistingRows:" + prefix);
            foreach (var name in new[] { "scopes", "placements", "attributions" })
                Ensure(Empty(supplement.GetProperty(name)), "CaptureExistingRows:" + name);
            var tenant = supplement.GetProperty("tenant");
            Ensure(tenant.GetProperty("Id").GetInt32() == 1 && tenant.GetProperty("IsActive").GetBoolean() && !tenant.GetProperty("IsDeleted").GetBoolean() &&
                   supplement.GetProperty("additional_active_descendant_count").GetInt32() == 0 &&
                   supplement.GetProperty("approval_actor_checks").GetArrayLength() == 5 && supplement.GetProperty("checkout_checks").GetArrayLength() == 6, "CaptureSupplement");
            return evidence;
        }

        private static bool Empty(JsonElement e) => e.ValueKind == JsonValueKind.Null || e.ValueKind == JsonValueKind.Array && e.GetArrayLength() == 0;

        // Deterministic storage key; topology authority still comes solely from accepted bytes.
        public Guid PlacementId(Guid participantId)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(
                $"AQGreenCustomer11Backfill:{InputSha256}:{ScopeId:D}:{participantId:D}"));
            return new Guid(digest.AsSpan(0, 16), bigEndian: true);
        }

        private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        private static DateTime Timestamp(JsonElement row, string key) =>
            DateTime.Parse(row.GetProperty(key).GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        private static Guid? NullableGuid(JsonElement row, string key) => row.GetProperty(key).ValueKind == JsonValueKind.Null ? null : row.GetProperty(key).GetGuid();
        private static int? NullableInt(JsonElement row, string key) => row.GetProperty(key).ValueKind == JsonValueKind.Null ? null : row.GetProperty(key).GetInt32();
        private static void Ensure(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    public sealed record AQGreenAcceptedBackfillMember(
        Guid ParticipantId, int CustomerId, int? RecruiterCustomerId,
        DateTime StartedAtUtc, DateTime ActivatedAtUtc, DateTime PaymentConfirmedAtUtc,
        decimal JoiningAmount, string Currency, Guid? ParentId, int? Slot,
        string CanonicalPath, long Sequence, bool RequiresRetainedPositiveApproval,
        Guid CapturedJoiningPaymentId, Guid CapturedCheckoutId, Guid CapturedReceiptId,
        DateTime CapturedReceiptProcessedAtUtc, long CapturedUserId,
        Guid? CapturedApprovalDecisionId, long? CapturedApprovingActorId, DateTime? CapturedActorCreationTimeUtc,
        int? EffectiveV1StructuralLevel, string HistoricalAdministratorApproval,
        string AcquisitionProvenance, string HistoricalInvitation, string HistoricalParticipantConfirmation);
}
