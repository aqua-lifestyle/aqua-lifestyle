using System.Text.Json;
using System.Text.Json.Nodes;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed record B6SimulationOutputVerification(
    string Result,
    string ProjectionSchemaVersion,
    string SemanticProjectionSha256,
    string SimulatorOutputDigest,
    string CurrentFullOutputSha256);

public static class B6SimulationOutputVerifier
{
    private static readonly string[] TopLevel =
    [
        "analysisSnapshotCutoff", "comparatorVersion", "components", "placementSource", "placements",
        "simulationVersion", "simulatorOutputDigest", "snapshotId", "structuralImpacts", "toolchainIdentity"
    ];

    private static readonly string[] Component =
    [
        "impacts", "manifestDigest", "placements", "placementTreeScopeSnapshotKey", "tenantSnapshotKey"
    ];

    private static readonly string[] Placement =
    [
        "canonicalInputOrdinal", "canonicalPath", "canonicalPosition", "comparatorVersion",
        "legacyCreditedSponsorSnapshotKey", "legacyOrderingTieBreakOrdinal", "participantSnapshotKey",
        "placementParentSnapshotKey", "placementSequence", "placementSlot", "placementSource",
        "placementTreeScopeSnapshotKey", "rulesVersion", "simulationVersion", "sourceEvidenceDigest",
        "tenantSnapshotKey"
    ];

    private static readonly string[] Impact =
    [
        "classification", "effectiveV1StructuralLevel", "legacyOrderingTieBreakOrdinal",
        "participantSnapshotKey", "simulatedV2StructuralLevel"
    ];

    private static readonly string[] Toolchain =
    [
        "evidenceClassification", "firstPartyDependencies", "repositoryCommit", "runner"
    ];

    private static readonly string[] Assembly =
    [
        "assemblyName", "assemblySha256", "informationalVersion"
    ];

    public static B6SimulationOutputVerification Verify(string inputPath, ToolchainIdentity expectedProvenance)
    {
        byte[] bytes;
        using (var input = SecureFiles.OpenRead(inputPath, controlled: true))
        using (var buffer = new MemoryStream())
        {
            input.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        JsonObject root;
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            RejectDuplicateProperties(document.RootElement, "simulation output");
            root = JsonNode.Parse(bytes)?.AsObject()
                ?? throw new B6PackageException("Simulation output must be a JSON object.");
        }
        catch (B6PackageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new B6PackageException("Simulation output is not valid strict JSON.", exception);
        }

        ValidateSchemaAndFixture(root);
        var projection = root.DeepClone().AsObject();
        projection.Remove("toolchainIdentity");
        projection.Remove("simulatorOutputDigest");
        var projectionEnvelope = new JsonObject
        {
            ["projectionSchemaVersion"] = B6Contract.GateASemanticProjectionVersion,
            ["simulation"] = projection
        };
        var semanticDigest = CanonicalJson.Digest(projectionEnvelope);
        if (semanticDigest != B6Contract.ExpectedGateASemanticProjectionSha256)
            throw new B6PackageException("Gate-A semantic projection does not match the frozen expectation.");

        ValidateProvenance(root["toolchainIdentity"]!.AsObject(), expectedProvenance);
        var digestPayload = new JsonObject
        {
            ["components"] = root["components"]!.DeepClone(),
            ["toolchainIdentity"] = CanonicalJson.ToNode(expectedProvenance),
            ["simulationVersion"] = root["simulationVersion"]!.DeepClone(),
            ["comparatorVersion"] = root["comparatorVersion"]!.DeepClone()
        };
        var recomputedDigest = CanonicalJson.Digest(digestPayload);
        if (String(root, "simulatorOutputDigest") != recomputedDigest)
            throw new B6PackageException("Simulation output digest does not match independently validated provenance and components.");

        return new B6SimulationOutputVerification(
            "PASS",
            B6Contract.GateASemanticProjectionVersion,
            semanticDigest,
            recomputedDigest,
            B6Values.Sha256Hex(bytes));
    }

    private static void ValidateSchemaAndFixture(JsonObject root)
    {
        ExactKeys(root, TopLevel, "top level");
        if (!B6Contract.SnapshotId.IsMatch(String(root, "snapshotId")))
            throw new B6PackageException("Gate-A snapshotId is invalid.");
        B6Values.ParseTimestamp(String(root, "analysisSnapshotCutoff"));
        RequireEqual(String(root, "simulationVersion"), B6Contract.SimulatorVersion, "simulationVersion");
        RequireEqual(String(root, "comparatorVersion"), B6Contract.SimulatorComparatorVersion, "comparatorVersion");
        RequireEqual(String(root, "placementSource"), "LegacyCanonicalisation", "placementSource");
        Digest(root, "simulatorOutputDigest");

        var components = Array(root, "components", 1);
        var topPlacements = Array(root, "placements", 7);
        var topImpacts = Array(root, "structuralImpacts", 7);
        var concatenatedPlacements = new JsonArray();
        var concatenatedImpacts = new JsonArray();
        foreach (var item in components)
        {
            var component = Object(item, "component");
            ExactKeys(component, Component, "component");
            Integer(component, "tenantSnapshotKey");
            GuidString(component, "placementTreeScopeSnapshotKey");
            Digest(component, "manifestDigest");
            foreach (var placement in Array(component, "placements", 7))
            {
                ValidatePlacement(Object(placement, "component placement"));
                concatenatedPlacements.Add(placement!.DeepClone());
            }
            foreach (var impact in Array(component, "impacts", 7))
            {
                ValidateImpact(Object(impact, "component impact"));
                concatenatedImpacts.Add(impact!.DeepClone());
            }
        }
        foreach (var placement in topPlacements) ValidatePlacement(Object(placement, "top-level placement"));
        foreach (var impact in topImpacts) ValidateImpact(Object(impact, "top-level impact"));
        if (!JsonNode.DeepEquals(topPlacements, concatenatedPlacements))
            throw new B6PackageException("Top-level placements do not equal concatenated component placements in contractual order.");
        if (!JsonNode.DeepEquals(topImpacts, concatenatedImpacts))
            throw new B6PackageException("Top-level structural impacts do not equal concatenated component impacts in contractual order.");

        var toolchain = Object(root["toolchainIdentity"], "toolchainIdentity");
        ExactKeys(toolchain, Toolchain, "toolchainIdentity");
        String(toolchain, "repositoryCommit");
        String(toolchain, "evidenceClassification");
        ValidateAssembly(Object(toolchain["runner"], "runner"), "runner");
        var dependencies = Array(toolchain, "firstPartyDependencies", 2);
        foreach (var dependency in dependencies) ValidateAssembly(Object(dependency, "dependency"), "dependency");
    }

