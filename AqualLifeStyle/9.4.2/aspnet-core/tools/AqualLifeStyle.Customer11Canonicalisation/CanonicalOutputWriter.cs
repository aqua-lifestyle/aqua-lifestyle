using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Customer11Canonicalisation;

internal static class CanonicalOutputWriter
{
    internal static byte[] Write(VerifiedInput input, AQGreenLegacyCanonicalisationResult result)
    {
        var m = input.Manifest;
        var participants = Customer11ManifestReader.Array(m, "participants");
        var byId = participants.ToDictionary(p => Customer11ManifestReader.Id(p, "participationId"));
        var byCandidate = input.Request.Candidates.ToDictionary(c => c.ParticipantSnapshotKey);
        var output = new
        {
            label = "NEW-LINEAGE CUSTOMER-11 CANDIDATE CANONICAL OUTPUT",
            outputVersion = "Customer11OfflineCanonicalisationOutputV1",
            acceptance = "NEW ARTIFACT — FRESH ACCEPTANCE REQUIRED",
            tenantId = input.Request.TenantSnapshotKey,
            programme = "AQGreen",
            rootCustomerId = Customer11ManifestReader.Int(m, "rootCustomerId"),
            rootParticipationId = Customer11ManifestReader.Id(m, "rootParticipationId"),
            placementTreeScopeId = input.Request.PlacementTreeScopeSnapshotKey,
            canonicalisationEffectiveAtUtc = Customer11ManifestReader.Text(m, "canonicalisationEffectiveAtUtc"),
            placementSource = AQGreenLegacyCanonicalisationRules.PlacementSource,
            rulesVersion = AQGreenPlacementRules.CurrentVersion,
            simulationVersion = AQGreenLegacyCanonicalisationRules.SimulationVersion,
            comparatorVersion = AQGreenLegacyCanonicalisationRules.ComparatorVersion,
            inputManifestSha256 = input.InputSha256,
            sourceCaptureSha256 = input.CaptureSha256,
            sourceSnapshot = m.GetProperty("sourceSnapshot"),
            custody = m.GetProperty("custody"),
            effectiveTimeMeaning = m.GetProperty("effectiveTimeMeaning"),
            domainManifestDigest = result.ManifestDigest,
            anomalies = result.Anomalies.Select(a => a.Code.ToString()).ToArray(),
            // Existing domain order is preserved. No adapter sort/traversal selects topology.
            population = result.Placements.Select(p => new
            {
                customerId = Customer11ManifestReader.Int(byId[p.ParticipantSnapshotKey], "customerId"),
                participationId = p.ParticipantSnapshotKey
            }).ToArray(),
            sponsorEdges = result.Placements.Where(p => p.LegacyCreditedSponsorSnapshotKey.HasValue).Select(p => new
            {
                participationId = p.ParticipantSnapshotKey,
                acceptedLegacySponsorParticipationId = p.LegacyCreditedSponsorSnapshotKey
            }).ToArray(),
            placements = result.Placements.Select(p => new
            {
                customerId = Customer11ManifestReader.Int(byId[p.ParticipantSnapshotKey], "customerId"),
                participationId = p.ParticipantSnapshotKey,
                tenantId = p.TenantSnapshotKey,
                placementTreeScopeId = p.PlacementTreeScopeSnapshotKey,
                acceptedLegacySponsorParticipationId = p.LegacyCreditedSponsorSnapshotKey,
                placementParentParticipationId = p.PlacementParentSnapshotKey,
                placementSlot = p.PlacementSlot,
                canonicalPath = p.CanonicalPath,
                canonicalPosition = p.CanonicalPosition.ToString(CultureInfo.InvariantCulture),
                canonicalInputOrdinal = p.CanonicalInputOrdinal,
                legacyOrderingTieBreakOrdinal = p.LegacyOrderingTieBreakOrdinal,
                placementSequence = p.PlacementSequence,
                placementSource = p.PlacementSource,
                rulesVersion = p.RulesVersion,
                simulationVersion = p.SimulationVersion,
                comparatorVersion = p.ComparatorVersion,
                sourceEvidenceDigest = p.SourceEvidenceDigest
            }).ToArray(),
            structuralImpacts = result.StructuralImpacts.Select(p => new
            {
                participationId = p.ParticipantSnapshotKey,
                effectiveV1StructuralLevel = p.EffectiveV1StructuralLevel.HasValue ? (int?)p.EffectiveV1StructuralLevel.Value : null,
                v1EvidenceDisposition = p.EffectiveV1StructuralLevel.HasValue ? "KNOWN" : "UNKNOWN",
                simulatedV2StructuralLevel = (int)p.SimulatedV2StructuralLevel,
                classification = p.Classification.ToString(),
                interpretation = "Simulation only; not historical financial eligibility"
            }).ToArray(),
            sourceFacts = result.Placements.Select(p => EvidenceMetadata(byId[p.ParticipantSnapshotKey])).ToArray()
        };
        foreach (var impact in result.StructuralImpacts)
            Customer11ManifestReader.Check(impact.EffectiveV1StructuralLevel ==
                byCandidate[impact.ParticipantSnapshotKey].EffectiveV1StructuralLevel, "StructuralPreservation");
        return CanonicalBytes(JsonSerializer.SerializeToElement(output), null, true);
    }

    private static JsonObject EvidenceMetadata(JsonElement p)
    {
        // Preserve every explicit UNKNOWN and evidence disposition without copying payment references,
        // provider payloads, approving actor identifiers or other source-capture sensitive values.
        var retained = new HashSet<string>(StringComparer.Ordinal)
        {
            "customerId", "participationId", "tenantId", "programme", "currentStatus", "rawStatus", "isDeleted",
            "acceptedLegacySponsorParticipationId", "legacyRecruiterCustomerId", "startedAtUtc", "activatedAtUtc",
            "qualifiedUnderRecruiterAtUtc", "legacyOrderingTieBreakOrdinal", "orderingDerivation",
            "effectiveV1StructuralLevel", "acquisitionProvenance", "historicalAdministratorApproval",
            "historicalV2Placement", "sourceEvidenceDigest", "sourceEvidenceCaptureSha256", "evidenceReceiptSha256",
            "canonicalisationEligibility", "correctionCount", "paymentOwnershipConsistency",
            "rootEvidenceStatus", "sponsorEvidenceStatus"
        };
        var result = new JsonObject();
        foreach (var property in p.EnumerateObject())
            if (retained.Contains(property.Name) || property.Name.EndsWith("Disposition", StringComparison.Ordinal) ||
                property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "UNKNOWN")
                result.Add(property.Name, JsonNode.Parse(property.Value.GetRawText()));
        return result;
    }

    internal static byte[] CanonicalBytes(JsonElement value, string? excludedProperty, bool indented)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = indented, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        })) WriteValue(writer, value, excludedProperty);
        stream.WriteByte(10);
        return stream.ToArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value, string? excludedProperty = null)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in value.EnumerateObject().Where(p => p.Name != excludedProperty).OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(p.Name);
                    WriteValue(writer, p.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteValue(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            // Preserve the input number lexeme for the participant digest contract (e.g. 1200.00).
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new AdapterValidationException("JsonValueKind");
        }
    }
}
