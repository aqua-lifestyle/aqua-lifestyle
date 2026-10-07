using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed partial class B6SnapshotPackage
{
    public void Analyse(string snapshot, string manifest, string rootDispositions, string output,
        string? simulatorInput, string? forbiddenValues)
    {
        var verified = Verify(snapshot, manifest, forbiddenValues);
        if (verified["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT")
            throw new B6PackageException("Standalone analysis requires a verified base manifest.");
        var rows = LoadRows(snapshot);
        var metadata = CanonicalJson.Read<SnapshotMetadata>(Path.Combine(snapshot, "snapshot-metadata.json"));
        var analysis = BuildAnalysis(rows, metadata, rootDispositions, verified);
        CanonicalJson.WriteNew(output, analysis);
        if (simulatorInput != null) CanonicalJson.WriteNew(simulatorInput, analysis["simulatorInput"]!.DeepClone());
    }

    private JsonObject BuildAnalysis(Dictionary<string, List<Dictionary<string, string>>> rows,
        SnapshotMetadata metadata, string? rootDispositionPath, JsonObject? baseManifest = null)
    {
        var cutoff = B6Values.ParseTimestamp(metadata.AnalysisSnapshotCutoff);
        var (dispositions, dispositionDigest) = LoadRootDispositions(
            rootDispositionPath, metadata.SnapshotId, baseManifest);
        var customers = ById(rows["Customers"]);
        var participations = ById(rows["EntryParticipations"]);
        var customerParticipations = participations.Values.GroupBy(row => (row["TenantId"], row["CustomerId"]))
            .ToDictionary(group => group.Key, group => group.ToList());
        var corrections = Group(rows["EntryRecruiterCorrections"], "EntryParticipationId");
        var decisions = Group(rows["EntryParticipationApprovalDecisions"], "EntryParticipationId");
        var attributions = Group(rows["AQGreenRecruitmentAttributions"], "ParticipantId");
        var confirmations = Group(rows["AQGreenRecruitmentAttributionConfirmations"], "AttributionId");
        var existingV2 = rows["AQGreenNetworkPlacements"].Select(row => row["ParticipantId"]).ToHashSet(StringComparer.Ordinal);
        var duplicateCustomers = customerParticipations.Where(item => item.Value.Count > 1).Select(item => item.Key).ToHashSet();
        var excluded = new List<JsonObject>();
        var nodes = new Dictionary<string, AnalysisNode>(StringComparer.Ordinal);

        foreach (var participant in participations.Values.OrderBy(row => row["Id"], StringComparer.Ordinal))
        {
            var key = participant["Id"];
            var nodeAnomalies = new List<AnalysisAnomaly>();
            var history = corrections.GetValueOrDefault(key, []).OrderBy(row => B6Values.ParseTimestamp(row["CorrectedAt"]))
                .ThenBy(row => row["Id"], StringComparer.Ordinal).ToList();
            if (history.GroupBy(row => row["CorrectedAt"], StringComparer.Ordinal).Any(group => group.Count() > 1))
                nodeAnomalies.Add(new("AMBIGUOUS_EQUAL_CORRECTION_TIME", "Recruiter corrections share an effective time."));
            for (var index = 0; index < history.Count; index++)
            {
                var correction = history[index];
                if (correction["PreviousRecruiterCustomerId"] == correction["NewRecruiterCustomerId"])
                    nodeAnomalies.Add(new("NO_OP_CORRECTION", "Recruiter correction does not change recruiter evidence."));
                if (B6Values.ParseTimestamp(correction["CorrectedAt"]) < B6Values.ParseTimestamp(participant["StartedAt"]))
                    nodeAnomalies.Add(new("CORRECTION_BEFORE_START", "Recruiter correction predates participation start."));
                if (index > 0 && correction["PreviousRecruiterCustomerId"] != history[index - 1]["NewRecruiterCustomerId"])
                    nodeAnomalies.Add(new("DISCONTINUOUS_CORRECTION", "Recruiter correction chain is discontinuous."));
            }
            if (history.Count > 0 && history[^1]["NewRecruiterCustomerId"] != participant["RecruiterCustomerId"])
                nodeAnomalies.Add(new("CORRECTION_CURRENT_STATE_MISMATCH", "Correction chain does not end at current recruiter."));

            var activatedAt = participant["ActivatedAt"] == B6Contract.Null
                ? (DateTimeOffset?)null : B6Values.ParseTimestamp(participant["ActivatedAt"]);
            var status = (EntryParticipationStatus)int.Parse(participant["Status"], CultureInfo.InvariantCulture);
            var approval = decisions.GetValueOrDefault(key, []);
            var authoritativeApproval = approval.Count == 1 && approval[0]["Approved"] == "true" &&
                approval[0]["ActorPresent"] == "true";
            var approvalAt = authoritativeApproval
                ? B6Values.ParseTimestamp(approval[0]["DecidedAt"])
                : (DateTimeOffset?)null;
            var conclusivelyPostCutoff = status == EntryParticipationStatus.Active &&
                (activatedAt > cutoff || approvalAt > cutoff);
            var cutoffGateEffective = status == EntryParticipationStatus.Active && !conclusivelyPostCutoff &&
                participant["IsDeleted"] != "true" && activatedAt.HasValue && activatedAt <= cutoff &&
                approvalAt.HasValue && approvalAt <= cutoff;
            var topologyState = status is EntryParticipationStatus.AwaitingJoiningPayment or
                    EntryParticipationStatus.AwaitingActivationPayment or
                    EntryParticipationStatus.PaymentConfirmedAwaitingApproval or EntryParticipationStatus.Rejected
                ? CutoffTopologyState.OutsideCanonicalisation
                : conclusivelyPostCutoff
                    ? CutoffTopologyState.ConclusivePostCutoff
                    : cutoffGateEffective
                        ? CutoffTopologyState.CutoffEffective
                        : CutoffTopologyState.Indeterminate;
            var effectiveRecruiter = history.Count > 0 ? history[0]["PreviousRecruiterCustomerId"] : participant["RecruiterCustomerId"];
            DateTimeOffset? qualifiedAt = null;
            var orderingDeterminate = false;
            if (activatedAt.HasValue)
            {
                try
                {
                    var evidence = EffectiveRecruiterOrdering.Derive(
                        int.Parse(participant["CustomerId"], CultureInfo.InvariantCulture),
                        ParseNullableCustomerId(participant["RecruiterCustomerId"]),
                        B6Values.ParseTimestamp(participant["StartedAt"]).UtcDateTime,
                        activatedAt.Value.UtcDateTime,
                        history.Select(correction => new EffectiveRecruiterCorrectionEvidence(
                            ParseNullableCustomerId(correction["PreviousRecruiterCustomerId"]),
                            ParseNullableCustomerId(correction["NewRecruiterCustomerId"]),
                            B6Values.ParseTimestamp(correction["CorrectedAt"]).UtcDateTime)),
                        cutoff.UtcDateTime,
                        "AQGreen");
                    effectiveRecruiter = evidence.EffectiveRecruiterCustomerId?.ToString(CultureInfo.InvariantCulture) ?? B6Contract.Null;
                    qualifiedAt = new DateTimeOffset(DateTime.SpecifyKind(evidence.QualifiedUnderRecruiterAt, DateTimeKind.Utc));
                    orderingDeterminate = true;
                }
                catch (InvalidOperationException exception)
                {
                    nodeAnomalies.Add(new("INDETERMINATE_V1_ORDERING", exception.Message));
                }
            }

            string? sponsorKey = null;
            var exceptionalRelatedKeys = new HashSet<string>(StringComparer.Ordinal);
            var sponsorStatus = AQGreenLegacySponsorEvidenceStatus.MissingSponsorEvidence;
            var cutoffCorrectionRelatedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (effectiveRecruiter != B6Contract.Null)
            {
                customers.TryGetValue(effectiveRecruiter, out var owner);
                var matches = customerParticipations.GetValueOrDefault((participant["TenantId"], effectiveRecruiter), []);
                if (owner == null || matches.Count == 0)
                    nodeAnomalies.Add(new("DANGLING_SPONSOR", "Recruiter does not resolve to an AQGreen participation."));
                else if (owner["TenantId"] != participant["TenantId"])
                {
                    nodeAnomalies.Add(new("CROSS_TENANT", "Recruiter evidence crosses Tenant boundary."));
                    sponsorStatus = AQGreenLegacySponsorEvidenceStatus.RejectedSponsor;
                }
                else if (matches.Count > 1)
                {
                    nodeAnomalies.Add(new("AMBIGUOUS_SPONSOR", "Recruiter resolves to multiple AQGreen participations."));
                    sponsorStatus = AQGreenLegacySponsorEvidenceStatus.AmbiguousSponsor;
                }
                else { sponsorKey = matches[0]["Id"]; sponsorStatus = AQGreenLegacySponsorEvidenceStatus.AcceptedSponsor; }
            }
            if (!orderingDeterminate && history.Count > 0)
            {
                var implicatedRecruiters = history.SelectMany(row => new[]
                    { row["PreviousRecruiterCustomerId"], row["NewRecruiterCustomerId"] })
                    .Append(participant["RecruiterCustomerId"])
                    .Where(value => value != B6Contract.Null)
                    .Distinct(StringComparer.Ordinal);
                foreach (var recruiter in implicatedRecruiters)
                foreach (var match in customerParticipations.GetValueOrDefault((participant["TenantId"], recruiter), []))
                    exceptionalRelatedKeys.Add(match["Id"]);
            }
            foreach (var recruiter in history.Where(row => B6Values.ParseTimestamp(row["CorrectedAt"]) <= cutoff)
                         .SelectMany(row => new[] { row["PreviousRecruiterCustomerId"], row["NewRecruiterCustomerId"] })
                         .Where(value => value != B6Contract.Null).Distinct(StringComparer.Ordinal))
            foreach (var match in customerParticipations.GetValueOrDefault((participant["TenantId"], recruiter), []))
                cutoffCorrectionRelatedKeys.Add(match["Id"]);
            var rootCandidate = effectiveRecruiter == B6Contract.Null;
            var rootStatus = AQGreenLegacyRootEvidenceStatus.NotRoot;
            if (rootCandidate)
            {
                rootStatus = dispositions.TryGetValue(key, out var disposition)
                    ? Enum.Parse<AQGreenLegacyRootEvidenceStatus>(disposition.Disposition)
                    : AQGreenLegacyRootEvidenceStatus.RootCandidate;
                sponsorStatus = AQGreenLegacySponsorEvidenceStatus.NotApplicableToRoot;
            }
            else if (dispositions.ContainsKey(key))
                nodeAnomalies.Add(new("INVALID_ROOT_DISPOSITION", "Root disposition targets a participant with recruiter evidence."));

            var eligible = true;
            if (!orderingDeterminate)
                eligible = false;
            if (conclusivelyPostCutoff)
            {
                excluded.Add(new JsonObject
                {
                    ["participantSnapshotKey"] = key,
                    ["status"] = ((int)status).ToString(CultureInfo.InvariantCulture),
                    ["classification"] = "CONCLUSIVE_POST_CUTOFF"
                });
                eligible = false;
            }
            else if (participant["IsDeleted"] == "true")
            {
                nodeAnomalies.Add(new("UNSUPPORTED_LIFECYCLE_STATE", "Deleted/post-Active disposition requires D08 exception review."));
                eligible = false;
            }
            else if (status is EntryParticipationStatus.AwaitingJoiningPayment or EntryParticipationStatus.AwaitingActivationPayment or
                     EntryParticipationStatus.PaymentConfirmedAwaitingApproval or EntryParticipationStatus.Rejected)
            {
                excluded.Add(new JsonObject { ["participantSnapshotKey"] = key, ["status"] = ((int)status).ToString(CultureInfo.InvariantCulture) });
                eligible = false;
            }
            else if (status != EntryParticipationStatus.Active)
            {
                nodeAnomalies.Add(new("UNSUPPORTED_LIFECYCLE_STATE", "Unrecognised AQGreen participation state.")); eligible = false;
            }
            if (!conclusivelyPostCutoff && (!activatedAt.HasValue || activatedAt > cutoff))
            {
                nodeAnomalies.Add(new("MISSING_ACTIVATION_EVIDENCE", "Active state lacks cutoff-effective activation evidence.")); eligible = false;
            }
            if (!conclusivelyPostCutoff && approval.Count != 1)
            {
                nodeAnomalies.Add(new("MISSING_APPROVAL_EVIDENCE", "Exactly one retained approval decision is required.")); eligible = false;
            }
            else if (!conclusivelyPostCutoff && (approval[0]["Approved"] != "true" || approval[0]["ActorPresent"] != "true" ||
                     B6Values.ParseTimestamp(approval[0]["DecidedAt"]) > cutoff))
            {
                nodeAnomalies.Add(new("CONTRADICTORY_APPROVAL_EVIDENCE", "Approval evidence is not cutoff-effective activation authority.")); eligible = false;
            }
            if (duplicateCustomers.Contains((participant["TenantId"], participant["CustomerId"])))
            {
                nodeAnomalies.Add(new("DUPLICATE_CUSTOMER_PARTICIPATION", "Customer has multiple AQGreen participations.")); eligible = false;
            }

            var provenance = AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition;
            var attrs = attributions.GetValueOrDefault(key, []);
            if (attrs.Count == 1)
            {
                var attribution = attrs[0]; var confirms = confirmations.GetValueOrDefault(attribution["Id"], []);
                var complete = confirms.Count == 1 && B6Values.ParseTimestamp(attribution["AttributedAt"]) <= cutoff &&
                    B6Values.ParseTimestamp(confirms[0]["ConfirmedAt"]) <= cutoff;
                if (complete)
                {
                    provenance = AQGreenLegacyAcquisitionProvenance.Known;
                    var source = (AQGreenAcquisitionSource)int.Parse(attribution["AcquisitionSource"], CultureInfo.InvariantCulture);
                    if (source == AQGreenAcquisitionSource.MemberInvitation && attribution["CreditedSponsorParticipantId"] != (sponsorKey ?? B6Contract.Null))
                    {
                        sponsorStatus = AQGreenLegacySponsorEvidenceStatus.AmbiguousSponsor;
                        nodeAnomalies.Add(new("SPONSOR_EVIDENCE_CONFLICT", "Invitation attribution sponsor conflicts with recruiter evidence."));
                    }
                    if (source == AQGreenAcquisitionSource.AuthorisedDirectAdmission && !rootCandidate)
                        nodeAnomalies.Add(new("ROOT_EVIDENCE_CONFLICT", "Direct-admission attribution conflicts with recruiter evidence."));
                }
                else nodeAnomalies.Add(new("INCOMPLETE_ATTRIBUTION_EVIDENCE", "Attribution lacks cutoff-effective matching confirmation."));
            }
            else if (attrs.Count > 1) nodeAnomalies.Add(new("AMBIGUOUS_ATTRIBUTION_EVIDENCE", "Participant has multiple attributions."));
            if (existingV2.Contains(key)) nodeAnomalies.Add(new("CONFLICTING_EXISTING_V2", "Existing V2 placement requires exact-manifest reconciliation."));

            nodes[key] = new AnalysisNode(int.Parse(participant["TenantId"], CultureInfo.InvariantCulture), key,
                sponsorKey, exceptionalRelatedKeys, cutoffCorrectionRelatedKeys, new HashSet<string>(StringComparer.Ordinal),
                topologyState, qualifiedAt, long.Parse(participant["LegacyOrderingTieBreakOrdinal"], CultureInfo.InvariantCulture),
                provenance, sponsorStatus, rootStatus, rootCandidate, eligible, nodeAnomalies);
        }
        if (dispositions.Keys.Except(nodes.Keys, StringComparer.Ordinal).Any())
            throw new B6PackageException("Root disposition references a participant outside the snapshot.");

        foreach (var node in nodes.Values.Where(value => value.TopologyState == CutoffTopologyState.CutoffEffective))
        {
            var dependencies = node.CutoffCorrectionRelatedKeys
                .Concat(node.SponsorKey is { } sponsor ? [sponsor] : [])
                .Distinct(StringComparer.Ordinal);
            foreach (var dependency in dependencies.Where(nodes.ContainsKey))
            {
                if (nodes[dependency].TopologyState != CutoffTopologyState.ConclusivePostCutoff) continue;
                node.ContradictoryRelatedKeys.Add(dependency);
                node.Anomalies.Add(new("CONTRADICTORY_POST_CUTOFF_TOPOLOGY",
                    "Cutoff-effective sponsor/correction evidence depends on a participant conclusively effective only after the cutoff."));
            }
        }

        var eligibleKeys = nodes.Where(item => item.Value.Eligible).Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var topology = nodes.Where(item => item.Value.TopologyState is CutoffTopologyState.CutoffEffective or CutoffTopologyState.Indeterminate)
            .Select(item => item.Key).Concat(eligibleKeys).ToHashSet(StringComparer.Ordinal);
        var pendingTopology = new Queue<string>(topology);
        while (pendingTopology.Count > 0)
        {
            var current = pendingTopology.Dequeue();
            var related = nodes[current].ExceptionalRelatedKeys
                .Concat(nodes[current].ContradictoryRelatedKeys)
                .Concat(nodes[current].SponsorKey is { } sponsor ? [sponsor] : [])
                .Where(nodes.ContainsKey);
            foreach (var key in related)
                if (topology.Add(key)) pendingTopology.Enqueue(key);
        }
        var adjacency = topology.ToDictionary(key => key, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var key in topology)
        {
            if (nodes[key].SponsorKey is { } sponsor && topology.Contains(sponsor)) { adjacency[key].Add(sponsor); adjacency[sponsor].Add(key); }
            foreach (var related in nodes[key].ExceptionalRelatedKeys.Where(topology.Contains))
            {
                adjacency[key].Add(related);
                adjacency[related].Add(key);
            }
            foreach (var related in nodes[key].ContradictoryRelatedKeys.Where(topology.Contains))
            {
                adjacency[key].Add(related);
                adjacency[related].Add(key);
            }
        }

        var components = new List<AnalysisComponent>(); var acceptedKeys = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in topology.Order(StringComparer.Ordinal))
        {
            if (visited.Contains(start)) continue;
            var queue = new Stack<string>(); queue.Push(start); var keys = new HashSet<string>(StringComparer.Ordinal);
            while (queue.Count > 0)
            {
                var current = queue.Pop(); if (!keys.Add(current)) continue; visited.Add(current);
                foreach (var related in adjacency[current]) if (!keys.Contains(related)) queue.Push(related);
            }
            var anomalies = keys.SelectMany(key => nodes[key].Anomalies.Select(item => new ComponentAnomaly(item.Code, key, item.Reason))).ToList();
            foreach (var key in keys.Where(key => !nodes[key].Eligible))
                anomalies.Add(new("EXCEPTION_ANCESTOR", key, "A non-canonical lifecycle/approval record participates in sponsor ancestry; the complete component is quarantined."));
            var roots = keys.Where(key => nodes[key].RootCandidate).Order(StringComparer.Ordinal).ToList();
            var acceptedRoots = roots.Where(key => nodes[key].RootStatus == AQGreenLegacyRootEvidenceStatus.AcceptedRoot).ToList();
            if (roots.Count == 0) anomalies.Add(new("MISSING_ROOT_EVIDENCE", keys.Min(StringComparer.Ordinal)!, "Component has no recruiterless root candidate."));
            else if (roots.Count > 1) anomalies.Add(new("AMBIGUOUS_ROOT", roots[0], "Component has multiple root candidates."));
            else if (acceptedRoots.Count != 1) anomalies.Add(new(nodes[roots[0]].RootStatus == AQGreenLegacyRootEvidenceStatus.RootCandidate ? "MISSING_ROOT_EVIDENCE" : "ROOT_NOT_ACCEPTED",
                roots[0], "Recruiterless candidate lacks explicit accepted reviewed root evidence."));
            if (ContainsCycle(keys, nodes)) anomalies.Add(new("CYCLE", keys.Min(StringComparer.Ordinal)!, "Sponsor graph contains a cycle."));
            var accepted = anomalies.Count == 0 && acceptedRoots.Count == 1;
            if (accepted) acceptedKeys.UnionWith(keys);
            var digest = B6Digests.Tuples(keys.Select(key => new[] { key, nodes[key].SponsorKey ?? B6Contract.Null,
                string.Join(',', nodes[key].ExceptionalRelatedKeys.Order(StringComparer.Ordinal)),
                string.Join(',', nodes[key].ContradictoryRelatedKeys.Order(StringComparer.Ordinal)),
                nodes[key].SponsorStatus.ToString(), nodes[key].RootStatus.ToString() }));
            components.Add(new AnalysisComponent(digest, acceptedRoots.SingleOrDefault(), roots, keys.Order(StringComparer.Ordinal).ToList(), accepted, anomalies));
        }

        var simulatorComponents = new JsonArray();
        foreach (var component in components.Where(value => value.Accepted).OrderBy(value => value.RootKey, StringComparer.Ordinal))
        {
            var root = nodes[component.RootKey!];
            var scope = DeterministicUuid.CreateUrlV5($"AQGreenB6Scope:{root.Tenant}:{root.Key}");
            var candidates = new JsonArray();
            foreach (var key in component.Keys)
            {
                var node = nodes[key];
                var level = CalculateV1Level(key, component.Keys, nodes);
                var sourceDigest = B6Digests.Tuples([new[] { key, node.SponsorKey ?? B6Contract.Null,
                    B6Values.Timestamp(node.QualifiedAt!.Value), node.OrderingOrdinal.ToString(CultureInfo.InvariantCulture),
                    node.Provenance.ToString(), node.SponsorStatus.ToString(), node.RootStatus.ToString(), dispositionDigest }]);
                candidates.Add(new JsonObject
                {
                    ["tenantSnapshotKey"] = node.Tenant, ["participantSnapshotKey"] = key,
                    ["acceptedLegacySponsorSnapshotKey"] = node.SponsorKey,
                    ["qualifiedUnderRecruiterAtUtc"] = B6Values.Timestamp(node.QualifiedAt!.Value),
                    ["legacyOrderingTieBreakOrdinal"] = node.OrderingOrdinal,
                    ["acquisitionProvenance"] = node.Provenance.ToString(), ["sponsorEvidenceStatus"] = node.SponsorStatus.ToString(),
                    ["rootEvidenceStatus"] = node.RootStatus.ToString(), ["effectiveV1StructuralLevel"] = level,
                    ["sourceEvidenceDigest"] = sourceDigest
                });
            }
            simulatorComponents.Add(new JsonObject
            {
                ["tenantSnapshotKey"] = root.Tenant, ["placementTreeScopeSnapshotKey"] = scope.ToString("D"),
                ["analysisSnapshotCutoffUtc"] = metadata.AnalysisSnapshotCutoff, ["candidates"] = candidates
            });
        }

        var qualification = nodes.Where(item => item.Value.Eligible &&
                (EntryParticipationStatus)int.Parse(participations[item.Key]["Status"], CultureInfo.InvariantCulture) == EntryParticipationStatus.Active)
            .Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var historical = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in new[] { "EntryWeeklyCommissions", "OnyxLoanAgreements", "OnyxGraduationDecisions", "OnyxParticipations" })
        foreach (var row in rows[table]) if (row["EntryParticipationId"] != B6Contract.Null) historical.Add(row["EntryParticipationId"]);
        var componentJson = new JsonArray(components.OrderBy(value => value.Keys[0], StringComparer.Ordinal).Select(ComponentJson).ToArray());
        var allAnomalies = components.SelectMany(component => component.Anomalies).ToList();
        var sponsorCounts = topology.Select(key => nodes[key]).Where(node => node.SponsorKey != null)
            .GroupBy(node => node.SponsorKey!).Select(group => group.Count());
        var populationRows = nodes.Values.Where(node => node.Eligible).Select(node => new[] { node.Key,
            node.Tenant.ToString(CultureInfo.InvariantCulture), node.SponsorKey ?? B6Contract.Null, B6Values.Timestamp(node.QualifiedAt!.Value),
            node.OrderingOrdinal.ToString(CultureInfo.InvariantCulture), node.Provenance.ToString(), node.SponsorStatus.ToString() });
        var counts = new JsonObject
        {
            ["tenantCount"] = rows["EntryParticipations"].Select(row => row["TenantId"]).Distinct().Count(),
            ["aqGreenParticipantCount"] = participations.Count, ["candidateCanonicalisationPopulation"] = eligibleKeys.Count,
            ["qualificationEligibleActiveAtCutoff"] = qualification.Count, ["historicalImpactParticipants"] = historical.Count,
            ["historicalImpactRecords"] = new[] { "EntryWeeklyCommissions", "OnyxLoanAgreements", "OnyxGraduationDecisions", "OnyxParticipations" }.Sum(name => rows[name].Count),
            ["candidateRoots"] = nodes.Values.Count(node => node.RootCandidate), ["rootSpecificComponents"] = components.Count,
            ["acceptedComponents"] = components.Count(component => component.Accepted), ["quarantinedComponents"] = components.Count(component => !component.Accepted),
            ["acceptedParticipants"] = acceptedKeys.Count, ["blockedParticipants"] = topology.Except(acceptedKeys).Count(),
            ["legacyUnknownAcquisition"] = nodes.Values.Count(node => node.Provenance == AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition),
            ["excludedPreActivationOrRejected"] = excluded.Count,
            ["parentsWithMoreThanFiveEffectiveRecruiterChildren"] = sponsorCounts.Count(count => count > 5),
            ["recruiterCorrectionChains"] = corrections.Count
        };
        var anomalyCounts = new JsonObject();
        foreach (var group in allAnomalies.GroupBy(item => item.Code).OrderBy(group => group.Key, StringComparer.Ordinal)) anomalyCounts[group.Key] = group.Count();
        return new JsonObject
        {
            ["analysisVersion"] = B6Contract.AnalysisVersion, ["rootReviewVersion"] = B6Contract.RootReviewVersion,
            ["rootDispositionDigest"] = dispositionDigest, ["counts"] = counts, ["anomalyCounts"] = anomalyCounts,
            ["excluded"] = new JsonArray(excluded.OrderBy(item => item["participantSnapshotKey"]!.GetValue<string>(), StringComparer.Ordinal).ToArray()),
            ["populations"] = new JsonObject { ["canonicalisation"] = Strings(acceptedKeys), ["qualificationEligibility"] = Strings(qualification), ["historicalImpactInventory"] = Strings(historical) },
            ["populationDigests"] = new JsonObject { ["canonicalisation"] = KeyDigest(acceptedKeys), ["qualificationEligibility"] = KeyDigest(qualification), ["historicalImpactInventory"] = KeyDigest(historical) },
            ["components"] = componentJson,
            ["simulatorInput"] = new JsonObject { ["snapshotId"] = metadata.SnapshotId, ["analysisSnapshotCutoff"] = metadata.AnalysisSnapshotCutoff, ["components"] = simulatorComponents },
            ["candidatePopulationDigest"] = B6Digests.Tuples(populationRows),
            ["acceptedComponentDigest"] = B6Digests.Tuples(components.Where(value => value.Accepted).Select(value => new[] { value.Digest })),
            ["quarantinedComponentDigest"] = B6Digests.Tuples(components.Where(value => !value.Accepted).Select(value => new[] { value.Digest }))
        };
    }

    private (Dictionary<string, RootDisposition>, string) LoadRootDispositions(
        string? path, string snapshotId, JsonObject? baseManifest)
    {
        if (path == null) return (new(StringComparer.Ordinal), B6Digests.Tuples([]));
        if (baseManifest == null || baseManifest["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT")
            throw new B6PackageException("Root dispositions require verified base snapshot authority.");
        var node = CanonicalJson.Read(path).AsObject();
        if (!node.Select(item => item.Key).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["schemaVersion", "snapshotId", "sourceSnapshotDigest", "baseManifestDigest", "dispositions"]))
            throw new B6PackageException("Root disposition file has unexpected fields.");
        var value = node.Deserialize<RootDispositionFile>(CanonicalJson.Options)!;
        if (value.SchemaVersion != B6Contract.RootReviewVersion || value.SnapshotId != snapshotId ||
            value.SourceSnapshotDigest != baseManifest["overallSnapshotDigest"]?.GetValue<string>() ||
            value.BaseManifestDigest != baseManifest["manifestHash"]?.GetValue<string>())
            throw new B6PackageException("Root disposition identity mismatches snapshot.");
        var result = new Dictionary<string, RootDisposition>(StringComparer.Ordinal);
        foreach (var itemNode in node["dispositions"]!.AsArray())
        {
            var fields = itemNode!.AsObject().Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
            if (!fields.SetEquals(["participantSnapshotKey", "disposition", "evidenceType", "evidenceDigest", "decisionReference", "reviewedAt", "reviewedByRole"]))
                throw new B6PackageException("Root disposition lacks the exact reviewed contract.");
        }
        foreach (var item in value.Dispositions)
        {
            if (!Guid.TryParse(item.ParticipantSnapshotKey, out var key) || key == Guid.Empty || !result.TryAdd(key.ToString("D"), item) ||
                item.Disposition is not ("AcceptedRoot" or "RejectedRoot" or "AmbiguousRoot") ||
                item.EvidenceType != "ReviewedLegacyRootEvidenceV1" || !B6Contract.Sha256.IsMatch(item.EvidenceDigest) ||
                !B6Contract.ControlledCode.IsMatch(item.DecisionReference) || item.ReviewedByRole != "AuthorisedB6RootReviewer")
                throw new B6PackageException("Root disposition reviewed evidence is invalid or duplicated.");
            item.ReviewedAt = B6Values.Timestamp(B6Values.ParseTimestamp(item.ReviewedAt));
        }
        var digest = B6Digests.Tuples(new[]
            {
                new[] { "SNAPSHOT_AUTHORITY", value.SnapshotId, value.SourceSnapshotDigest, value.BaseManifestDigest }
            }
            .Concat(result.OrderBy(item => item.Key).Select(item => new[] { item.Key, item.Value.Disposition,
                item.Value.EvidenceType, item.Value.EvidenceDigest, item.Value.DecisionReference, item.Value.ReviewedAt, item.Value.ReviewedByRole })));
        return (result, digest);
    }

    private static int CalculateV1Level(string anchor, IReadOnlyCollection<string> keys, IReadOnlyDictionary<string, AnalysisNode> nodes)
    {
        var children = keys.Where(key => nodes[key].SponsorKey != null).GroupBy(key => nodes[key].SponsorKey!)
            .ToDictionary(group => group.Key, group => group.OrderBy(key => nodes[key].QualifiedAt!.Value)
                .ThenBy(key => nodes[key].OrderingOrdinal).Take(5).ToList(), StringComparer.Ordinal);
        var counts = new int[4]; var queue = new Queue<(string Key, int Depth)>();
        foreach (var child in children.GetValueOrDefault(anchor, [])) queue.Enqueue((child, 1));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var item = queue.Dequeue(); if (item.Depth > 3 || !visited.Add(item.Key)) continue; counts[item.Depth]++;
            foreach (var child in children.GetValueOrDefault(item.Key, [])) queue.Enqueue((child, item.Depth + 1));
        }
        if (counts[1] == 5 && counts[2] == 25 && counts[3] == 125) return 3;
        if (counts[1] == 5 && counts[2] == 25) return 2;
        return counts[1] == 5 ? 1 : 0;
    }

    private static bool ContainsCycle(IEnumerable<string> keys, IReadOnlyDictionary<string, AnalysisNode> nodes)
    {
        var set = keys.ToHashSet(StringComparer.Ordinal);
        foreach (var start in set)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal); string? current = start;
            while (current != null && set.Contains(current)) { if (!seen.Add(current)) return true; current = nodes[current].SponsorKey; }
        }
        return false;
    }

    private static Dictionary<string, Dictionary<string, string>> ById(IEnumerable<Dictionary<string, string>> rows) =>
        rows.ToDictionary(row => row["Id"], StringComparer.Ordinal);
    private static int? ParseNullableCustomerId(string value) => value == B6Contract.Null
        ? null
        : int.Parse(value, CultureInfo.InvariantCulture);
    private static Dictionary<string, List<Dictionary<string, string>>> Group(IEnumerable<Dictionary<string, string>> rows, string field) =>
        rows.GroupBy(row => row[field], StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    private static JsonArray Strings(IEnumerable<string> values) => new(values.Order(StringComparer.Ordinal)
        .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static string KeyDigest(IEnumerable<string> values) => B6Digests.Tuples(values.Select(value => new[] { value }));
    private static JsonObject ComponentJson(AnalysisComponent component) => new()
    {
        ["componentDigest"] = component.Digest, ["rootSnapshotKey"] = component.RootKey,
        ["rootCandidateSnapshotKeys"] = Strings(component.RootCandidates), ["participantSnapshotKeys"] = Strings(component.Keys),
        ["status"] = component.Accepted ? "ACCEPTED" : "QUARANTINED",
        ["anomalies"] = new JsonArray(component.Anomalies.OrderBy(item => item.Code).ThenBy(item => item.ParticipantKey).Select(item =>
            (JsonNode)new JsonObject { ["code"] = item.Code, ["participantSnapshotKey"] = item.ParticipantKey, ["reason"] = item.Reason }).ToArray())
    };

    private sealed record AnalysisAnomaly(string Code, string Reason);
    private sealed record ComponentAnomaly(string Code, string ParticipantKey, string Reason);
    private enum CutoffTopologyState { OutsideCanonicalisation, ConclusivePostCutoff, CutoffEffective, Indeterminate }
    private sealed record AnalysisNode(int Tenant, string Key, string? SponsorKey, IReadOnlySet<string> ExceptionalRelatedKeys,
        IReadOnlySet<string> CutoffCorrectionRelatedKeys, HashSet<string> ContradictoryRelatedKeys,
        CutoffTopologyState TopologyState, DateTimeOffset? QualifiedAt, long OrderingOrdinal,
        AQGreenLegacyAcquisitionProvenance Provenance, AQGreenLegacySponsorEvidenceStatus SponsorStatus,
        AQGreenLegacyRootEvidenceStatus RootStatus, bool RootCandidate, bool Eligible, List<AnalysisAnomaly> Anomalies);
    private sealed record AnalysisComponent(string Digest, string? RootKey, List<string> RootCandidates, List<string> Keys,
        bool Accepted, List<ComponentAnomaly> Anomalies);
}
