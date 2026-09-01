using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Domain.Onyx
{
    public interface IAQGreenV2WeeklyCommissionEvidenceReplayValidator
    {
        Task<AQGreenV2WeeklyCommissionEvidenceReplayResult> ValidateAsync(
            Guid weeklyCommissionId,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenV2WeeklyCommissionEvidenceReplayResult
    {
        public AQGreenV2WeeklyCommissionEvidenceReplayResult(
            AQGreenStructuralCompletionLevel qualifiedStructuralLevel,
            int commissionedLevel,
            decimal totalAmount,
            AQGreenWeeklySalesReviewStatus? salesReviewStatus,
            AQGreenWeeklySalesThresholdResult? salesThresholdResult,
            int evidenceNodeCount)
        {
            QualifiedStructuralLevel = qualifiedStructuralLevel;
            CommissionedLevel = commissionedLevel;
            TotalAmount = totalAmount;
            SalesReviewStatus = salesReviewStatus;
            SalesThresholdResult = salesThresholdResult;
            EvidenceNodeCount = evidenceNodeCount;
        }

        public AQGreenStructuralCompletionLevel QualifiedStructuralLevel { get; }
        public int CommissionedLevel { get; }
        public decimal TotalAmount { get; }
        public AQGreenWeeklySalesReviewStatus? SalesReviewStatus { get; }
        public AQGreenWeeklySalesThresholdResult? SalesThresholdResult { get; }
        public int EvidenceNodeCount { get; }
    }

    public static class AQGreenV2WeeklyCommissionEvidenceReplay
    {
        public static AQGreenV2WeeklyCommissionEvidenceReplayResult Validate(
            EntryWeeklyCommission commission,
            EntryCommissionPeriod period,
            AQGreenV2WeeklyCommissionEvidence evidence,
            IReadOnlyCollection<AQGreenV2WeeklyCommissionEvidenceNode> nodes,
            IReadOnlyCollection<AQGreenNetworkPlacement> placements,
            AQGreenWeeklySalesEligibilityDecision salesDecision,
            EntryCommissionTerms terms)
        {
            if (commission == null) throw new ArgumentNullException(nameof(commission));
            if (period == null) throw new ArgumentNullException(nameof(period));
            if (evidence == null)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The Placement V2 weekly commission evidence header is missing.");
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            if (terms == null)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The recorded commission financial terms are missing.");

            EnsureVersion(
                "commission decision rules",
                commission.CommissionDecisionRulesVersion,
                AQGreenCommissionDecisionRules.CurrentVersion);
            EnsureVersion(
                "evidence commission decision rules",
                evidence.CommissionDecisionRulesVersion,
                AQGreenCommissionDecisionRules.CurrentVersion);
            EnsureVersion(
                "evidence schema",
                evidence.EvidenceSchemaVersion,
                AQGreenV2WeeklyCommissionEvidenceSchema.CurrentVersion);
            EnsureVersion(
                "placement rules",
                evidence.PlacementRulesVersion,
                AQGreenPlacementRules.CurrentVersion);
            EnsureVersion(
                "structural qualification rules",
                evidence.StructuralQualificationRulesVersion,
                AQGreenStructuralQualificationRules.CurrentVersion);
            var isLevel0 = evidence.QualifiedStructuralLevel ==
                AQGreenStructuralCompletionLevel.Level0;
            if (evidence.SalesApplicability !=
                    (isLevel0
                        ? AQGreenWeeklySalesApplicability.NotApplicable
                        : AQGreenWeeklySalesApplicability.Applicable))
                throw new AQGreenCommissionEvidenceReplayException(
                    "The Placement V2 sales applicability does not match the structural level.");
            if (isLevel0)
            {
                if (salesDecision != null || evidence.WeeklySalesEligibilityDecisionId.HasValue ||
                    evidence.SalesEligibilityRulesVersion != null ||
                    evidence.SalesReviewStatus.HasValue || evidence.SalesThresholdResult.HasValue ||
                    evidence.SalesReviewedAt.HasValue || evidence.SalesReviewedByUserId.HasValue)
                    throw new AQGreenCommissionEvidenceReplayException(
                        "Level 0 Placement V2 evidence must not contain weekly-sales evidence.");
            }
            else
            {
                if (salesDecision == null)
                    throw new AQGreenCommissionEvidenceReplayException(
                        "The recorded weekly-sales decision is missing.");
                EnsureVersion(
                    "weekly-sales rules",
                    evidence.SalesEligibilityRulesVersion,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            }

            if (commission.StructuralModel != AQGreenCommissionStructuralModel.PlacementV2 ||
                evidence.Id != commission.Id ||
                evidence.TenantId != commission.TenantId ||
                evidence.EntryParticipationId != commission.EntryParticipationId ||
                period.Id != commission.CommissionPeriodId ||
                period.TenantId != commission.TenantId ||
                evidence.Cutoff != period.PeriodEnd ||
                !string.Equals(
                    evidence.CommissionDecisionRulesVersion,
                    commission.CommissionDecisionRulesVersion,
                    StringComparison.Ordinal) ||
                evidence.QualifiedStructuralLevel !=
                    (AQGreenStructuralCompletionLevel)commission.HighestQualifiedNetworkLevel ||
                !string.Equals(period.RulesVersion, commission.RulesVersion, StringComparison.Ordinal) ||
                !string.Equals(terms.Version, commission.RulesVersion, StringComparison.Ordinal) ||
                !string.Equals(terms.Currency, commission.Currency, StringComparison.Ordinal))
                throw new AQGreenCommissionEvidenceReplayException(
                    "The Placement V2 weekly commission ledger, period, and evidence conflict.");

            if (!isLevel0 && (salesDecision.Id != evidence.WeeklySalesEligibilityDecisionId ||
                salesDecision.TenantId != evidence.TenantId ||
                salesDecision.ParticipantId != evidence.EntryParticipationId ||
                salesDecision.CommissionWeekStartUtc != period.PeriodStart ||
                !string.Equals(
                    salesDecision.SalesEligibilityRulesVersion,
                    evidence.SalesEligibilityRulesVersion,
                    StringComparison.Ordinal) ||
                salesDecision.ReviewStatus != evidence.SalesReviewStatus ||
                salesDecision.ThresholdResult != evidence.SalesThresholdResult ||
                salesDecision.ReviewedAt != evidence.SalesReviewedAt ||
                salesDecision.ReviewedByUserId != evidence.SalesReviewedByUserId))
                throw new AQGreenCommissionEvidenceReplayException(
                    "The evidence conflicts with the exact finalized weekly-sales decision.");
            if (!isLevel0)
                ValidateSalesDecision(salesDecision, commission.CalculatedAt);

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
                throw new AQGreenCommissionEvidenceReplayException(
                    "The Placement V2 weekly commission manifest is incomplete or duplicated.");

            var placementById = placements
                .GroupBy(placement => placement.Id)
                .ToDictionary(group => group.Key, group => group.ToList());
            if (placementById.Any(group => group.Value.Count != 1) ||
                orderedNodes.Any(node => !placementById.ContainsKey(node.SourcePlacementId)) ||
                placements.Count != orderedNodes.Count)
                throw new AQGreenCommissionEvidenceReplayException(
                    "An immutable source placement is missing or ambiguous.");

            var anchor = placementById[orderedNodes[0].SourcePlacementId][0];
            if (anchor.TenantId != evidence.TenantId ||
                anchor.ParticipantId != commission.EntryParticipationId ||
                anchor.PlacementTreeScopeId != evidence.PlacementTreeScopeId ||
                anchor.PlacedAt > evidence.Cutoff)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The evidence anchor does not match the weekly commission decision.");

            IReadOnlyList<AQGreenBoundedPlacementTopologyNode> topology;
            try
            {
                topology = AQGreenBoundedPlacementTopologyValidator.Validate(
                    evidence.TenantId,
                    evidence.PlacementTreeScopeId,
                    commission.EntryParticipationId,
                    evidence.Cutoff,
                    AQGreenStructuralCompletionCalculator.MaximumLevel,
                    placements.Select(MapPlacement).ToList());
            }
            catch (AQGreenPlacementTopologyIntegrityException exception)
            {
                throw new AQGreenCommissionEvidenceReplayException(
                    "The immutable placements do not reproduce the bounded commission topology.",
                    exception);
            }
            if (!topology.Select(item => item.Placement.Id).SequenceEqual(
                    orderedNodes.Select(node => node.SourcePlacementId)))
                throw new AQGreenCommissionEvidenceReplayException(
                    "The weekly commission manifest is not in canonical order.");

            var nodeByPlacement = orderedNodes.ToDictionary(node => node.SourcePlacementId);
            var qualificationNodes = topology.Select(topologyNode =>
            {
                var placement = topologyNode.Placement;
                var node = nodeByPlacement[placement.Id];
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
                    AQGreenStructuralCompletionCalculator.MaximumLevel,
                    qualificationNodes);
            }
            catch (Exception exception) when (
                exception is AQGreenPlacementTopologyIntegrityException ||
                exception is AQGreenStructuralContributionPolicyRequiredException)
            {
                throw new AQGreenCommissionEvidenceReplayException(
                    "The persisted lifecycle and security observations no longer prove the recorded structure.",
                    exception);
            }
            if (outcome.StructuralCompletionLevel != evidence.QualifiedStructuralLevel ||
                outcome.QualifyingDepthCounts[1] != evidence.QualifyingDepth1Count ||
                outcome.QualifyingDepthCounts[2] != evidence.QualifyingDepth2Count ||
                outcome.QualifyingDepthCounts[3] != evidence.QualifyingDepth3Count)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The replayed structural result contradicts the recorded commission evidence.");

            var commissionedLevel = !isLevel0 && evidence.SalesReviewStatus ==
                                        AQGreenWeeklySalesReviewStatus.Confirmed &&
                                    evidence.SalesThresholdResult ==
                                        AQGreenWeeklySalesThresholdResult.Met
                ? (int)outcome.StructuralCompletionLevel
                : 0;
            var orderedComponents = commission.Components
                .OrderBy(component => component.Level)
                .ToList();
            var expectedTotal = 0m;
            for (var level = 1; level <= commissionedLevel; level++)
            {
                expectedTotal += terms.GetComponentAmount(level);
                if (orderedComponents.Count < level ||
                    orderedComponents[level - 1].Level != level ||
                    orderedComponents[level - 1].Amount != terms.GetComponentAmount(level))
                    throw new AQGreenCommissionEvidenceReplayException(
                        "The recorded commission components conflict with the financial terms.");
            }
            if (evidence.CommissionedLevel != commissionedLevel ||
                commission.HighestCommissionedLevel != commissionedLevel ||
                orderedComponents.Count != commissionedLevel ||
                commission.TotalAmount != expectedTotal)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The replayed commissioned level or amount contradicts the ledger.");

            return new AQGreenV2WeeklyCommissionEvidenceReplayResult(
                outcome.StructuralCompletionLevel,
                commissionedLevel,
                expectedTotal,
                evidence.SalesReviewStatus,
                evidence.SalesThresholdResult,
                orderedNodes.Count);
        }

        private static void EnsureVersion(string kind, string actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new AQGreenCommissionEvidenceVersionNotSupportedException(kind, actual);
        }

        private static void ValidateSalesDecision(
            AQGreenWeeklySalesEligibilityDecision decision,
            DateTime commissionCalculatedAt)
        {
            EnsureVersion(
                "weekly-sales rules",
                decision.SalesEligibilityRulesVersion,
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            if (decision.EvidenceReferences.Count == 0 ||
                !decision.ReviewedAt.HasValue ||
                !decision.ReviewedByUserId.HasValue ||
                decision.ReviewedByUserId <= 0 ||
                decision.ReviewedAt > commissionCalculatedAt ||
                decision.ReviewedAt < decision.CommissionWeekStartUtc.AddDays(7))
                throw new AQGreenCommissionEvidenceReplayException(
                    "The finalized weekly-sales decision has incomplete or invalid evidence and audit facts.");

            if (decision.ReviewStatus == AQGreenWeeklySalesReviewStatus.Confirmed)
            {
                if (!decision.ReviewedSprayQuantity.HasValue ||
                    !decision.ReviewedOneLitreQuantity.HasValue ||
                    !decision.ReviewedFiveLitreQuantity.HasValue ||
                    !decision.ThresholdResult.HasValue ||
                    decision.RejectionReason != null)
                    throw new AQGreenCommissionEvidenceReplayException(
                        "The confirmed weekly-sales decision has an invalid state shape.");
                AQGreenWeeklySalesThresholdResult recalculated;
                try
                {
                    recalculated = AQGreenWeeklySalesEligibilityEvaluator.Evaluate(
                        decision.SalesEligibilityRulesVersion,
                        new AQGreenWeeklySalesQuantities(
                            decision.ReviewedSprayQuantity.Value,
                            decision.ReviewedOneLitreQuantity.Value,
                            decision.ReviewedFiveLitreQuantity.Value));
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is AQGreenWeeklySalesEligibilityVersionNotSupportedException)
                {
                    throw new AQGreenCommissionEvidenceReplayException(
                        "The confirmed weekly-sales quantities cannot be safely evaluated.",
                        exception);
                }
                if (recalculated != decision.ThresholdResult)
                    throw new AQGreenCommissionEvidenceReplayException(
                        "The weekly-sales threshold result contradicts the versioned evaluator.");
            }
            else if (decision.ReviewStatus == AQGreenWeeklySalesReviewStatus.Rejected)
            {
                if (decision.ReviewedSprayQuantity.HasValue ||
                    decision.ReviewedOneLitreQuantity.HasValue ||
                    decision.ReviewedFiveLitreQuantity.HasValue ||
                    decision.ThresholdResult.HasValue ||
                    string.IsNullOrWhiteSpace(decision.RejectionReason))
                    throw new AQGreenCommissionEvidenceReplayException(
                        "The rejected weekly-sales decision has an invalid state shape.");
            }
            else
            {
                throw new AQGreenCommissionEvidenceReplayException(
                    "The weekly-sales decision is not final.");
            }
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

    public sealed class AQGreenCommissionEvidenceVersionNotSupportedException
        : InvalidOperationException
    {
        public AQGreenCommissionEvidenceVersionNotSupportedException(
            string versionKind,
            string version)
            : base(
                $"The recorded AQGreen weekly commission {versionKind} version " +
                $"'{version ?? "<missing>"}' is not supported.")
        {
        }
    }

    public sealed class AQGreenCommissionEvidenceReplayException
        : InvalidOperationException
    {
        public AQGreenCommissionEvidenceReplayException(string message)
            : base(message)
        {
        }

        public AQGreenCommissionEvidenceReplayException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
