using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AQGreenV2WeeklyCommissionEvidenceTests
    {
        private static readonly DateTime WeekStart =
            new(2026, 7, 2, 22, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Replay_ReconstructsStructureSalesAndAmountWithoutCurrentIdentityState()
        {
            var graph = CreateReplayGraph();

            var replay = AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                graph.Commission,
                graph.Period,
                graph.Evidence,
                graph.Evidence.Nodes.ToList(),
                graph.Placements,
                graph.SalesDecision,
                graph.Terms);

            Assert.Equal(AQGreenStructuralCompletionLevel.Level1,
                replay.QualifiedStructuralLevel);
            Assert.Equal(1, replay.CommissionedLevel);
            Assert.Equal(150m, replay.TotalAmount);
            Assert.Equal(AQGreenWeeklySalesReviewStatus.Confirmed,
                replay.SalesReviewStatus);
            Assert.Equal(AQGreenWeeklySalesThresholdResult.Met,
                replay.SalesThresholdResult);
            Assert.Equal(6, replay.EvidenceNodeCount);
        }

        [Fact]
        public void Replay_FailsClosedForUnsupportedVersionOrChangedRecordedTerms()
        {
            var unsupported = CreateReplayGraph();
            typeof(AQGreenV2WeeklyCommissionEvidence)
                .GetProperty(
                    nameof(AQGreenV2WeeklyCommissionEvidence.EvidenceSchemaVersion),
                    BindingFlags.Instance | BindingFlags.Public)
                .SetValue(unsupported.Evidence, "unsupported-evidence-version");
            Assert.Throws<AQGreenCommissionEvidenceVersionNotSupportedException>(() =>
                AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                    unsupported.Commission,
                    unsupported.Period,
                    unsupported.Evidence,
                    unsupported.Evidence.Nodes.ToList(),
                    unsupported.Placements,
                    unsupported.SalesDecision,
                    unsupported.Terms));

            var changedTerms = CreateReplayGraph();
            var incompatibleTerms = EntryCommissionTerms.Create(
                changedTerms.Terms.Version,
                changedTerms.Terms.EffectiveFrom,
                999m,
                999m,
                999m);
            Assert.Throws<AQGreenCommissionEvidenceReplayException>(() =>
                AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                    changedTerms.Commission,
                    changedTerms.Period,
                    changedTerms.Evidence,
                    changedTerms.Evidence.Nodes.ToList(),
                    changedTerms.Placements,
                    changedTerms.SalesDecision,
                    incompatibleTerms));
        }

        [Fact]
        public void ConfirmedNotMet_AndRejected_RemainDurablyDistinctNoCommissionOutcomes()
        {
            var notMet = CaptureNoCommissionEvidence(
                AQGreenWeeklySalesReviewStatus.Confirmed,
                AQGreenWeeklySalesThresholdResult.NotMet);
            var rejected = CaptureNoCommissionEvidence(
                AQGreenWeeklySalesReviewStatus.Rejected,
                null);

            Assert.Equal(0, notMet.CommissionedLevel);
            Assert.Equal(AQGreenWeeklySalesReviewStatus.Confirmed,
                notMet.SalesReviewStatus);
            Assert.Equal(AQGreenWeeklySalesThresholdResult.NotMet,
                notMet.SalesThresholdResult);
            Assert.Equal(0, rejected.CommissionedLevel);
            Assert.Equal(AQGreenWeeklySalesReviewStatus.Rejected,
                rejected.SalesReviewStatus);
            Assert.Null(rejected.SalesThresholdResult);
            Assert.NotEqual(notMet.SalesReviewStatus, rejected.SalesReviewStatus);
        }

        [Fact]
        public void Level0_CapturesDeterministicNotEarnedWithoutSalesEvidence()
        {
            var participation = CreateActiveParticipation();
            var period = CreatePeriod();
            var evidence = AQGreenV2WeeklyCommissionEvidence.Capture(
                CreateNoCommission(participation, period),
                period,
                CreateStructuralEvidence(participation, period),
                null);

            Assert.Equal(AQGreenWeeklySalesApplicability.NotApplicable,
                evidence.SalesApplicability);
            Assert.Null(evidence.WeeklySalesEligibilityDecisionId);
            Assert.Null(evidence.SalesEligibilityRulesVersion);
            Assert.Null(evidence.SalesReviewStatus);
            Assert.Null(evidence.SalesThresholdResult);
            Assert.Null(evidence.SalesReviewedAt);
            Assert.Null(evidence.SalesReviewedByUserId);
        }

        [Fact]
        public void Capture_RejectsHeldSalesEvidence()
        {
            var participation = CreateActiveParticipation();
            var period = CreatePeriod();
            var commission = CreateNoCommission(
                participation, period, EntryNetworkLevel.Level1);
            var structuralEvidence = CreateStructuralEvidence(
                participation, period, AQGreenStructuralCompletionLevel.Level1);
            var held = CreateSalesSnapshot(
                participation,
                AQGreenWeeklySalesReviewStatus.HeldForEvidence,
                null);

            Assert.Throws<InvalidOperationException>(() =>
                AQGreenV2WeeklyCommissionEvidence.Capture(
                    commission,
                    period,
                    structuralEvidence,
                    held));
        }

        [Fact]
        public void Capture_RejectsSalesReviewAfterCommissionCalculation()
        {
            var participation = CreateActiveParticipation();
            var period = CreatePeriod();
            var commission = CreateNoCommission(
                participation, period, EntryNetworkLevel.Level1);
            var reviewedAfterCalculation = new AQGreenWeeklySalesEligibilitySnapshot(
                Guid.NewGuid(),
                participation.TenantId,
                participation.Id,
                WeekStart,
                AQGreenWeeklySalesEligibilityRules.CurrentVersion,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                0,
                0,
                0,
                AQGreenWeeklySalesThresholdResult.NotMet,
                commission.CalculatedAt.AddTicks(1),
                9001,
                null);

            Assert.Throws<InvalidOperationException>(() =>
                AQGreenV2WeeklyCommissionEvidence.Capture(
                    commission,
                    period,
                    CreateStructuralEvidence(
                        participation, period, AQGreenStructuralCompletionLevel.Level1),
                    reviewedAfterCalculation));
        }

        [Fact]
        public void Replay_FailsWhenFinalSalesFactsDoNotReproduceEvaluatorOrEvidence()
        {
            var changedResult = CreateReplayGraph();
            SetProperty(
                changedResult.SalesDecision,
                nameof(AQGreenWeeklySalesEligibilityDecision.ThresholdResult),
                AQGreenWeeklySalesThresholdResult.NotMet);
            SetProperty(
                changedResult.Evidence,
                nameof(AQGreenV2WeeklyCommissionEvidence.SalesThresholdResult),
                AQGreenWeeklySalesThresholdResult.NotMet);
            var evaluatorException = Assert.Throws<
                AQGreenCommissionEvidenceReplayException>(() =>
                AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                    changedResult.Commission,
                    changedResult.Period,
                    changedResult.Evidence,
                    changedResult.Evidence.Nodes.ToList(),
                    changedResult.Placements,
                    changedResult.SalesDecision,
                    changedResult.Terms));
            Assert.Contains("versioned evaluator", evaluatorException.Message);

            var missingFinalEvidence = CreateReplayGraph();
            var evidenceReferences = (List<AQGreenWeeklySalesEvidenceReference>)
                typeof(AQGreenWeeklySalesEligibilityDecision)
                    .GetField(
                        "_evidenceReferences",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(missingFinalEvidence.SalesDecision);
            evidenceReferences.Clear();
            Assert.Throws<AQGreenCommissionEvidenceReplayException>(() =>
                AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                    missingFinalEvidence.Commission,
                    missingFinalEvidence.Period,
                    missingFinalEvidence.Evidence,
                    missingFinalEvidence.Evidence.Nodes.ToList(),
                    missingFinalEvidence.Placements,
                    missingFinalEvidence.SalesDecision,
                    missingFinalEvidence.Terms));
        }

        [Fact]
        public void Replay_FailsWhenBoundedCutoffManifestOmitsAPlacement()
        {
            var graph = CreateReplayGraph();
            var extraParticipation = CreateActiveParticipation(999);
            var extraPlacement = AQGreenNetworkPlacement.CreateChild(
                graph.Placements.ElementAt(1),
                extraParticipation.Id,
                1,
                WeekStart.AddDays(-1),
                AQGreenPlacementRules.CurrentVersion);
            var incompleteManifestPlacements = graph.Placements
                .Append(extraPlacement)
                .ToList();

            Assert.Throws<AQGreenCommissionEvidenceReplayException>(() =>
                AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                    graph.Commission,
                    graph.Period,
                    graph.Evidence,
                    graph.Evidence.Nodes.ToList(),
                    incompleteManifestPlacements,
                    graph.SalesDecision,
                    graph.Terms));
        }

        private static AQGreenV2WeeklyCommissionEvidence CaptureNoCommissionEvidence(
            AQGreenWeeklySalesReviewStatus status,
            AQGreenWeeklySalesThresholdResult? threshold)
        {
            var participation = CreateActiveParticipation();
            var period = CreatePeriod();
            return AQGreenV2WeeklyCommissionEvidence.Capture(
                CreateNoCommission(participation, period, EntryNetworkLevel.Level1),
                period,
                CreateStructuralEvidence(
                    participation, period, AQGreenStructuralCompletionLevel.Level1),
                CreateSalesSnapshot(participation, status, threshold));
        }

        private static ReplayGraph CreateReplayGraph()
        {
            var participations = Enumerable.Range(0, 6)
                .Select(index => CreateActiveParticipation(101 + index))
                .ToList();
            var root = participations[0];
            var terms = CreateCommissionTerms();
            var period = CreatePeriod();
            var calculator = new EntryWeeklyCommissionCalculator(
                new EntryNetworkQualificationEvaluator());
            var commission = calculator.CalculatePlacementV2(
                root,
                period,
                terms,
                EntryNetworkLevel.Level1,
                EntryNetworkLevel.Level1,
                Array.Empty<EntryMonthlyObligation>());
            var scope = AQGreenPlacementTreeScope.Create(1);
            var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                scope,
                root.Id,
                WeekStart.AddDays(-1),
                AQGreenPlacementRules.CurrentVersion);
            var placements = new List<AQGreenNetworkPlacement> { rootPlacement };
            placements.AddRange(participations.Skip(1).Select((participation, index) =>
                AQGreenNetworkPlacement.CreateChild(
                    rootPlacement,
                    participation.Id,
                    index + 1,
                    rootPlacement.PlacedAt,
                    AQGreenPlacementRules.CurrentVersion)));
            var structuralEvidence = new AQGreenCommissionStructuralEvidenceResult(
                root.Id,
                scope.Id,
                period.PeriodEnd,
                AQGreenStructuralCompletionLevel.Level1,
                5,
                0,
                0,
                AQGreenPlacementRules.CurrentVersion,
                AQGreenStructuralQualificationRules.CurrentVersion,
                placements.Select((placement, ordinal) =>
                    new AQGreenCommissionStructuralEvidenceObservation
                    {
                        CanonicalOrdinal = ordinal,
                        SourcePlacementId = placement.Id,
                        ParticipationStatusObserved = EntryParticipationStatus.Active,
                        ParticipationActivatedAtObserved = participations[ordinal].ActivatedAt,
                        CustomerIdObserved = participations[ordinal].CustomerId,
                        CustomerTenantMatchedObserved = true,
                        CustomerIsActiveObserved = true,
                        UserIdObserved = 5001 + ordinal,
                        UserTenantMatchedObserved = true,
                        UserIsActiveObserved = true
                    }).ToList());
            var salesDecision = AQGreenWeeklySalesEligibilityDecision.Begin(
                1,
                root.Id,
                AQGreenCommissionWeek.FromStartUtc(WeekStart),
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            salesDecision.AddManualEvidence("replay-evidence", WeekStart.AddDays(7));
            salesDecision.Confirm(
                new AQGreenWeeklySalesQuantities(5, 5, 5),
                9001,
                WeekStart.AddDays(7));
            var salesSnapshot = new AQGreenWeeklySalesEligibilitySnapshot(
                salesDecision.Id,
                salesDecision.TenantId,
                salesDecision.ParticipantId,
                salesDecision.CommissionWeekStartUtc,
                salesDecision.SalesEligibilityRulesVersion,
                salesDecision.ReviewStatus,
                salesDecision.ReviewedSprayQuantity,
                salesDecision.ReviewedOneLitreQuantity,
                salesDecision.ReviewedFiveLitreQuantity,
                salesDecision.ThresholdResult,
                salesDecision.ReviewedAt.Value,
                salesDecision.ReviewedByUserId.Value,
                salesDecision.RejectionReason);
            var evidence = AQGreenV2WeeklyCommissionEvidence.Capture(
                commission,
                period,
                structuralEvidence,
                salesSnapshot);
            return new ReplayGraph(
                commission,
                period,
                evidence,
                placements,
                salesDecision,
                terms);
        }

        private static EntryWeeklyCommission CreateNoCommission(
            EntryParticipation participation,
            EntryCommissionPeriod period,
            EntryNetworkLevel qualifiedLevel = EntryNetworkLevel.None)
        {
            var calculator = new EntryWeeklyCommissionCalculator(
                new EntryNetworkQualificationEvaluator());
            return calculator.CalculatePlacementV2(
                participation,
                period,
                CreateCommissionTerms(),
                qualifiedLevel,
                EntryNetworkLevel.None,
                Array.Empty<EntryMonthlyObligation>());
        }

        private static AQGreenCommissionStructuralEvidenceResult CreateStructuralEvidence(
            EntryParticipation participation,
            EntryCommissionPeriod period,
            AQGreenStructuralCompletionLevel level = AQGreenStructuralCompletionLevel.Level0) =>
            new(
                participation.Id,
                Guid.NewGuid(),
                period.PeriodEnd,
                level,
                level == AQGreenStructuralCompletionLevel.Level1 ? 5 : 0,
                0,
                0,
                AQGreenPlacementRules.CurrentVersion,
                AQGreenStructuralQualificationRules.CurrentVersion,
                new[]
                {
                    new AQGreenCommissionStructuralEvidenceObservation
                    {
                        CanonicalOrdinal = 0,
                        SourcePlacementId = Guid.NewGuid(),
                        ParticipationStatusObserved = EntryParticipationStatus.Active,
                        ParticipationActivatedAtObserved = participation.ActivatedAt,
                        CustomerIdObserved = participation.CustomerId,
                        CustomerTenantMatchedObserved = true,
                        CustomerIsActiveObserved = true,
                        UserIdObserved = 5001,
                        UserTenantMatchedObserved = true,
                        UserIsActiveObserved = true
                    }
                });

        private static AQGreenWeeklySalesEligibilitySnapshot CreateSalesSnapshot(
            EntryParticipation participation,
            AQGreenWeeklySalesReviewStatus status,
            AQGreenWeeklySalesThresholdResult? threshold) =>
            new(
                Guid.NewGuid(),
                participation.TenantId,
                participation.Id,
                WeekStart,
                AQGreenWeeklySalesEligibilityRules.CurrentVersion,
                status,
                threshold.HasValue ? 0 : null,
                threshold.HasValue ? 0 : null,
                threshold.HasValue ? 0 : null,
                threshold,
                WeekStart.AddDays(7),
                9001,
                status == AQGreenWeeklySalesReviewStatus.Rejected
                    ? "Evidence could not be substantiated."
                    : null);

        private static void SetProperty(
            object target,
            string propertyName,
            object value) =>
            target.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                .SetValue(target, value);

        private static EntryCommissionPeriod CreatePeriod()
        {
            var terms = CreateCommissionTerms();
            return EntryCommissionPeriod.CreateClosedPeriod(
                1,
                WeekStart,
                WeekStart.AddDays(7).AddTicks(-1),
                "Africa/Johannesburg",
                WeekStart.AddDays(7),
                terms);
        }

        private static EntryCommissionTerms CreateCommissionTerms() =>
            EntryCommissionTerms.Create(
                "2026-07",
                WeekStart.AddDays(-2),
                150m,
                250m,
                1250m);

        private static EntryParticipation CreateActiveParticipation(int customerId = 101)
        {
            var programmeTerms = EntryProgrammeTerms.Create(
                "2026-07",
                WeekStart.AddDays(-2),
                600m,
                600m,
                600m,
                7);
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                programmeTerms,
                WeekStart.AddDays(-2));
            foreach (var purpose in new[]
                     {
                         MemberPaymentPurpose.EntryRegistration,
                         MemberPaymentPurpose.EntryActivation
                     })
            {
                var payment = MemberPayment.CreatePending(
                    1,
                    participation.CustomerId,
                    purpose,
                    600m,
                    "Yoco",
                    $"{purpose}-{Guid.NewGuid():N}",
                    WeekStart.AddDays(-2));
                payment.Confirm(WeekStart.AddDays(-2).AddMinutes(1));
                participation.ApplyConfirmedActivationPayment(payment);
            }
            participation.ApproveByAdministrator(
                1,
                WeekStart.AddDays(-2).AddMinutes(2));
            return participation;
        }

        private sealed class ReplayGraph
        {
            public ReplayGraph(
                EntryWeeklyCommission commission,
                EntryCommissionPeriod period,
                AQGreenV2WeeklyCommissionEvidence evidence,
                IReadOnlyCollection<AQGreenNetworkPlacement> placements,
                AQGreenWeeklySalesEligibilityDecision salesDecision,
                EntryCommissionTerms terms)
            {
                Commission = commission;
                Period = period;
                Evidence = evidence;
                Placements = placements;
                SalesDecision = salesDecision;
                Terms = terms;
            }

            public EntryWeeklyCommission Commission { get; }
            public EntryCommissionPeriod Period { get; }
            public AQGreenV2WeeklyCommissionEvidence Evidence { get; }
            public IReadOnlyCollection<AQGreenNetworkPlacement> Placements { get; }
            public AQGreenWeeklySalesEligibilityDecision SalesDecision { get; }
            public EntryCommissionTerms Terms { get; }
        }
    }
}
