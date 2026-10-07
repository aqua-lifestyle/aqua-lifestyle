using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed partial class B6SnapshotPackage
{
    public void RunSimulator(string inputPath, string outputPath)
    {
        var input = CanonicalJson.Read<SimulationInput>(inputPath);
        CanonicalJson.WriteNew(outputPath, Simulate(input));
    }

    private JsonObject Simulate(SimulationInput input)
    {
        if (!B6Contract.SnapshotId.IsMatch(input.SnapshotId)) throw new B6PackageException("Simulator SnapshotId is invalid.");
        var cutoff = B6Values.Timestamp(B6Values.ParseTimestamp(input.AnalysisSnapshotCutoff));
        var componentResults = new JsonArray(); var allPlacements = new JsonArray(); var allImpacts = new JsonArray();
        foreach (var component in input.Components.OrderBy(value => value.TenantSnapshotKey)
                     .ThenBy(value => value.PlacementTreeScopeSnapshotKey))
        {
            if (B6Values.Timestamp(B6Values.ParseTimestamp(component.AnalysisSnapshotCutoffUtc)) != cutoff)
                throw new B6PackageException("Simulator component cutoff mismatches snapshot cutoff.");
            var request = new AQGreenLegacyCanonicalisationRequest
            {
                TenantSnapshotKey = component.TenantSnapshotKey,
                PlacementTreeScopeSnapshotKey = component.PlacementTreeScopeSnapshotKey,
                AnalysisSnapshotCutoffUtc = B6Values.ParseTimestamp(component.AnalysisSnapshotCutoffUtc).UtcDateTime,
                Candidates = component.Candidates.Select(candidate => new AQGreenLegacyCanonicalisationCandidate
                {
                    TenantSnapshotKey = candidate.TenantSnapshotKey,
                    ParticipantSnapshotKey = candidate.ParticipantSnapshotKey,
                    AcceptedLegacySponsorSnapshotKey = candidate.AcceptedLegacySponsorSnapshotKey,
                    QualifiedUnderRecruiterAtUtc = candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.AcceptedRoot.ToString()
                        ? null : B6Values.ParseTimestamp(candidate.QualifiedUnderRecruiterAtUtc).UtcDateTime,
                    LegacyOrderingTieBreakOrdinal = candidate.LegacyOrderingTieBreakOrdinal,
                    SponsorEvidenceStatus = ParseEnum<AQGreenLegacySponsorEvidenceStatus>(candidate.SponsorEvidenceStatus),
                    RootEvidenceStatus = ParseEnum<AQGreenLegacyRootEvidenceStatus>(candidate.RootEvidenceStatus),
                    AcquisitionProvenance = ParseEnum<AQGreenLegacyAcquisitionProvenance>(candidate.AcquisitionProvenance),
                    EffectiveV1StructuralLevel = Enum.IsDefined((AQGreenStructuralCompletionLevel)candidate.EffectiveV1StructuralLevel)
                        ? (AQGreenStructuralCompletionLevel)candidate.EffectiveV1StructuralLevel
                        : throw new B6PackageException("Simulator input contains invalid V1 structural evidence."),
                    SourceEvidenceDigest = candidate.SourceEvidenceDigest
                }).ToArray()
            };
            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(request);
            if (!result.IsCanonicalisable)
                throw new B6PackageException($"Accepted component {component.PlacementTreeScopeSnapshotKey:D} was rejected: " +
                    string.Join(',', result.Anomalies.Select(value => value.Code)));
            var placements = new JsonArray();
            foreach (var value in result.Placements)
            {
                var item = new JsonObject
                {
                    ["tenantSnapshotKey"] = value.TenantSnapshotKey,
                    ["placementTreeScopeSnapshotKey"] = value.PlacementTreeScopeSnapshotKey.ToString("D"),
                    ["participantSnapshotKey"] = value.ParticipantSnapshotKey.ToString("D"),
                    ["legacyCreditedSponsorSnapshotKey"] = value.LegacyCreditedSponsorSnapshotKey?.ToString("D"),
                    ["legacyOrderingTieBreakOrdinal"] = value.LegacyOrderingTieBreakOrdinal,
                    ["canonicalInputOrdinal"] = value.CanonicalInputOrdinal,
                    ["placementParentSnapshotKey"] = value.PlacementParentSnapshotKey?.ToString("D"),
                    ["placementSlot"] = value.PlacementSlot,
                    ["canonicalPath"] = value.CanonicalPath,
                    ["canonicalPosition"] = value.CanonicalPosition.ToString(CultureInfo.InvariantCulture),
                    ["placementSequence"] = value.PlacementSequence,
                    ["placementSource"] = value.PlacementSource,
                    ["rulesVersion"] = value.RulesVersion,
                    ["simulationVersion"] = value.SimulationVersion,
                    ["comparatorVersion"] = value.ComparatorVersion,
                    ["sourceEvidenceDigest"] = value.SourceEvidenceDigest
                };
                placements.Add(item); allPlacements.Add(item.DeepClone());
            }
            var impacts = new JsonArray();
            foreach (var value in result.StructuralImpacts)
            {
                var item = new JsonObject
                {
                    ["participantSnapshotKey"] = value.ParticipantSnapshotKey.ToString("D"),
                    ["legacyOrderingTieBreakOrdinal"] = value.LegacyOrderingTieBreakOrdinal,
                    ["effectiveV1StructuralLevel"] = value.EffectiveV1StructuralLevel?.ToString(),
                    ["simulatedV2StructuralLevel"] = value.SimulatedV2StructuralLevel.ToString(),
                    ["classification"] = value.Classification.ToString()
                };
                impacts.Add(item); allImpacts.Add(item.DeepClone());
            }
            componentResults.Add(new JsonObject
            {
                ["tenantSnapshotKey"] = component.TenantSnapshotKey,
                ["placementTreeScopeSnapshotKey"] = component.PlacementTreeScopeSnapshotKey.ToString("D"),
                ["manifestDigest"] = result.ManifestDigest, ["placements"] = placements, ["impacts"] = impacts
            });
        }
        var digestPayload = new JsonObject
        {
            ["components"] = componentResults.DeepClone(), ["toolchainIdentity"] = CanonicalJson.ToNode(_toolchainIdentity),
            ["simulationVersion"] = B6Contract.SimulatorVersion, ["comparatorVersion"] = B6Contract.SimulatorComparatorVersion
        };
        return new JsonObject
        {
            ["snapshotId"] = input.SnapshotId, ["analysisSnapshotCutoff"] = cutoff,
            ["simulationVersion"] = B6Contract.SimulatorVersion,
            ["comparatorVersion"] = B6Contract.SimulatorComparatorVersion,
            ["placementSource"] = AQGreenLegacyCanonicalisationRules.PlacementSource,
            ["toolchainIdentity"] = CanonicalJson.ToNode(_toolchainIdentity),
            ["simulatorOutputDigest"] = CanonicalJson.Digest(digestPayload),
            ["components"] = componentResults, ["placements"] = allPlacements, ["structuralImpacts"] = allImpacts
        };
    }

    public async Task FinalizeManifestAsync(FinalizationRequest request)
    {
        var @base = Verify(request.Snapshot, request.BaseManifest, request.ForbiddenValues);
        if (@base["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT")
            throw new B6PackageException("Finalization requires a verified BASE_SNAPSHOT manifest.");
        var chain = await BuildAuthoritativeChainAsync(request.Snapshot, @base, request.RootDispositions,
            request.Inventory, request.RuntimeState, request.Schema, request.InventorySql);
        CanonicalJson.WriteNew(request.AnalysisOutput, chain.Analysis);
        CanonicalJson.WriteNew(request.SimulationOutput, chain.Simulation);
        CanonicalJson.WriteNew(request.ImpactOutput, chain.Impact);
        CanonicalJson.WriteNew(request.Output, BuildFinalManifest(@base, chain));
    }

    public async Task WriteImpactReportAsync(string snapshot, string manifest, string rootDispositions, string inventory,
        string runtimeState, string schema, string inventorySql, string output, string? forbiddenValues)
    {
        var final = await VerifyFinalAsync(snapshot, manifest, rootDispositions, inventory,
            runtimeState, schema, inventorySql, forbiddenValues);
        if (final["manifestStage"]?.GetValue<string>() != "FINAL_ANALYSIS")
            throw new B6PackageException("Impact report requires a verified FINAL_ANALYSIS manifest.");
        var @base = CreateBaseManifestValue(snapshot);
        CanonicalJson.WriteNew(output, (await BuildAuthoritativeChainAsync(snapshot, @base, rootDispositions,
            inventory, runtimeState, schema, inventorySql)).Impact);
    }

    public async Task CreateHandoffReceiptAsync(string snapshot, string manifest, string rootDispositions, string inventory,
        string runtimeState, string schema, string inventorySql, string approvedDestination, string output,
        DateTimeOffset? generatedAt, string? forbiddenValues)
    {
        if (approvedDestination != B6Contract.ApprovedDestination)
            throw new B6PackageException("Handoff destination is not the package-owned isolated PostgreSQL 16 destination.");
        var final = await VerifyFinalAsync(snapshot, manifest, rootDispositions, inventory,
            runtimeState, schema, inventorySql, forbiddenValues);
        if (final["manifestStage"]?.GetValue<string>() != "FINAL_ANALYSIS")
            throw new B6PackageException("Receipt requires successful authoritative FINAL_ANALYSIS verification.");
        var receipt = new JsonObject
        {
            ["snapshotId"] = final["snapshotId"]!.DeepClone(),
            ["finalManifestDigest"] = final["manifestHash"]!.DeepClone(),
            ["analysisSnapshotCutoff"] = final["analysisSnapshotCutoff"]!.DeepClone(),
            ["packageVersion"] = B6Contract.PackageVersion,
            ["sanitisationVersion"] = B6Contract.SanitisationVersion,
            ["toolchainIdentity"] = CanonicalJson.ToNode(_toolchainIdentity),
            ["repositoryCommit"] = _toolchainIdentity.RepositoryCommit,
            ["approvedDestination"] = approvedDestination,
            ["verificationResult"] = "PASSED",
            ["operatorGeneratedAt"] = B6Values.Timestamp(generatedAt ?? DateTimeOffset.UtcNow)
        };
        receipt["receiptDigest"] = CanonicalJson.Digest(receipt);
        CanonicalJson.WriteNew(output, receipt);
    }

    private async Task<AuthoritativeChain> BuildAuthoritativeChainAsync(string snapshot, JsonObject @base,
        string rootDispositions, string inventoryPath, string runtimeState, string schema, string inventorySql)
    {
        var inventory = await VerifyInventoryAsync(@base, inventoryPath, runtimeState, schema, inventorySql);
        var rows = LoadRows(snapshot);
        var metadata = CanonicalJson.Read<SnapshotMetadata>(Path.Combine(snapshot, "snapshot-metadata.json"));
        var analysis = BuildAnalysis(rows, metadata, rootDispositions, @base);
        if (analysis["candidatePopulationDigest"]?.GetValue<string>() != @base["candidatePopulationDigest"]?.GetValue<string>())
            throw new B6PackageException("Reviewed analysis population differs from verified base population.");
        var input = analysis["simulatorInput"]!.Deserialize<SimulationInput>(CanonicalJson.Options)!;
        var simulation = Simulate(input);
        var impact = ComputeImpact(rows, analysis, simulation, metadata);
        return new AuthoritativeChain(inventory, analysis, simulation, impact);
    }

    private JsonObject ComputeImpact(Dictionary<string, List<Dictionary<string, string>>> rows,
        JsonObject analysis, JsonObject simulation, SnapshotMetadata metadata)
    {
        if (!JsonNode.DeepEquals(simulation["toolchainIdentity"], CanonicalJson.ToNode(_toolchainIdentity)) ||
            simulation["simulationVersion"]?.GetValue<string>() != B6Contract.SimulatorVersion)
            throw new B6PackageException("Impact input is not bound to the authoritative .NET simulator/tool.");
        var impacts = simulation["structuralImpacts"]!.AsArray();
        var changed = impacts.Where(item => item!["classification"]!.GetValue<string>() == "Changed")
            .Select(item => item!["participantSnapshotKey"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var ambiguous = impacts.Where(item => item!["classification"]!.GetValue<string>() == "Ambiguous")
            .Select(item => item!["participantSnapshotKey"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var quarantinedComponents = analysis["components"]!.AsArray()
            .Where(component => component!["status"]!.GetValue<string>() == "QUARANTINED")
            .OrderBy(component => component!["componentDigest"]!.GetValue<string>(), StringComparer.Ordinal)
            .ToList();
        var quarantinedMembership = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var component in quarantinedComponents)
        foreach (var participant in component!["participantSnapshotKeys"]!.AsArray()
                     .Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal))
            if (!quarantinedMembership.TryAdd(participant, component["componentDigest"]!.GetValue<string>()))
                throw new B6PackageException("Analysis assigns a participant to more than one quarantined component.");
        var quarantined = quarantinedMembership.Keys.ToHashSet(StringComparer.Ordinal);
        if (quarantined.Overlaps(impacts.Select(item => item!["participantSnapshotKey"]!.GetValue<string>())))
            throw new B6PackageException("A quarantined participant was supplied to the accepted-component simulator.");
        var allAmbiguous = ambiguous.Concat(quarantined).ToHashSet(StringComparer.Ordinal);
        var affected = changed.Concat(allAmbiguous).ToHashSet(StringComparer.Ordinal);
        var commissions = rows["EntryWeeklyCommissions"].Where(row => affected.Contains(row["EntryParticipationId"])).ToList();
        var participantImpacts = new JsonArray();
        var simulatedByParticipant = impacts.ToDictionary(
            item => item!["participantSnapshotKey"]!.GetValue<string>(), item => item!, StringComparer.Ordinal);
        foreach (var participant in simulatedByParticipant.Keys.Concat(quarantined).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var isQuarantined = quarantinedMembership.TryGetValue(participant, out var componentDigest);
            simulatedByParticipant.TryGetValue(participant, out var simulated);
            var participantCommissions = rows["EntryWeeklyCommissions"]
                .Where(row => row["EntryParticipationId"] == participant).ToList();
            participantImpacts.Add(new JsonObject
            {
                ["participantSnapshotKey"] = participant,
                ["componentStatus"] = isQuarantined ? "QUARANTINED" : "ACCEPTED",
                ["componentDigest"] = isQuarantined ? componentDigest : B6Contract.Null,
                ["structuralClassification"] = isQuarantined
                    ? "STRUCTURALLY_AMBIGUOUS"
                    : "DETERMINISTIC_" + simulated!["classification"]!.GetValue<string>().ToUpperInvariant(),
                ["effectiveV1StructuralLevel"] = isQuarantined
                    ? "NOT_DETERMINABLE_FOR_QUARANTINED_COMPONENT"
                    : simulated!["effectiveV1StructuralLevel"]!.DeepClone(),
                ["simulatedV2StructuralLevel"] = isQuarantined
                    ? "NOT_SIMULATED_COMPONENT_QUARANTINED"
                    : simulated!["simulatedV2StructuralLevel"]!.DeepClone(),
                ["isImpactIntersection"] = affected.Contains(participant),
                ["historicalEvidence"] = new JsonObject
                {
                    ["commissionRecords"] = participantCommissions.Count,
                    ["payoutStateRecords"] = Counts(participantCommissions.Select(row => row["PayoutStatus"])),
                    ["loanRecords"] = rows["OnyxLoanAgreements"].Count(row => row["EntryParticipationId"] == participant),
                    ["graduationRecords"] = rows["OnyxGraduationDecisions"].Count(row => row["EntryParticipationId"] == participant),
                    ["resultingOnyxParticipationRecords"] = rows["OnyxParticipations"].Count(row => row["EntryParticipationId"] == participant)
                }
            });
        }
        var result = new JsonObject
        {
            ["impactAnalysisVersion"] = B6Contract.ImpactVersion,
            ["snapshotId"] = metadata.SnapshotId, ["analysisSnapshotCutoff"] = metadata.AnalysisSnapshotCutoff,
            ["simulationVersion"] = B6Contract.SimulatorVersion,
            ["simulatorOutputDigest"] = simulation["simulatorOutputDigest"]!.DeepClone(),
            ["analysisArtifactDigest"] = CanonicalJson.Digest(analysis),
            ["toolchainIdentity"] = CanonicalJson.ToNode(_toolchainIdentity), ["historicalFactsModified"] = false,
            ["structural"] = new JsonObject
            {
                ["same"] = impacts.Count(item => item!["classification"]!.GetValue<string>() == "Same"),
                ["changed"] = changed.Count, ["ambiguous"] = allAmbiguous.Count,
                ["quarantinedComponentParticipants"] = quarantined.Count
            },
            ["historicalCommissionIntersections"] = commissions.Count,
            ["payoutStateIntersections"] = Counts(commissions.Select(row => row["PayoutStatus"])),
            ["loanIntersections"] = rows["OnyxLoanAgreements"].Count(row => affected.Contains(row["EntryParticipationId"])),
            ["aqGreenGraduationIntersections"] = rows["OnyxGraduationDecisions"].Count(row => affected.Contains(row["EntryParticipationId"])),
            ["onyxParticipationIntersections"] = rows["OnyxParticipations"].Count(row => row["EntryParticipationId"] != B6Contract.Null && affected.Contains(row["EntryParticipationId"])),
            ["participantImpacts"] = participantImpacts,
            ["affectedParticipantSnapshotKeys"] = new JsonArray(affected.Order(StringComparer.Ordinal)
                .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray())
        };
        result["impactReportDigest"] = CanonicalJson.Digest(result);
        return result;
    }

    private JsonObject BuildFinalManifest(JsonObject @base, AuthoritativeChain chain)
    {
        var result = @base.DeepClone().AsObject();
        var baseHash = result["manifestHash"]!.GetValue<string>(); result.Remove("manifestHash");
        result["manifestStage"] = "FINAL_ANALYSIS"; result["baseManifestHash"] = baseHash;
        result["analysisDigests"] = new JsonObject
        {
            ["rootDispositionDigest"] = chain.Analysis["rootDispositionDigest"]!.DeepClone(),
            ["canonicalisationPopulationDigest"] = chain.Analysis["populationDigests"]!["canonicalisation"]!.DeepClone(),
            ["qualificationEligibilityDigest"] = chain.Analysis["populationDigests"]!["qualificationEligibility"]!.DeepClone(),
            ["historicalImpactInventoryDigest"] = chain.Analysis["populationDigests"]!["historicalImpactInventory"]!.DeepClone(),
            ["acceptedComponentDigest"] = chain.Analysis["acceptedComponentDigest"]!.DeepClone(),
            ["quarantinedComponentDigest"] = chain.Analysis["quarantinedComponentDigest"]!.DeepClone(),
            ["analysisArtifactDigest"] = CanonicalJson.Digest(chain.Analysis),
            ["simulatorInputDigest"] = CanonicalJson.Digest(chain.Analysis["simulatorInput"]!),
            ["simulatorOutputDigest"] = chain.Simulation["simulatorOutputDigest"]!.DeepClone(),
            ["simulationArtifactDigest"] = CanonicalJson.Digest(chain.Simulation),
            ["impactReportDigest"] = chain.Impact["impactReportDigest"]!.DeepClone(),
            ["inventoryDigest"] = chain.Inventory["inventoryDigest"]!.DeepClone(),
            ["inventoryResultDigest"] = chain.Inventory["inventoryResultDigest"]!.DeepClone(),
            ["inventorySemanticDigest"] = chain.Inventory["inventorySemanticDigest"]!.DeepClone(),
            ["inventoryQueryDigest"] = chain.Inventory["inventoryQueryDigest"]!.DeepClone(),
            ["toolchainDigest"] = CanonicalJson.Digest(CanonicalJson.ToNode(_toolchainIdentity))
        };
        AddSelfHash(result);
        return result;
    }

    private static JsonObject Counts(IEnumerable<string> values)
    {
        var result = new JsonObject();
        foreach (var group in values.GroupBy(value => value).OrderBy(group => group.Key, StringComparer.Ordinal)) result[group.Key] = group.Count();
        return result;
    }

    private static T ParseEnum<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed : throw new B6PackageException($"Unsupported {typeof(T).Name} value {value}.");

    private sealed record AuthoritativeChain(JsonObject Inventory, JsonObject Analysis, JsonObject Simulation, JsonObject Impact);
}