    private static void ValidatePlacement(JsonObject value)
    {
        ExactKeys(value, Placement, "placement");
        Integer(value, "canonicalInputOrdinal");
        String(value, "canonicalPath");
        String(value, "canonicalPosition");
        String(value, "comparatorVersion");
        NullableGuidString(value, "legacyCreditedSponsorSnapshotKey");
        Integer(value, "legacyOrderingTieBreakOrdinal");
        GuidString(value, "participantSnapshotKey");
        NullableGuidString(value, "placementParentSnapshotKey");
        Integer(value, "placementSequence");
        NullableInteger(value, "placementSlot");
        String(value, "placementSource");
        GuidString(value, "placementTreeScopeSnapshotKey");
        String(value, "rulesVersion");
        String(value, "simulationVersion");
        Digest(value, "sourceEvidenceDigest");
        Integer(value, "tenantSnapshotKey");
    }

    private static void ValidateImpact(JsonObject value)
    {
        ExactKeys(value, Impact, "impact");
        String(value, "classification");
        String(value, "effectiveV1StructuralLevel");
        Integer(value, "legacyOrderingTieBreakOrdinal");
        GuidString(value, "participantSnapshotKey");
        String(value, "simulatedV2StructuralLevel");
    }

    private static void ValidateAssembly(JsonObject value, string context)
    {
        ExactKeys(value, Assembly, context);
        String(value, "assemblyName");
        Digest(value, "assemblySha256");
        String(value, "informationalVersion");
    }

    private static void ValidateProvenance(JsonObject actual, ToolchainIdentity expected)
    {
        expected.RequireComplete();
        if (expected.EvidenceClassification != B6Contract.OperationalEvidence)
            throw new B6PackageException("Gate-A expected provenance must be independently classified OPERATIONAL_EVIDENCE.");
        var assemblies = new[] { expected.Runner }.Concat(expected.FirstPartyDependencies);
        if (assemblies.Any(value => string.IsNullOrWhiteSpace(value.InformationalVersion) ||
                                    !value.InformationalVersion.EndsWith("+" + expected.RepositoryCommit,
                                        StringComparison.Ordinal)))
            throw new B6PackageException("Gate-A expected assembly informational versions do not bind the reviewed commit.");
        if (!JsonNode.DeepEquals(actual, CanonicalJson.ToNode(expected)))
            throw new B6PackageException("Simulation provenance does not match independently established source and B2 assembly evidence.");
    }

    private static void RejectDuplicateProperties(JsonElement value, string context)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new B6PackageException($"Duplicate JSON property '{property.Name}' in {context}.");
                RejectDuplicateProperties(property.Value, property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item, context);
    }

    private static void ExactKeys(JsonObject value, IReadOnlyCollection<string> expected, string context)
    {
        var actual = value.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        if (actual.Count != expected.Count || !actual.SetEquals(expected))
            throw new B6PackageException($"{context} has a missing or unknown field.");
    }

    private static JsonObject Object(JsonNode? value, string context) =>
        value as JsonObject ?? throw new B6PackageException($"{context} must be an object.");

    private static JsonArray Array(JsonObject owner, string name, int count)
    {
        if (owner[name] is not JsonArray value)
            throw new B6PackageException($"{name} must be an array.");
        if (value.Count != count)
            throw new B6PackageException($"{name} must contain exactly {count} entries.");
        return value;
    }

    private static string String(JsonObject owner, string name)
    {
        if (owner[name] is not JsonValue value || !value.TryGetValue<string>(out var result) || result == null)
            throw new B6PackageException($"{name} must be a non-null JSON string.");
        return result;
    }

    private static long Integer(JsonObject owner, string name)
    {
        if (owner[name] is not JsonValue value || !value.TryGetValue<long>(out var result))
            throw new B6PackageException($"{name} must be a JSON integer.");
        return result;
    }

    private static void NullableInteger(JsonObject owner, string name)
    {
        if (owner[name] == null) return;
        Integer(owner, name);
    }

    private static void GuidString(JsonObject owner, string name)
    {
        var text = String(owner, name);
        if (!Guid.TryParseExact(text, "D", out _)) throw new B6PackageException($"{name} must be a canonical UUID string.");
    }

    private static void NullableGuidString(JsonObject owner, string name)
    {
        if (owner[name] == null) return;
        GuidString(owner, name);
    }

    private static void Digest(JsonObject owner, string name)
    {
        if (!B6Contract.Sha256.IsMatch(String(owner, name)))
            throw new B6PackageException($"{name} must be a lowercase SHA-256 digest.");
    }

    private static void RequireEqual(string actual, string expected, string field)
    {
        if (actual != expected) throw new B6PackageException($"{field} is outside the Gate-A contract.");
    }
}
