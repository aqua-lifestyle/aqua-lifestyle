using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Domain.Onyx
{
    public static class AQGreenV2GraduationEvidenceSchema
    {
        public const int MaximumVersionLength = 32;
        public const string CurrentVersion = "AQGreenV2GraduationEvidenceV1";
    }

    public sealed class AQGreenV2GraduationEvidence : Entity<Guid>, IMustHaveTenant
    {
        private readonly List<AQGreenV2GraduationEvidenceNode> _nodes = new();

        public Guid OnyxGraduationDecisionId => Id;
        public int TenantId { get; private set; }
        public DateTime Cutoff { get; private set; }
        public string StructuralQualificationRulesVersion { get; private set; }
        public string EvidenceSchemaVersion { get; private set; }
        public AQGreenStructuralCompletionLevel EvaluatedStructuralCompletionLevel { get; private set; }
        public int QualifyingDepth1Count { get; private set; }
        public int QualifyingDepth2Count { get; private set; }
        public int EvidenceNodeCount { get; private set; }
        public IReadOnlyCollection<AQGreenV2GraduationEvidenceNode> Nodes =>
            _nodes.AsReadOnly();

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenV2GraduationEvidence()
        {
        }

        public static AQGreenV2GraduationEvidence Capture(
            OnyxGraduationDecision decision,
            AQGreenGraduationStructuralEvidenceResult structuralEvidence)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (structuralEvidence == null)
                throw new ArgumentNullException(nameof(structuralEvidence));
            if (decision.StructuralModel != AQGreenGraduationStructuralModel.PlacementV2 ||
                decision.EvaluatedNetworkLevel.HasValue)
                throw new InvalidOperationException(
                    "Only a Placement V2 graduation decision can own V2 evidence.");
            if (structuralEvidence.ParticipantId != decision.EntryParticipationId ||
                structuralEvidence.Cutoff != decision.DecidedAt ||
                structuralEvidence.StructuralCompletionLevel <
                    AQGreenStructuralCompletionLevel.Level2)
                throw new InvalidOperationException(
                    "The V2 evidence does not prove this graduation decision.");
            if (!string.Equals(
                    structuralEvidence.StructuralQualificationRulesVersion,
                    AQGreenStructuralQualificationRules.CurrentVersion,
                    StringComparison.Ordinal))
                throw new AQGreenGraduationEvidenceVersionNotSupportedException(
                    "structural qualification",
                    structuralEvidence.StructuralQualificationRulesVersion);
            var requiredDepth1 =
                AQGreenStructuralCompletionCalculator.GetRequiredPopulation(1);
            var requiredDepth2 =
                AQGreenStructuralCompletionCalculator.GetRequiredPopulation(2);
            var requiredManifestCount = 1 + requiredDepth1 + requiredDepth2;
            if (structuralEvidence.QualifyingDepth1Count != requiredDepth1 ||
                structuralEvidence.QualifyingDepth2Count != requiredDepth2 ||
                structuralEvidence.Observations.Count != requiredManifestCount ||
                structuralEvidence.Observations
                    .Select((observation, ordinal) =>
                        observation.CanonicalOrdinal == ordinal)
                    .Any(matches => !matches) ||
                structuralEvidence.Observations
                    .GroupBy(observation => observation.SourcePlacementId)
                    .Any(group => group.Key == Guid.Empty || group.Count() != 1))
                throw new AQGreenPlacementTopologyIntegrityException(
                    "The V2 graduation evidence manifest is incomplete or duplicated.");

            var evidence = new AQGreenV2GraduationEvidence
            {
                Id = decision.Id,
                TenantId = decision.TenantId,
                Cutoff = structuralEvidence.Cutoff,
                StructuralQualificationRulesVersion =
                    structuralEvidence.StructuralQualificationRulesVersion,
                EvidenceSchemaVersion = AQGreenV2GraduationEvidenceSchema.CurrentVersion,
                EvaluatedStructuralCompletionLevel =
                    structuralEvidence.StructuralCompletionLevel,
                QualifyingDepth1Count = structuralEvidence.QualifyingDepth1Count,
                QualifyingDepth2Count = structuralEvidence.QualifyingDepth2Count,
                EvidenceNodeCount = structuralEvidence.Observations.Count
            };
            evidence._nodes.AddRange(structuralEvidence.Observations.Select(observation =>
                AQGreenV2GraduationEvidenceNode.Capture(
                    decision.TenantId,
                    decision.Id,
                    observation)));
            return evidence;
        }
    }

    public sealed class AQGreenV2GraduationEvidenceNode
    {
        public int TenantId { get; private set; }
        public Guid EvidenceId { get; private set; }
        public int CanonicalOrdinal { get; private set; }
        public Guid SourcePlacementId { get; private set; }
        public EntryParticipationStatus ParticipationStatusObserved { get; private set; }
        public DateTime? ParticipationActivatedAtObserved { get; private set; }
        public bool ParticipationIsDeletedObserved { get; private set; }
        public int CustomerIdObserved { get; private set; }
        public bool CustomerTenantMatchedObserved { get; private set; }
        public bool CustomerIsActiveObserved { get; private set; }
        public bool CustomerIsDeletedObserved { get; private set; }
        public long UserIdObserved { get; private set; }
        public bool UserTenantMatchedObserved { get; private set; }
        public bool UserIsActiveObserved { get; private set; }
        public bool UserIsDeletedObserved { get; private set; }

        private AQGreenV2GraduationEvidenceNode()
        {
        }

        internal static AQGreenV2GraduationEvidenceNode Capture(
            int tenantId,
            Guid evidenceId,
            AQGreenGraduationStructuralEvidenceObservation observation)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (evidenceId == Guid.Empty)
                throw new ArgumentException(
                    "A graduation evidence identity is required.",
                    nameof(evidenceId));
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (observation.CanonicalOrdinal < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(observation.CanonicalOrdinal));
            if (observation.SourcePlacementId == Guid.Empty)
                throw new ArgumentException(
                    "An immutable source placement is required.",
                    nameof(observation));
            if (observation.CustomerIdObserved <= 0 || observation.UserIdObserved <= 0)
                throw new AQGreenPlacementTopologyIntegrityException(
                    "V2 graduation evidence requires the observed Customer/User binding.");

            return new AQGreenV2GraduationEvidenceNode
            {
                TenantId = tenantId,
                EvidenceId = evidenceId,
                CanonicalOrdinal = observation.CanonicalOrdinal,
                SourcePlacementId = observation.SourcePlacementId,
                ParticipationStatusObserved = observation.ParticipationStatusObserved,
                ParticipationActivatedAtObserved =
                    observation.ParticipationActivatedAtObserved,
                ParticipationIsDeletedObserved =
                    observation.ParticipationIsDeletedObserved,
                CustomerIdObserved = observation.CustomerIdObserved,
                CustomerTenantMatchedObserved =
                    observation.CustomerTenantMatchedObserved,
                CustomerIsActiveObserved = observation.CustomerIsActiveObserved,
                CustomerIsDeletedObserved = observation.CustomerIsDeletedObserved,
                UserIdObserved = observation.UserIdObserved,
                UserTenantMatchedObserved = observation.UserTenantMatchedObserved,
                UserIsActiveObserved = observation.UserIsActiveObserved,
                UserIsDeletedObserved = observation.UserIsDeletedObserved
            };
        }
    }
}
