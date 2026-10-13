using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace AqualLifeStyle.Domain.AQGreen
{
    public static class AQGreenLegacyCanonicalisationRules
    {
        public const string SimulationVersion = "AQGreenLegacyCanonicalisationSimulationV2";
        public const string ComparatorVersion = "LegacyOrderingTieBreakOrdinalV1";
        public const string PlacementSource = "LegacyCanonicalisation";
    }

    public enum AQGreenLegacyCanonicalisationAnomalyCode
    {
        AmbiguousRoot = 1,
        MissingRootEvidence = 2,
        CrossTenant = 3,
        DuplicateParticipation = 4,
        MissingSponsor = 5,
        DanglingSponsor = 6,
        Cycle = 7,
        MissingOrderingEvidence = 8,
        SponsorEvidenceUncertainty = 9,
        MissingSourceEvidenceDigest = 10,
        InvalidScope = 11,
        InvalidStructuralEvidence = 12,
        MissingOrderingTieBreakOrdinal = 13,
        DuplicateOrderingTieBreakOrdinal = 14,
        UnsupportedAcquisitionProvenance = 15
    }

    public enum AQGreenLegacyAcquisitionProvenance
    {
        Known = 1,
        LegacyUnknownAcquisition = 2
    }

    public enum AQGreenLegacySponsorEvidenceStatus
    {
        AcceptedSponsor = 1,
        MissingSponsorEvidence = 2,
        AmbiguousSponsor = 3,
        RejectedSponsor = 4,
        NotApplicableToRoot = 5
    }

    public enum AQGreenLegacyRootEvidenceStatus
    {
        NotRoot = 1,
        RootCandidate = 2,
        AcceptedRoot = 3,
        RejectedRoot = 4,
        AmbiguousRoot = 5,
        MissingRootEvidence = 6
    }

    public enum AQGreenLegacyStructuralImpactClassification
    {
        Same = 1,
        Changed = 2,
        Ambiguous = 3
    }

    /// <summary>
    /// Frozen, already-classified candidate input. This type deliberately does not
    /// decide D03B or D09 population policy.
    /// </summary>
    public sealed class AQGreenLegacyCanonicalisationCandidate
    {
        public int TenantSnapshotKey { get; init; }
        public Guid ParticipantSnapshotKey { get; init; }
        public Guid? AcceptedLegacySponsorSnapshotKey { get; init; }
        public DateTime? QualifiedUnderRecruiterAtUtc { get; init; }
        public long? LegacyOrderingTieBreakOrdinal { get; init; }
        public AQGreenLegacySponsorEvidenceStatus SponsorEvidenceStatus { get; init; }
        public AQGreenLegacyRootEvidenceStatus RootEvidenceStatus { get; init; }
        public AQGreenLegacyAcquisitionProvenance AcquisitionProvenance { get; init; }
        public AQGreenStructuralCompletionLevel? EffectiveV1StructuralLevel { get; init; }
        public string SourceEvidenceDigest { get; init; }
    }

    public sealed class AQGreenLegacyCanonicalisationRequest
    {
        public int TenantSnapshotKey { get; init; }
        public Guid PlacementTreeScopeSnapshotKey { get; init; }
        public DateTime AnalysisSnapshotCutoffUtc { get; init; }
        public IReadOnlyCollection<AQGreenLegacyCanonicalisationCandidate> Candidates { get; init; }
    }

    public sealed class AQGreenLegacyCanonicalisationAnomaly
    {
        public AQGreenLegacyCanonicalisationAnomaly(
            AQGreenLegacyCanonicalisationAnomalyCode code,
            Guid? participantSnapshotKey,
            string reason)
        {
            Code = code;
            ParticipantSnapshotKey = participantSnapshotKey;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public AQGreenLegacyCanonicalisationAnomalyCode Code { get; }
        public Guid? ParticipantSnapshotKey { get; }
        public string Reason { get; }
    }

    public sealed class AQGreenLegacyCanonicalPlacementProposal
    {
        public int TenantSnapshotKey { get; init; }
        public Guid PlacementTreeScopeSnapshotKey { get; init; }
        public Guid ParticipantSnapshotKey { get; init; }
        public Guid? LegacyCreditedSponsorSnapshotKey { get; init; }
        public long LegacyOrderingTieBreakOrdinal { get; init; }
        public long CanonicalInputOrdinal { get; init; }
        public Guid? PlacementParentSnapshotKey { get; init; }
        public int? PlacementSlot { get; init; }
        public string CanonicalPath { get; init; }
        public BigInteger CanonicalPosition { get; init; }
        public long PlacementSequence { get; init; }
        public string PlacementSource { get; init; }
        public string RulesVersion { get; init; }
        public string SimulationVersion { get; init; }
        public string ComparatorVersion { get; init; }
        public string SourceEvidenceDigest { get; init; }
    }

    public sealed class AQGreenLegacyCanonicalisationResult
    {
        public AQGreenLegacyCanonicalisationResult(
            IReadOnlyList<AQGreenLegacyCanonicalPlacementProposal> placements,
            IReadOnlyList<AQGreenLegacyCanonicalisationAnomaly> anomalies,
            IReadOnlyList<AQGreenLegacyStructuralImpact> structuralImpacts,
            string manifestDigest)
        {
            Placements = placements;
            Anomalies = anomalies;
            StructuralImpacts = structuralImpacts;
            ManifestDigest = manifestDigest;
        }

        public IReadOnlyList<AQGreenLegacyCanonicalPlacementProposal> Placements { get; }
        public IReadOnlyList<AQGreenLegacyCanonicalisationAnomaly> Anomalies { get; }
        public IReadOnlyList<AQGreenLegacyStructuralImpact> StructuralImpacts { get; }
        public string ManifestDigest { get; }
        public bool IsCanonicalisable => Anomalies.Count == 0;
    }

    public sealed class AQGreenLegacyStructuralImpact
    {
        public Guid ParticipantSnapshotKey { get; init; }
        public long LegacyOrderingTieBreakOrdinal { get; init; }
        public AQGreenStructuralCompletionLevel? EffectiveV1StructuralLevel { get; init; }
        public AQGreenStructuralCompletionLevel SimulatedV2StructuralLevel { get; init; }
        public AQGreenLegacyStructuralImpactClassification Classification { get; init; }
    }

    /// <summary>
    /// Read-only deterministic simulation of a single accepted legacy scope.
    /// It has no persistence dependency and emits data-minimised technical evidence
    /// without direct customer identifiers.
    /// </summary>
    public static class AQGreenLegacyCanonicalisationSimulator
    {
        public static AQGreenLegacyCanonicalisationResult Simulate(
            AQGreenLegacyCanonicalisationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var candidates = request.Candidates?.ToList() ?? new List<AQGreenLegacyCanonicalisationCandidate>();
            var anomalies = Validate(request, candidates);
            if (anomalies.Count > 0)
                return new AQGreenLegacyCanonicalisationResult(
                    Array.Empty<AQGreenLegacyCanonicalPlacementProposal>(),
                    anomalies.AsReadOnly(),
                    Array.Empty<AQGreenLegacyStructuralImpact>(),
                    null);

            var root = candidates.Single(candidate =>
                candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.AcceptedRoot);
            var placements = new List<AQGreenLegacyCanonicalPlacementProposal>();
            var placedByParticipant = new Dictionary<Guid, AQGreenLegacyCanonicalPlacementProposal>();
            var childrenBySponsor = candidates
                .Where(candidate =>
                    candidate.RootEvidenceStatus != AQGreenLegacyRootEvidenceStatus.AcceptedRoot)
                .GroupBy(candidate => candidate.AcceptedLegacySponsorSnapshotKey.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
            var ready = new SortedSet<AQGreenLegacyCanonicalisationCandidate>(
                LegacyReplayKeyComparer.Instance);
            long inputOrdinal = 0;
            long sequence = 1;

            var rootProposal = CreateRootProposal(request, root, inputOrdinal++, sequence++);
            placements.Add(rootProposal);
            placedByParticipant.Add(root.ParticipantSnapshotKey, rootProposal);
            EnqueueChildren(root.ParticipantSnapshotKey, childrenBySponsor, ready);

            while (ready.Count > 0)
            {
                var candidate = ready.Min;
                ready.Remove(candidate);
                var sponsor = placedByParticipant[candidate.AcceptedLegacySponsorSnapshotKey.Value];
                var sponsorSubtree = placements
                    .Where(placement =>
                        placement.CanonicalPath.StartsWith(
                            sponsor.CanonicalPath,
                            StringComparison.Ordinal))
                    .OrderBy(placement => placement.CanonicalPath.Length)
                    .ThenBy(placement => placement.CanonicalPath, StringComparer.Ordinal)
                    .Select(placement => new AQGreenPlacementVacancyNode(
                        placement.ParticipantSnapshotKey,
                        placement.PlacementParentSnapshotKey,
                        placement.PlacementSlot))
                    .ToList();
                var vacancy = AQGreenSponsorLocalVacancySelector.FindFirstVacancy(sponsorSubtree);
                var parent = placedByParticipant[vacancy.ParentParticipantId];
                var proposal = CreateChildProposal(
                    request,
                    candidate,
                    parent,
                    vacancy.PlacementSlot,
                    inputOrdinal++,
                    sequence++);
                placements.Add(proposal);
                placedByParticipant.Add(candidate.ParticipantSnapshotKey, proposal);
                EnqueueChildren(candidate.ParticipantSnapshotKey, childrenBySponsor, ready);
            }

            if (placements.Count != candidates.Count)
            {
                var unplaced = candidates
                    .Where(candidate => !placedByParticipant.ContainsKey(candidate.ParticipantSnapshotKey))
                    .OrderBy(candidate => candidate.LegacyOrderingTieBreakOrdinal)
                    .Select(candidate => new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.Cycle,
                        candidate.ParticipantSnapshotKey,
                        "The accepted sponsor graph cannot be replayed from its root."))
                    .ToList();
                return new AQGreenLegacyCanonicalisationResult(
                    Array.Empty<AQGreenLegacyCanonicalPlacementProposal>(),
                    unplaced.AsReadOnly(),
                    Array.Empty<AQGreenLegacyStructuralImpact>(),
                    null);
            }

            var ordered = placements.OrderBy(placement => placement.PlacementSequence).ToList();
            return new AQGreenLegacyCanonicalisationResult(
                ordered.AsReadOnly(),
                Array.Empty<AQGreenLegacyCanonicalisationAnomaly>(),
                CalculateStructuralImpacts(candidates, ordered),
                ComputeManifestDigest(request, ordered));
        }

        private static IReadOnlyList<AQGreenLegacyStructuralImpact> CalculateStructuralImpacts(
            IReadOnlyCollection<AQGreenLegacyCanonicalisationCandidate> candidates,
            IReadOnlyCollection<AQGreenLegacyCanonicalPlacementProposal> placements)
        {
            var candidateById = candidates.ToDictionary(candidate => candidate.ParticipantSnapshotKey);
            return placements
                .Select(anchor =>
                {
                    var simulatedLevel = AQGreenStructuralCompletionCalculator.Evaluate(
                        relativeDepth => placements.Count(placement =>
                            placement.CanonicalPath.StartsWith(
                                anchor.CanonicalPath,
                                StringComparison.Ordinal) &&
                            placement.CanonicalPath.Length ==
                                anchor.CanonicalPath.Length + relativeDepth));
                    var candidate = candidateById[anchor.ParticipantSnapshotKey];
                    var v1Level = candidate
                        .EffectiveV1StructuralLevel;
                    return new AQGreenLegacyStructuralImpact
                    {
                        ParticipantSnapshotKey = anchor.ParticipantSnapshotKey,
                        LegacyOrderingTieBreakOrdinal = candidate.LegacyOrderingTieBreakOrdinal.Value,
                        EffectiveV1StructuralLevel = v1Level,
                        SimulatedV2StructuralLevel = simulatedLevel,
                        Classification = !v1Level.HasValue
                            ? AQGreenLegacyStructuralImpactClassification.Ambiguous
                            : v1Level.Value == simulatedLevel
                                ? AQGreenLegacyStructuralImpactClassification.Same
                                : AQGreenLegacyStructuralImpactClassification.Changed
                    };
                })
                .OrderBy(impact => impact.LegacyOrderingTieBreakOrdinal)
                .ToList()
                .AsReadOnly();
        }

        private static List<AQGreenLegacyCanonicalisationAnomaly> Validate(
            AQGreenLegacyCanonicalisationRequest request,
            IReadOnlyCollection<AQGreenLegacyCanonicalisationCandidate> candidates)
        {
            var anomalies = new List<AQGreenLegacyCanonicalisationAnomaly>();
            if (request.TenantSnapshotKey <= 0 ||
                request.PlacementTreeScopeSnapshotKey == Guid.Empty ||
                request.AnalysisSnapshotCutoffUtc == default ||
                request.AnalysisSnapshotCutoffUtc.Kind != DateTimeKind.Utc)
            {
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.InvalidScope,
                    null,
                    "The accepted scope requires snapshot Tenant/scope identities and a UTC analysis cutoff."));
            }

            var roots = candidates.Where(candidate =>
                candidate?.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.AcceptedRoot).ToList();
            if (roots.Count == 0)
            {
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.MissingRootEvidence,
                    null,
                    "The accepted scope has no root evidence."));
            }
            else if (roots.Count > 1)
            {
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot,
                    null,
                    "The accepted scope has more than one explicitly accepted root."));
            }

            foreach (var nullCandidate in candidates.Where(candidate => candidate == null))
            {
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.DuplicateParticipation,
                    null,
                    "The accepted population contains a missing candidate row."));
            }

            var nonNull = candidates.Where(candidate => candidate != null).ToList();
            foreach (var duplicate in nonNull
                         .GroupBy(candidate => candidate.ParticipantSnapshotKey)
                         .Where(group => group.Key == Guid.Empty || group.Count() > 1))
            {
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.DuplicateParticipation,
                    duplicate.Key == Guid.Empty ? null : duplicate.Key,
                    "The accepted population contains a missing or duplicate participation identity."));
            }

            var byId = nonNull
                .Where(candidate => candidate.ParticipantSnapshotKey != Guid.Empty)
                .GroupBy(candidate => candidate.ParticipantSnapshotKey)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
            foreach (var candidate in nonNull)
            {
                if (candidate.TenantSnapshotKey != request.TenantSnapshotKey)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.CrossTenant,
                        candidate.ParticipantSnapshotKey,
                        "The candidate is outside the accepted Tenant boundary."));
                if (candidate.AcquisitionProvenance !=
                         AQGreenLegacyAcquisitionProvenance.Known &&
                         candidate.AcquisitionProvenance !=
                         AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.UnsupportedAcquisitionProvenance,
                        candidate.ParticipantSnapshotKey,
                        "The acquisition-provenance classification is unsupported."));
                if (!Enum.IsDefined(candidate.RootEvidenceStatus))
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot,
                        candidate.ParticipantSnapshotKey,
                        "The root-evidence classification is unsupported."));
                if (!Enum.IsDefined(candidate.SponsorEvidenceStatus))
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty,
                        candidate.ParticipantSnapshotKey,
                        "The sponsor-evidence classification is unsupported."));
                if (!candidate.LegacyOrderingTieBreakOrdinal.HasValue ||
                    candidate.LegacyOrderingTieBreakOrdinal.Value <= 0)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.MissingOrderingTieBreakOrdinal,
                        candidate.ParticipantSnapshotKey,
                        "The candidate lacks its extraction-derived legacy ordering ordinal."));
                if (!IsSha256Digest(candidate.SourceEvidenceDigest))
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.MissingSourceEvidenceDigest,
                        candidate.ParticipantSnapshotKey,
                        "The candidate requires a 64-character hexadecimal SHA-256 source evidence digest."));
                if (candidate.EffectiveV1StructuralLevel.HasValue &&
                    (candidate.EffectiveV1StructuralLevel.Value <
                        AQGreenStructuralCompletionLevel.Level0 ||
                     candidate.EffectiveV1StructuralLevel.Value >
                        AQGreenStructuralCompletionLevel.Level3))
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.InvalidStructuralEvidence,
                        candidate.ParticipantSnapshotKey,
                        "The candidate contains an unsupported V1 structural level."));

                if (candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.RootCandidate ||
                    candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.MissingRootEvidence)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.MissingRootEvidence,
                        candidate.ParticipantSnapshotKey,
                        "A recruiterless root candidate lacks explicit reviewed acceptance evidence."));
                else if (candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.RejectedRoot ||
                         candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.AmbiguousRoot)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot,
                        candidate.ParticipantSnapshotKey,
                        "Root review did not accept this candidate."));

                if (candidate.RootEvidenceStatus == AQGreenLegacyRootEvidenceStatus.AcceptedRoot)
                {
                    if (candidate.AcceptedLegacySponsorSnapshotKey.HasValue)
                        anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                            AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot,
                            candidate.ParticipantSnapshotKey,
                            "An accepted root also has accepted sponsor evidence."));
                    if (candidate.SponsorEvidenceStatus !=
                        AQGreenLegacySponsorEvidenceStatus.NotApplicableToRoot)
                        anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                            AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty,
                            candidate.ParticipantSnapshotKey,
                            "An accepted root must mark sponsor evidence as not applicable."));
                    continue;
                }

                if (candidate.RootEvidenceStatus != AQGreenLegacyRootEvidenceStatus.NotRoot)
                    continue;

                if (!candidate.QualifiedUnderRecruiterAtUtc.HasValue ||
                    candidate.QualifiedUnderRecruiterAtUtc.Value == default ||
                    candidate.QualifiedUnderRecruiterAtUtc.Value.Kind != DateTimeKind.Utc)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.MissingOrderingEvidence,
                        candidate.ParticipantSnapshotKey,
                        "The candidate lacks a normalized authoritative UTC recruiter-ordering time."));
                if (candidate.SponsorEvidenceStatus !=
                    AQGreenLegacySponsorEvidenceStatus.AcceptedSponsor)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty,
                        candidate.ParticipantSnapshotKey,
                        "The non-root candidate lacks explicitly accepted sponsor evidence."));
                if (!candidate.AcceptedLegacySponsorSnapshotKey.HasValue)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.MissingSponsor,
                        candidate.ParticipantSnapshotKey,
                        "A non-root candidate has no accepted legacy sponsor."));
                else if (candidate.AcceptedLegacySponsorSnapshotKey.Value == candidate.ParticipantSnapshotKey)
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.Cycle,
                        candidate.ParticipantSnapshotKey,
                        "A candidate sponsors itself."));
                else if (!byId.ContainsKey(candidate.AcceptedLegacySponsorSnapshotKey.Value))
                    anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                        AQGreenLegacyCanonicalisationAnomalyCode.DanglingSponsor,
                        candidate.ParticipantSnapshotKey,
                        "The accepted legacy sponsor is outside the accepted scope population."));
            }

            foreach (var duplicateOrdinal in nonNull
                         .Where(candidate => candidate.LegacyOrderingTieBreakOrdinal.HasValue)
                         .GroupBy(candidate => candidate.LegacyOrderingTieBreakOrdinal.Value)
                         .Where(group => group.Count() > 1))
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.DuplicateOrderingTieBreakOrdinal,
                    null,
                    "The accepted population contains a duplicate legacy ordering ordinal."));

            if (anomalies.Count == 0 && ContainsCycle(byId))
                anomalies.Add(new AQGreenLegacyCanonicalisationAnomaly(
                    AQGreenLegacyCanonicalisationAnomalyCode.Cycle,
                    null,
                    "The accepted legacy sponsor graph contains a cycle."));

            return anomalies
                .OrderBy(anomaly => anomaly.Code)
                .ThenBy(anomaly => anomaly.ParticipantSnapshotKey, NullableSnapshotGuidComparer.Instance)
                .ToList();
        }

        private static bool IsSha256Digest(string value) =>
            value != null &&
            value.Length == 64 &&
            value.All(character =>
                character is >= '0' and <= '9' or
                    >= 'a' and <= 'f' or
                    >= 'A' and <= 'F');

        private static bool ContainsCycle(
            IReadOnlyDictionary<Guid, AQGreenLegacyCanonicalisationCandidate> byId)
        {
            foreach (var candidate in byId.Values)
            {
                var path = new HashSet<Guid>();
                var current = candidate;
                while (current.AcceptedLegacySponsorSnapshotKey.HasValue &&
                       byId.TryGetValue(current.AcceptedLegacySponsorSnapshotKey.Value, out current))
                {
                    if (!path.Add(current.ParticipantSnapshotKey)) return true;
                }
            }

            return false;
        }

        private static void EnqueueChildren(
            Guid sponsorParticipantId,
            IReadOnlyDictionary<Guid, List<AQGreenLegacyCanonicalisationCandidate>> childrenBySponsor,
            ISet<AQGreenLegacyCanonicalisationCandidate> ready)
        {
            if (!childrenBySponsor.TryGetValue(sponsorParticipantId, out var children)) return;
            foreach (var child in children) ready.Add(child);
        }

        private static AQGreenLegacyCanonicalPlacementProposal CreateRootProposal(
            AQGreenLegacyCanonicalisationRequest request,
            AQGreenLegacyCanonicalisationCandidate root,
            long inputOrdinal,
            long sequence) =>
            new()
            {
                TenantSnapshotKey = request.TenantSnapshotKey,
                PlacementTreeScopeSnapshotKey = request.PlacementTreeScopeSnapshotKey,
                ParticipantSnapshotKey = root.ParticipantSnapshotKey,
                LegacyOrderingTieBreakOrdinal = root.LegacyOrderingTieBreakOrdinal.Value,
                CanonicalInputOrdinal = inputOrdinal,
                CanonicalPath = string.Empty,
                CanonicalPosition = BigInteger.One,
                PlacementSequence = sequence,
                PlacementSource = AQGreenLegacyCanonicalisationRules.PlacementSource,
                RulesVersion = AQGreenPlacementRules.CurrentVersion,
                SimulationVersion = AQGreenLegacyCanonicalisationRules.SimulationVersion,
                ComparatorVersion = AQGreenLegacyCanonicalisationRules.ComparatorVersion,
                SourceEvidenceDigest = root.SourceEvidenceDigest
            };

        private static AQGreenLegacyCanonicalPlacementProposal CreateChildProposal(
            AQGreenLegacyCanonicalisationRequest request,
            AQGreenLegacyCanonicalisationCandidate candidate,
            AQGreenLegacyCanonicalPlacementProposal parent,
            int slot,
            long inputOrdinal,
            long sequence) =>
            new()
            {
                TenantSnapshotKey = request.TenantSnapshotKey,
                PlacementTreeScopeSnapshotKey = request.PlacementTreeScopeSnapshotKey,
                ParticipantSnapshotKey = candidate.ParticipantSnapshotKey,
                LegacyCreditedSponsorSnapshotKey = candidate.AcceptedLegacySponsorSnapshotKey,
                LegacyOrderingTieBreakOrdinal = candidate.LegacyOrderingTieBreakOrdinal.Value,
                CanonicalInputOrdinal = inputOrdinal,
                PlacementParentSnapshotKey = parent.ParticipantSnapshotKey,
                PlacementSlot = slot,
                CanonicalPath = parent.CanonicalPath + slot.ToString(CultureInfo.InvariantCulture),
                CanonicalPosition = 5 * parent.CanonicalPosition - 4 + slot,
                PlacementSequence = sequence,
                PlacementSource = AQGreenLegacyCanonicalisationRules.PlacementSource,
                RulesVersion = AQGreenPlacementRules.CurrentVersion,
                SimulationVersion = AQGreenLegacyCanonicalisationRules.SimulationVersion,
                ComparatorVersion = AQGreenLegacyCanonicalisationRules.ComparatorVersion,
                SourceEvidenceDigest = candidate.SourceEvidenceDigest
            };

        private static string ComputeManifestDigest(
            AQGreenLegacyCanonicalisationRequest request,
            IEnumerable<AQGreenLegacyCanonicalPlacementProposal> placements)
        {
            var lines = new StringBuilder()
                .Append(request.TenantSnapshotKey).Append('|')
                .Append(request.PlacementTreeScopeSnapshotKey.ToString("D")).Append('|')
                .Append(request.AnalysisSnapshotCutoffUtc.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(AQGreenLegacyCanonicalisationRules.SimulationVersion).Append('|')
                .Append(AQGreenLegacyCanonicalisationRules.ComparatorVersion).AppendLine();
            foreach (var placement in placements)
            {
                lines.Append(placement.PlacementSequence).Append('|')
                    .Append(placement.CanonicalInputOrdinal).Append('|')
                    .Append(placement.ParticipantSnapshotKey.ToString("D")).Append('|')
                    .Append(placement.LegacyCreditedSponsorSnapshotKey?.ToString("D") ?? "-").Append('|')
                    .Append(placement.LegacyOrderingTieBreakOrdinal).Append('|')
                    .Append(placement.PlacementParentSnapshotKey?.ToString("D") ?? "-").Append('|')
                    .Append(placement.PlacementSlot?.ToString(CultureInfo.InvariantCulture) ?? "-").Append('|')
                    .Append(placement.CanonicalPath).Append('|')
                    .Append(placement.CanonicalPosition.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(placement.SourceEvidenceDigest).AppendLine();
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString())))
                .ToLowerInvariant();
        }

        private sealed class LegacyReplayKeyComparer
            : IComparer<AQGreenLegacyCanonicalisationCandidate>
        {
            public static readonly LegacyReplayKeyComparer Instance = new();

            public int Compare(
                AQGreenLegacyCanonicalisationCandidate left,
                AQGreenLegacyCanonicalisationCandidate right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return -1;
                if (right == null) return 1;
                var time = left.QualifiedUnderRecruiterAtUtc.Value.CompareTo(
                    right.QualifiedUnderRecruiterAtUtc.Value);
                return time != 0
                    ? time
                    : left.LegacyOrderingTieBreakOrdinal.Value.CompareTo(
                        right.LegacyOrderingTieBreakOrdinal.Value);
            }
        }

        private sealed class SnapshotGuidComparer : IComparer<Guid>
        {
            public static readonly SnapshotGuidComparer Instance = new();

            public int Compare(Guid left, Guid right) =>
                string.CompareOrdinal(left.ToString("N"), right.ToString("N"));
        }

        private sealed class NullableSnapshotGuidComparer : IComparer<Guid?>
        {
            public static readonly NullableSnapshotGuidComparer Instance = new();

            public int Compare(Guid? left, Guid? right)
            {
                if (!left.HasValue) return right.HasValue ? -1 : 0;
                if (!right.HasValue) return 1;
                return SnapshotGuidComparer.Instance.Compare(left.Value, right.Value);
            }
        }
    }
}
