using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Domain.Onyx
{
    public interface IAQGreenV2GraduationEvidenceReplayValidator
    {
        Task<AQGreenV2GraduationEvidenceReplayResult> ValidateAsync(
            Guid graduationDecisionId,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenV2GraduationEvidenceReplayResult
    {
        public AQGreenV2GraduationEvidenceReplayResult(
            AQGreenStructuralCompletionLevel structuralCompletionLevel,
            int qualifyingDepth1Count,
            int qualifyingDepth2Count,
            int evidenceNodeCount)
        {
            StructuralCompletionLevel = structuralCompletionLevel;
            QualifyingDepth1Count = qualifyingDepth1Count;
            QualifyingDepth2Count = qualifyingDepth2Count;
            EvidenceNodeCount = evidenceNodeCount;
        }

        public AQGreenStructuralCompletionLevel StructuralCompletionLevel { get; }
        public int QualifyingDepth1Count { get; }
        public int QualifyingDepth2Count { get; }
        public int EvidenceNodeCount { get; }
    }

    public static class AQGreenV2GraduationEvidenceReplay
    {
        private const int GraduationMaximumRelativeDepth = 2;

        public static AQGreenV2GraduationEvidenceReplayResult Validate(
            OnyxGraduationDecision decision,
            AQGreenV2GraduationEvidence evidence,
            IReadOnlyCollection<AQGreenV2GraduationEvidenceNode> nodes,
            IReadOnlyCollection<AQGreenNetworkPlacement> placements)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (evidence == null)
                throw new AQGreenGraduationEvidenceReplayException(
                    "The Placement V2 graduation evidence header is missing.");
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            if (!OnyxGraduationRules.IsSupportedVersion(
                    decision.GraduationRulesVersion))
                throw new AQGreenGraduationEvidenceVersionNotSupportedException(
                    "graduation rules",
                    decision.GraduationRulesVersion);
            if (decision.StructuralModel != AQGreenGraduationStructuralModel.PlacementV2 ||
                decision.EvaluatedNetworkLevel.HasValue ||
                evidence.Id != decision.Id ||
                evidence.TenantId != decision.TenantId ||
                evidence.Cutoff != decision.DecidedAt)
                throw new AQGreenGraduationEvidenceReplayException(
                    "The Placement V2 graduation decision and evidence header conflict.");
            if (!string.Equals(
                    evidence.EvidenceSchemaVersion,
                    AQGreenV2GraduationEvidenceSchema.CurrentVersion,
                    StringComparison.Ordinal))
                throw new AQGreenGraduationEvidenceVersionNotSupportedException(
                    "evidence schema",
                    evidence.EvidenceSchemaVersion);
            if (!string.Equals(
                    evidence.StructuralQualificationRulesVersion,
                    AQGreenStructuralQualificationRules.CurrentVersion,
                    StringComparison.Ordinal))
                throw new AQGreenGraduationEvidenceVersionNotSupportedException(
                    "structural qualification",
                    evidence.StructuralQualificationRulesVersion);

            var orderedNodes = nodes.OrderBy(node => node.CanonicalOrdinal).ToList();
            if (orderedNodes.Count == 0 ||
                orderedNodes.Count != evidence.EvidenceNodeCount ||
                orderedNodes.Select((node, ordinal) => node.CanonicalOrdinal == ordinal)
                    .Any(matches => !matches) ||
                orderedNodes.Any(node =>
                    node.EvidenceId != evidence.Id ||
                    node.TenantId != evidence.TenantId) ||
                orderedNodes.GroupBy(node => node.SourcePlacementId)
                    .Any(group => group.Count() != 1))
                throw new AQGreenGraduationEvidenceReplayException(
                    "The Placement V2 graduation evidence manifest is incomplete or duplicated.");

            var placementById = placements
                .GroupBy(placement => placement.Id)
                .ToDictionary(group => group.Key, group => group.ToList());
            if (placementById.Any(group => group.Value.Count != 1) ||
                orderedNodes.Any(node => !placementById.ContainsKey(node.SourcePlacementId)) ||
                placements.Count != orderedNodes.Count)
                throw new AQGreenGraduationEvidenceReplayException(
                    "An immutable source placement is missing or ambiguous.");

            var anchor = placementById[orderedNodes[0].SourcePlacementId][0];
            if (anchor.TenantId != evidence.TenantId ||
                anchor.ParticipantId != decision.EntryParticipationId ||
                anchor.PlacedAt > evidence.Cutoff)
                throw new AQGreenGraduationEvidenceReplayException(
                    "The evidence anchor does not match the graduation decision.");

            IReadOnlyList<AQGreenBoundedPlacementTopologyNode> topology;
            try
            {
                topology = AQGreenBoundedPlacementTopologyValidator.Validate(
                    evidence.TenantId,
                    anchor.PlacementTreeScopeId,
                    decision.EntryParticipationId,
                    evidence.Cutoff,
                    GraduationMaximumRelativeDepth,
                    placements.Select(MapPlacement).ToList());
            }
            catch (AQGreenPlacementTopologyIntegrityException exception)
            {
                throw new AQGreenGraduationEvidenceReplayException(
                    "The immutable source placements do not reproduce the bounded decision topology.",
                    exception);
            }

            if (!topology.Select(item => item.Placement.Id).SequenceEqual(
                    orderedNodes.Select(node => node.SourcePlacementId)))
                throw new AQGreenGraduationEvidenceReplayException(
                    "The evidence manifest is not in canonical order.");

            var evidenceNodeByPlacement = orderedNodes.ToDictionary(
                node => node.SourcePlacementId);
            var qualificationNodes = topology.Select(topologyNode =>
            {
                var placement = topologyNode.Placement;
                var node = evidenceNodeByPlacement[placement.Id];
                return new AQGreenStructuralQualificationNode
                {
                    SourcePlacementId = placement.Id,
                    ParticipantId = placement.ParticipantId,
                    RelativeDepth = topologyNode.RelativeDepth,
                    ParticipationTenantId = evidence.TenantId,
                    ParticipationStatus = node.ParticipationStatusObserved,
                    ParticipationActivatedAt = node.ParticipationActivatedAtObserved,
                    ParticipationIsDeleted = node.ParticipationIsDeletedObserved,
                    CustomerId = node.CustomerIdObserved,
                    CustomerTenantId = node.CustomerTenantMatchedObserved
                        ? evidence.TenantId
                        : null,
                    CustomerIsActive = node.CustomerIsActiveObserved,
                    CustomerIsDeleted = node.CustomerIsDeletedObserved,
                    UserId = node.UserIdObserved,
                    UserTenantId = node.UserTenantMatchedObserved
                        ? evidence.TenantId
                        : null,
                    UserIsActive = node.UserIsActiveObserved,
                    UserIsDeleted = node.UserIsDeletedObserved
                };
            }).ToList();

            AQGreenStructuralQualificationOutcome outcome;
            try
            {
                outcome = AQGreenStructuralQualificationRules.Evaluate(
                    evidence.TenantId,
                    evidence.Cutoff,
                    GraduationMaximumRelativeDepth,
                    qualificationNodes);
            }
            catch (Exception exception) when (
                exception is AQGreenPlacementTopologyIntegrityException ||
                exception is AQGreenStructuralContributionPolicyRequiredException)
            {
                throw new AQGreenGraduationEvidenceReplayException(
                    "The persisted lifecycle/security observations no longer prove the recorded decision.",
                    exception);
            }

            if (outcome.StructuralCompletionLevel !=
                    evidence.EvaluatedStructuralCompletionLevel ||
                outcome.QualifyingDepthCounts[1] != evidence.QualifyingDepth1Count ||
                outcome.QualifyingDepthCounts[2] != evidence.QualifyingDepth2Count ||
                outcome.StructuralCompletionLevel < AQGreenStructuralCompletionLevel.Level2)
                throw new AQGreenGraduationEvidenceReplayException(
                    "The replayed structural result contradicts the recorded graduation evidence.");
            return new AQGreenV2GraduationEvidenceReplayResult(
                outcome.StructuralCompletionLevel,
                outcome.QualifyingDepthCounts[1],
                outcome.QualifyingDepthCounts[2],
                orderedNodes.Count);
        }

        private static AQGreenImmutablePlacementFact MapPlacement(
            AQGreenNetworkPlacement placement) =>
            new()
            {
                Id = placement.Id,
                TenantId = placement.TenantId,
                PlacementTreeScopeId = placement.PlacementTreeScopeId,
                ParticipantId = placement.ParticipantId,
                PlacementParentParticipantId = placement.PlacementParentParticipantId,
                PlacementSlot = placement.PlacementSlot,
                CanonicalPath = placement.CanonicalPath,
                PlacedAt = placement.PlacedAt,
                RulesVersion = placement.RulesVersion
            };
    }

    public sealed class AQGreenGraduationEvidenceVersionNotSupportedException
        : InvalidOperationException
    {
        public AQGreenGraduationEvidenceVersionNotSupportedException(
            string versionKind,
            string version)
            : base(
                $"The recorded AQGreen graduation {versionKind} version " +
                $"'{version ?? "<missing>"}' is not supported.")
        {
        }
    }

    public sealed class AQGreenGraduationEvidenceReplayException
        : InvalidOperationException
    {
        public AQGreenGraduationEvidenceReplayException(string message)
            : base(message)
        {
        }

        public AQGreenGraduationEvidenceReplayException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
