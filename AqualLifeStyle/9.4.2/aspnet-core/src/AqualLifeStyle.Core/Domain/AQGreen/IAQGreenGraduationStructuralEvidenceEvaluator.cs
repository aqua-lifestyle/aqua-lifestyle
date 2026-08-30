using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenGraduationStructuralEvidenceEvaluator
    {
        Task<AQGreenGraduationStructuralEvidenceResult> EvaluateAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenGraduationStructuralEvidenceResult
    {
        public AQGreenGraduationStructuralEvidenceResult(
            Guid participantId,
            Guid placementTreeScopeId,
            DateTime cutoff,
            AQGreenStructuralCompletionLevel structuralCompletionLevel,
            int qualifyingDepth1Count,
            int qualifyingDepth2Count,
            string structuralQualificationRulesVersion,
            IReadOnlyList<AQGreenGraduationStructuralEvidenceObservation> observations)
        {
            ParticipantId = participantId;
            PlacementTreeScopeId = placementTreeScopeId;
            Cutoff = cutoff;
            StructuralCompletionLevel = structuralCompletionLevel;
            QualifyingDepth1Count = qualifyingDepth1Count;
            QualifyingDepth2Count = qualifyingDepth2Count;
            StructuralQualificationRulesVersion =
                string.IsNullOrWhiteSpace(structuralQualificationRulesVersion)
                    ? throw new ArgumentException(
                        "A structural qualification rules version is required.",
                        nameof(structuralQualificationRulesVersion))
                    : structuralQualificationRulesVersion.Trim();
            Observations = (observations ??
                throw new ArgumentNullException(nameof(observations)))
                .ToList()
                .AsReadOnly();
        }

        public Guid ParticipantId { get; }
        public Guid PlacementTreeScopeId { get; }
        public DateTime Cutoff { get; }
        public AQGreenStructuralCompletionLevel StructuralCompletionLevel { get; }
        public int QualifyingDepth1Count { get; }
        public int QualifyingDepth2Count { get; }
        public string StructuralQualificationRulesVersion { get; }
        public IReadOnlyList<AQGreenGraduationStructuralEvidenceObservation> Observations { get; }
    }

    public sealed class AQGreenGraduationStructuralEvidenceObservation
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
