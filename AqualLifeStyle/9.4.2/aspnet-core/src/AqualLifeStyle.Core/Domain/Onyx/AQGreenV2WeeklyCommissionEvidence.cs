using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum AQGreenWeeklySalesApplicability
    {
        NotApplicable = 1,
        Applicable = 2
    }

    public static class AQGreenV2WeeklyCommissionEvidenceSchema
    {
        public const int MaximumVersionLength = 40;
        public const string CurrentVersion = "AQGreenV2WeeklyCommissionEvidenceV1";
    }

    /// <summary>
    /// Immutable Placement V2 decision evidence. Payout lifecycle mutations remain
    /// on EntryWeeklyCommission and cannot rewrite these calculation facts.
    /// </summary>
    public sealed class AQGreenV2WeeklyCommissionEvidence
        : Entity<Guid>, IMustHaveTenant
    {
        private readonly List<AQGreenV2WeeklyCommissionEvidenceNode> _nodes = new();

        public Guid EntryWeeklyCommissionId => Id;
        public int TenantId { get; private set; }
        public Guid EntryParticipationId { get; private set; }
        public Guid? WeeklySalesEligibilityDecisionId { get; private set; }
        public Guid PlacementTreeScopeId { get; private set; }
        public DateTime Cutoff { get; private set; }
        public string PlacementRulesVersion { get; private set; }
        public string StructuralQualificationRulesVersion { get; private set; }
        public string SalesEligibilityRulesVersion { get; private set; }
        public string CommissionDecisionRulesVersion { get; private set; }
        public string EvidenceSchemaVersion { get; private set; }
        public AQGreenStructuralCompletionLevel QualifiedStructuralLevel { get; private set; }
        public int CommissionedLevel { get; private set; }
        public int QualifyingDepth1Count { get; private set; }
        public int QualifyingDepth2Count { get; private set; }
        public int QualifyingDepth3Count { get; private set; }
        public AQGreenWeeklySalesApplicability SalesApplicability { get; private set; }
        public AQGreenWeeklySalesReviewStatus? SalesReviewStatus { get; private set; }
        public AQGreenWeeklySalesThresholdResult? SalesThresholdResult { get; private set; }
        public DateTime? SalesReviewedAt { get; private set; }
        public long? SalesReviewedByUserId { get; private set; }
        public int EvidenceNodeCount { get; private set; }
        public IReadOnlyCollection<AQGreenV2WeeklyCommissionEvidenceNode> Nodes =>
            _nodes.AsReadOnly();

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenV2WeeklyCommissionEvidence()
        {
        }

        public static AQGreenV2WeeklyCommissionEvidence Capture(
            EntryWeeklyCommission commission,
            EntryCommissionPeriod period,
            AQGreenCommissionStructuralEvidenceResult structuralEvidence,
            AQGreenWeeklySalesEligibilitySnapshot salesDecision)
        {
            if (commission == null) throw new ArgumentNullException(nameof(commission));
            if (period == null) throw new ArgumentNullException(nameof(period));
            if (structuralEvidence == null)
                throw new ArgumentNullException(nameof(structuralEvidence));
            if (commission.StructuralModel != AQGreenCommissionStructuralModel.PlacementV2 ||
                !string.Equals(
                    commission.CommissionDecisionRulesVersion,
                    AQGreenCommissionDecisionRules.CurrentVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Only a current Placement V2 weekly commission can own V2 evidence.");
            if (commission.TenantId != period.TenantId ||
                commission.EntryParticipationId != structuralEvidence.ParticipantId ||
                commission.CommissionPeriodId != period.Id ||
                structuralEvidence.Cutoff != period.PeriodEnd)
                throw new InvalidOperationException(
                    "The Placement V2 commission evidence crosses its Tenant, participant, or period boundary.");
            if (!string.Equals(
                    structuralEvidence.PlacementRulesVersion,
                    AQGreenPlacementRules.CurrentVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    structuralEvidence.StructuralQualificationRulesVersion,
                    AQGreenStructuralQualificationRules.CurrentVersion,
                    StringComparison.Ordinal) ||
                (int)structuralEvidence.StructuralCompletionLevel > 0 &&
                (salesDecision == null || !string.Equals(
                    salesDecision.SalesEligibilityRulesVersion,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion,
                    StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "The Placement V2 commission evidence uses an unsupported rules version.");
            if ((int)structuralEvidence.StructuralCompletionLevel !=
                    commission.HighestQualifiedNetworkLevel)
                throw new InvalidOperationException(
                    "The structural evidence does not prove the ledger's qualified level.");

            var isLevel0 = structuralEvidence.StructuralCompletionLevel ==
                AQGreenStructuralCompletionLevel.Level0;
            if (!isLevel0 && salesDecision == null)
                throw new InvalidOperationException(
                    "Placement V2 candidate levels require a finalized weekly-sales decision.");
            if (!isLevel0 &&
                (commission.TenantId != salesDecision.TenantId ||
                 commission.EntryParticipationId != salesDecision.ParticipantId ||
                 salesDecision.CommissionWeekStartUtc != period.PeriodStart ||
                 salesDecision.ReviewedAt > commission.CalculatedAt))
                throw new InvalidOperationException(
                    "The Placement V2 commission evidence crosses its Tenant, participant, period, or decision-time boundary.");
            if (!isLevel0 && salesDecision.ReviewStatus != AQGreenWeeklySalesReviewStatus.Confirmed &&
                salesDecision.ReviewStatus != AQGreenWeeklySalesReviewStatus.Rejected)
                throw new InvalidOperationException(
                    "Only a finalized weekly-sales decision can support commission evidence.");
            if (!isLevel0 && salesDecision.ReviewStatus == AQGreenWeeklySalesReviewStatus.Confirmed &&
                !salesDecision.ThresholdResult.HasValue)
                throw new InvalidOperationException(
                    "Confirmed weekly-sales evidence requires a threshold result.");
            if (!isLevel0 && salesDecision.ReviewStatus == AQGreenWeeklySalesReviewStatus.Rejected &&
                salesDecision.ThresholdResult.HasValue)
                throw new InvalidOperationException(
                    "Rejected weekly-sales evidence cannot contain a threshold result.");
            if (isLevel0 && (salesDecision != null ||
                             commission.HighestCommissionedLevel != 0))
                throw new InvalidOperationException(
                    "Level 0 Placement V2 evidence must not contain sales evidence or a commission.");

            var expectedCommissionedLevel = !isLevel0 &&
                                            salesDecision.ReviewStatus ==
                                                AQGreenWeeklySalesReviewStatus.Confirmed &&
                                            salesDecision.ThresholdResult ==
                                                AQGreenWeeklySalesThresholdResult.Met
                ? (int)structuralEvidence.StructuralCompletionLevel
                : 0;
            if (commission.HighestCommissionedLevel != expectedCommissionedLevel)
                throw new InvalidOperationException(
                    "The weekly-sales decision does not prove the ledger's commissioned level.");

            var observations = structuralEvidence.Observations;
            var maximumManifestCount = 1 +
                AQGreenStructuralCompletionCalculator.GetRequiredPopulation(1) +
                AQGreenStructuralCompletionCalculator.GetRequiredPopulation(2) +
                AQGreenStructuralCompletionCalculator.GetRequiredPopulation(3);
            if (observations.Count < 1 ||
                observations.Count > maximumManifestCount ||
                observations.Select((observation, ordinal) =>
                        observation.CanonicalOrdinal == ordinal)
                    .Any(matches => !matches) ||
                observations.GroupBy(observation => observation.SourcePlacementId)
                    .Any(group => group.Key == Guid.Empty || group.Count() != 1))
                throw new AQGreenPlacementTopologyIntegrityException(
                    "The Placement V2 commission evidence manifest is incomplete or duplicated.");

            var evidence = new AQGreenV2WeeklyCommissionEvidence
            {
                Id = commission.Id,
                TenantId = commission.TenantId,
                EntryParticipationId = commission.EntryParticipationId,
                WeeklySalesEligibilityDecisionId = salesDecision?.DecisionId,
                PlacementTreeScopeId = structuralEvidence.PlacementTreeScopeId,
                Cutoff = structuralEvidence.Cutoff,
                PlacementRulesVersion = structuralEvidence.PlacementRulesVersion,
                StructuralQualificationRulesVersion =
                    structuralEvidence.StructuralQualificationRulesVersion,
                SalesEligibilityRulesVersion = isLevel0
                    ? null
                    : salesDecision.SalesEligibilityRulesVersion,
                CommissionDecisionRulesVersion =
                    commission.CommissionDecisionRulesVersion,
                EvidenceSchemaVersion =
                    AQGreenV2WeeklyCommissionEvidenceSchema.CurrentVersion,
                QualifiedStructuralLevel =
                    structuralEvidence.StructuralCompletionLevel,
                CommissionedLevel = expectedCommissionedLevel,
                QualifyingDepth1Count = structuralEvidence.QualifyingDepth1Count,
                QualifyingDepth2Count = structuralEvidence.QualifyingDepth2Count,
                QualifyingDepth3Count = structuralEvidence.QualifyingDepth3Count,
                SalesApplicability = isLevel0
                    ? AQGreenWeeklySalesApplicability.NotApplicable
                    : AQGreenWeeklySalesApplicability.Applicable,
                SalesReviewStatus = salesDecision?.ReviewStatus,
                SalesThresholdResult = salesDecision?.ThresholdResult,
                SalesReviewedAt = salesDecision?.ReviewedAt,
                SalesReviewedByUserId = salesDecision?.ReviewedByUserId,
                EvidenceNodeCount = observations.Count
            };
            evidence._nodes.AddRange(observations.Select(observation =>
                AQGreenV2WeeklyCommissionEvidenceNode.Capture(
                    commission.TenantId,
                    commission.Id,
                    observation)));
            return evidence;
        }
    }

    public sealed class AQGreenV2WeeklyCommissionEvidenceNode
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

        private AQGreenV2WeeklyCommissionEvidenceNode()
        {
        }

        internal static AQGreenV2WeeklyCommissionEvidenceNode Capture(
            int tenantId,
            Guid evidenceId,
            AQGreenCommissionStructuralEvidenceObservation observation)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (evidenceId == Guid.Empty)
                throw new ArgumentException(
                    "A commission evidence identity is required.",
                    nameof(evidenceId));
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (observation.CanonicalOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(observation.CanonicalOrdinal));
            if (observation.SourcePlacementId == Guid.Empty ||
                observation.CustomerIdObserved <= 0 ||
                observation.UserIdObserved <= 0)
                throw new AQGreenPlacementTopologyIntegrityException(
                    "Placement V2 commission evidence requires placement and identity observations.");

            return new AQGreenV2WeeklyCommissionEvidenceNode
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
