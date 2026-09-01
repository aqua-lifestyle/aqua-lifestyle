using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Captures the cutoff-specific B4 inputs required by a Placement V2 weekly
    /// commission decision. The returned manifest is persisted by B5.4; it is not
    /// a graduation decision or a current-state projection.
    /// </summary>
    public interface IAQGreenCommissionStructuralEvidenceEvaluator
    {
        Task<AQGreenCommissionStructuralEvidenceResult> EvaluateAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenCommissionStructuralEvidenceResult
    {
        public AQGreenCommissionStructuralEvidenceResult(
            Guid participantId,
            Guid placementTreeScopeId,
            DateTime cutoff,
            AQGreenStructuralCompletionLevel structuralCompletionLevel,
            int qualifyingDepth1Count,
            int qualifyingDepth2Count,
            int qualifyingDepth3Count,
            string placementRulesVersion,
            string structuralQualificationRulesVersion,
            IReadOnlyList<AQGreenCommissionStructuralEvidenceObservation> observations)
        {
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));
            if (cutoff == default || cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC commission cutoff is required.",
                    nameof(cutoff));
            if (structuralCompletionLevel < AQGreenStructuralCompletionLevel.Level0 ||
                structuralCompletionLevel > AQGreenStructuralCompletionLevel.Level3)
                throw new ArgumentOutOfRangeException(nameof(structuralCompletionLevel));

            ParticipantId = participantId;
            PlacementTreeScopeId = placementTreeScopeId;
            Cutoff = cutoff;
            StructuralCompletionLevel = structuralCompletionLevel;
            QualifyingDepth1Count = qualifyingDepth1Count;
            QualifyingDepth2Count = qualifyingDepth2Count;
            QualifyingDepth3Count = qualifyingDepth3Count;
            PlacementRulesVersion = NormalizeVersion(
                placementRulesVersion,
                nameof(placementRulesVersion));
            StructuralQualificationRulesVersion = NormalizeVersion(
                structuralQualificationRulesVersion,
                nameof(structuralQualificationRulesVersion));
            Observations = (observations ??
                    throw new ArgumentNullException(nameof(observations)))
                .ToList()
                .AsReadOnly();

            _ = new AQGreenStructuralCompletionResult(
                participantId,
                placementTreeScopeId,
                structuralCompletionLevel,
                qualifyingDepth1Count,
                qualifyingDepth2Count,
                qualifyingDepth3Count,
                cutoff,
                StructuralQualificationRulesVersion);
        }

        public Guid ParticipantId { get; }
        public Guid PlacementTreeScopeId { get; }
        public DateTime Cutoff { get; }
        public AQGreenStructuralCompletionLevel StructuralCompletionLevel { get; }
        public int QualifyingDepth1Count { get; }
        public int QualifyingDepth2Count { get; }
        public int QualifyingDepth3Count { get; }
        public string PlacementRulesVersion { get; }
        public string StructuralQualificationRulesVersion { get; }
        public IReadOnlyList<AQGreenCommissionStructuralEvidenceObservation> Observations { get; }

        private static string NormalizeVersion(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException(
                    "A rules version is required.",
                    parameterName)
                : value.Trim();
    }

    public sealed class AQGreenCommissionStructuralEvidenceObservation
    {
        public int CanonicalOrdinal { get; init; }
        public Guid SourcePlacementId { get; init; }
        public EntryParticipationStatus ParticipationStatusObserved { get; init; }
        public DateTime? ParticipationActivatedAtObserved { get; init; }
        public bool ParticipationIsDeletedObserved { get; init; }
        public int CustomerIdObserved { get; init; }
        public bool CustomerTenantMatchedObserved { get; init; }
        public bool CustomerIsActiveObserved { get; init; }
        public bool CustomerIsDeletedObserved { get; init; }
        public long UserIdObserved { get; init; }
        public bool UserTenantMatchedObserved { get; init; }
        public bool UserIsActiveObserved { get; init; }
        public bool UserIsDeletedObserved { get; init; }
    }
}
