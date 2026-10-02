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
    /// An immutable topology-only plan from the two exact owner-accepted artifacts.
    /// These digests are authority pins, not configurable operator hash assertions.
    /// Production identities and source facts are loaded from protected input files,
    /// never copied into source-controlled fixtures. No attribution or approval is created.
    /// </summary>
    public sealed class AQGreenCustomer11BackfillPlan
    {
        public const string InputSha256 = "57529d67d4320595c90e2dedaf492d97c42fede1febd22caea014d0fc16f257c";
        public const string OutputSha256 = "46a7ae8a01b28b4c8896f1f778f704e6673256164b3825c1a052f1d113430978";
        public const string PlacementSource = "LegacyCanonicalisation";
        public int TenantId { get; }
        public string Programme { get; }
        public Guid ScopeId { get; }
        public Guid RootParticipantId { get; }
        public DateTime EffectiveAtUtc { get; }
        public string SourceSnapshotSha256 { get; }
        public string SourceServiceId { get; }
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
            Ensure(capturedBytes != null && Digest(capturedBytes) == SourceSnapshotSha256,
                "Protected source capture hash differs from accepted snapshot binding.");
            var captured = ReadCapturedEvidence(capturedBytes);
            Ensure(TenantId == 1 && Programme == "AQGreen" && ScopeId != Guid.Empty &&
                   input.GetProperty("placementSource").GetString() == PlacementSource &&
                   output.GetProperty("inputManifestSha256").GetString() == InputSha256 &&
                   output.GetProperty("candidatePlacementTreeScopeId").GetGuid() == ScopeId &&
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
                       row.GetProperty("scopeId").GetGuid() == ScopeId &&
                       row.GetProperty("rulesVersion").GetString() == AQGreenPlacementRules.CurrentVersion &&
                       row.GetProperty("placementSource").GetString() == PlacementSource &&
                       Timestamp(row, "effectiveAtUtc") == EffectiveAtUtc &&
                       NullableGuid(row, "acceptedLegacySponsorParticipationId") == sponsor,
                    "Accepted output topology/source binding disagrees.");
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
                    approval.ValueKind == JsonValueKind.Undefined ? null : Timestamp(approval,"ActorCreationTime")));
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
            Ensure(inputBytes != null && inputBytes.Length == 16351 && Digest(inputBytes) == InputSha256,
                "Input manifest hash/size is not owner-accepted.");
            Ensure(outputBytes != null && Digest(outputBytes) == OutputSha256,
                "Canonical output hash is not owner-accepted.");
            using var input = JsonDocument.Parse(inputBytes);
            using var output = JsonDocument.Parse(outputBytes);
            return new AQGreenCustomer11BackfillPlan(input.RootElement, output.RootElement, capturedBytes);
        }

        private static JsonElement ReadCapturedEvidence(byte[] bytes)
        {
            // The protected capture contains two pretty-printed JSON documents, not line-sized values.
            int offset = 0, count = 0;
            JsonElement result = default;
            while (offset < bytes.Length)
            {
                while (offset < bytes.Length && (bytes[offset] == 32 || bytes[offset] == 10 || bytes[offset] == 13 || bytes[offset] == 9)) offset++;
                if (offset == bytes.Length) break;
                var reader = new Utf8JsonReader(bytes.AsSpan(offset));
                using var document = JsonDocument.ParseValue(ref reader);
                offset += checked((int)reader.BytesConsumed);
                if (document.RootElement.TryGetProperty("participants", out _)) result = document.RootElement.Clone();
                count++;
            }
            Ensure(count == 2 && result.ValueKind == JsonValueKind.Object, "Accepted bounded capture structure differs.");
            return result;
        }

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
        Guid? CapturedApprovalDecisionId, long? CapturedApprovingActorId, DateTime? CapturedActorCreationTimeUtc);
}
