using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public sealed class AQGreenV2GraduationEvidenceTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly EntryProgrammeTerms EntryTerms =
            EntryProgrammeTerms.Create(
                "entry-evidence-v1",
                EffectiveFrom,
                600m,
                600m,
                600m,
                7);
        private static readonly OnyxLoanTerms LoanTerms =
            OnyxLoanTerms.Create(
                "accepted-loan-evidence-v1",
                EffectiveFrom,
                6120m,
                30m,
                3,
                4,
                200m);

        [Fact]
        public void CaptureAndReplay_SupportedGraduationRulesVersionPreservesMinimalProof()
        {
            var graph = CreateGraph();

            var replay = AQGreenV2GraduationEvidenceReplay.Validate(
                graph.Decision,
                graph.Evidence,
                graph.Evidence.Nodes.ToList(),
                graph.Placements);

            graph.Decision.StructuralModel.ShouldBe(
                AQGreenGraduationStructuralModel.PlacementV2);
            graph.Decision.EvaluatedNetworkLevel.ShouldBeNull();
            graph.Decision.GraduationRulesVersion.ShouldBe(
                OnyxGraduationRules.CurrentVersion);
            graph.Decision.EvaluatedLoanTermsVersion.ShouldBe(
                graph.Loan.TermsVersion);
            graph.Evidence.Id.ShouldBe(graph.Decision.Id);
            graph.Evidence.EvidenceNodeCount.ShouldBe(31);
            graph.Evidence.Nodes.Count.ShouldBe(31);
            graph.Evidence.Nodes.Select(node => node.CanonicalOrdinal)
                .ShouldBe(Enumerable.Range(0, 31));
            graph.Evidence.Nodes.Select(node => node.SourcePlacementId)
                .Distinct().Count().ShouldBe(31);
            replay.StructuralCompletionLevel.ShouldBe(
                AQGreenStructuralCompletionLevel.Level2);
            replay.QualifyingDepth1Count.ShouldBe(5);
            replay.QualifyingDepth2Count.ShouldBe(25);
            replay.EvidenceNodeCount.ShouldBe(31);

            var persistedNodeProperties = typeof(AQGreenV2GraduationEvidenceNode)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToList();
            persistedNodeProperties.ShouldNotContain("ParticipantId");
            persistedNodeProperties.ShouldNotContain("PlacementTreeScopeId");
            persistedNodeProperties.ShouldNotContain("PlacementParentParticipantId");
            persistedNodeProperties.ShouldNotContain("PlacementSlot");
            persistedNodeProperties.ShouldNotContain("CanonicalPath");
            persistedNodeProperties.ShouldNotContain("PlacedAt");
            persistedNodeProperties.ShouldNotContain("PlacementRulesVersion");
            persistedNodeProperties.ShouldNotContain("RelativeDepth");
            persistedNodeProperties.ShouldNotContain("Name");
            persistedNodeProperties.ShouldNotContain("Email");
        }

        [Fact]
        public void Replay_RejectsMissingEvidenceAndUnknownRecordedVersion()
        {
            var missing = CreateGraph();
            var incompleteNodes = missing.Evidence.Nodes.Take(30).ToList();
            Should.Throw<AQGreenGraduationEvidenceReplayException>(() =>
                AQGreenV2GraduationEvidenceReplay.Validate(
                    missing.Decision,
                    missing.Evidence,
                    incompleteNodes,
                    missing.Placements));

            var unsupported = CreateGraph();
            typeof(AQGreenV2GraduationEvidence)
                .GetProperty(
                    nameof(AQGreenV2GraduationEvidence.StructuralQualificationRulesVersion))
                .SetValue(unsupported.Evidence, "unsupported-structural-version");
            Should.Throw<AQGreenGraduationEvidenceVersionNotSupportedException>(() =>
                AQGreenV2GraduationEvidenceReplay.Validate(
                    unsupported.Decision,
                    unsupported.Evidence,
                    unsupported.Evidence.Nodes.ToList(),
                    unsupported.Placements));

            var unsupportedPlacement = CreateGraph();
            typeof(AQGreenNetworkPlacement)
                .GetProperty(nameof(AQGreenNetworkPlacement.RulesVersion))
                .SetValue(
                    unsupportedPlacement.Placements[0],
                    "unsupported-placement-version");
            Should.Throw<AQGreenGraduationEvidenceReplayException>(() =>
                AQGreenV2GraduationEvidenceReplay.Validate(
                    unsupportedPlacement.Decision,
                    unsupportedPlacement.Evidence,
                    unsupportedPlacement.Evidence.Nodes.ToList(),
                    unsupportedPlacement.Placements));

        }

        [Fact]
        public void Replay_RejectsUnsupportedGraduationRulesVersion()
        {
            var unsupportedGraduation = CreateGraph();
            typeof(OnyxGraduationDecision)
                .GetProperty(nameof(OnyxGraduationDecision.GraduationRulesVersion))
                .SetValue(
                    unsupportedGraduation.Decision,
                    "unsupported-graduation-version");
            var exception = Should.Throw<
                AQGreenGraduationEvidenceVersionNotSupportedException>(() =>
                AQGreenV2GraduationEvidenceReplay.Validate(
                    unsupportedGraduation.Decision,
                    unsupportedGraduation.Evidence,
                    unsupportedGraduation.Evidence.Nodes.ToList(),
                    unsupportedGraduation.Placements));
            exception.Message.ShouldContain("graduation rules");
            exception.Message.ShouldContain("unsupported-graduation-version");
        }

        [Fact]
        public void Capture_RejectsAValidLookingLevelWithoutTheCompleteManifest()
        {
            var graph = CreateGraph();
            var incomplete = new AQGreenGraduationStructuralEvidenceResult(
                graph.Decision.EntryParticipationId,
                graph.Evidence.Nodes.First().SourcePlacementId,
                graph.Decision.DecidedAt,
                AQGreenStructuralCompletionLevel.Level2,
                5,
                25,
                AQGreenStructuralQualificationRules.CurrentVersion,
                Observations(graph, 30));

            Should.Throw<AQGreenPlacementTopologyIntegrityException>(() =>
                AQGreenV2GraduationEvidence.Capture(graph.Decision, incomplete));

            var unsupportedVersion = new AQGreenGraduationStructuralEvidenceResult(
                graph.Decision.EntryParticipationId,
                graph.Placements[0].PlacementTreeScopeId,
                graph.Decision.DecidedAt,
                AQGreenStructuralCompletionLevel.Level2,
                5,
                25,
                "unsupported-structural-version",
                Observations(graph, 31));
            Should.Throw<AQGreenGraduationEvidenceVersionNotSupportedException>(() =>
                AQGreenV2GraduationEvidence.Capture(
                    graph.Decision,
                    unsupportedVersion));
        }

        private static List<AQGreenGraduationStructuralEvidenceObservation> Observations(
            EvidenceGraph graph,
            int count) => graph.Evidence.Nodes.Take(count).Select(node =>
                new AQGreenGraduationStructuralEvidenceObservation
                {
                    CanonicalOrdinal = node.CanonicalOrdinal,
                    SourcePlacementId = node.SourcePlacementId,
                    ParticipationStatusObserved = node.ParticipationStatusObserved,
                    ParticipationActivatedAtObserved =
                        node.ParticipationActivatedAtObserved,
                    ParticipationIsDeletedObserved = node.ParticipationIsDeletedObserved,
                    CustomerIdObserved = node.CustomerIdObserved,
                    CustomerTenantMatchedObserved =
                        node.CustomerTenantMatchedObserved,
                    CustomerIsActiveObserved = node.CustomerIsActiveObserved,
                    CustomerIsDeletedObserved = node.CustomerIsDeletedObserved,
                    UserIdObserved = node.UserIdObserved,
                    UserTenantMatchedObserved = node.UserTenantMatchedObserved,
                    UserIsActiveObserved = node.UserIsActiveObserved,
                    UserIsDeletedObserved = node.UserIsDeletedObserved
                }).ToList();

        private static EvidenceGraph CreateGraph()
        {
            var participations = CreateLevelTwoRecruiterNetwork();
            var root = participations[0];
            var loan = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                root,
                participations,
                new EntryNetworkQualificationEvaluator(),
                LoanTerms,
                EffectiveFrom.AddMinutes(4));
            loan.AcceptByMember(1010, "I accept the Onyx loan terms.",
                EffectiveFrom.AddMinutes(5));
            loan.ApproveByAdministrator(900, EffectiveFrom.AddMinutes(6));
            var decidedAt = EffectiveFrom.AddMinutes(8);
            var onyx = OnyxParticipation.GraduateFromAQGreenIndependently(
                root,
                loan,
                7,
                OnyxPlanTerms.FromCanonicalAcceptedAgreement(loan),
                decidedAt);

            var scope = AQGreenPlacementTreeScope.Create(1);
            var placements = new List<AQGreenNetworkPlacement>();
            var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                scope,
                root.Id,
                EffectiveFrom.AddMinutes(3),
                AQGreenPlacementRules.CurrentVersion);
            placements.Add(rootPlacement);
            var depthOne = new List<AQGreenNetworkPlacement>();
            for (var index = 0; index < 5; index++)
            {
                var placement = AQGreenNetworkPlacement.CreateChild(
                    rootPlacement,
                    participations[index + 1].Id,
                    index + 1,
                    EffectiveFrom.AddMinutes(3),
                    AQGreenPlacementRules.CurrentVersion);
                placements.Add(placement);
                depthOne.Add(placement);
            }

            var participantIndex = 6;
            foreach (var parent in depthOne)
            {
                for (var slot = 1; slot <= 5; slot++)
                {
                    placements.Add(AQGreenNetworkPlacement.CreateChild(
                        parent,
                        participations[participantIndex++].Id,
                        slot,
                        EffectiveFrom.AddMinutes(3),
                        AQGreenPlacementRules.CurrentVersion));
                }
            }

            var observations = placements.Select((placement, ordinal) =>
            {
                var participation = participations.Single(item =>
                    item.Id == placement.ParticipantId);
                return new AQGreenGraduationStructuralEvidenceObservation
                {
                    CanonicalOrdinal = ordinal,
                    SourcePlacementId = placement.Id,
                    ParticipationStatusObserved = participation.Status,
                    ParticipationActivatedAtObserved = participation.ActivatedAt,
                    ParticipationIsDeletedObserved = false,
                    CustomerIdObserved = participation.CustomerId,
                    CustomerTenantMatchedObserved = true,
                    CustomerIsActiveObserved = true,
                    CustomerIsDeletedObserved = false,
                    UserIdObserved = participation.CustomerId + 1000L,
                    UserTenantMatchedObserved = true,
                    UserIsActiveObserved = true,
                    UserIsDeletedObserved = false
                };
            }).ToList();
            var structural = new AQGreenGraduationStructuralEvidenceResult(
                root.Id,
                scope.Id,
                decidedAt,
                AQGreenStructuralCompletionLevel.Level2,
                5,
                25,
                AQGreenStructuralQualificationRules.CurrentVersion,
                observations);
            var decision = OnyxGraduationDecision.RecordPlacementV2Approval(
                root,
                loan,
                onyx,
                structural,
                900,
                "Placement V2 Level 2 verified.",
                decidedAt);
            var evidence = AQGreenV2GraduationEvidence.Capture(decision, structural);
            return new EvidenceGraph(decision, loan, evidence, placements);
        }

        private static List<EntryParticipation> CreateLevelTwoRecruiterNetwork()
        {
            var root = EntryParticipation.StartIndependently(
                1,
                10,
                EntryTerms,
                EffectiveFrom);
            Activate(root);
            var all = new List<EntryParticipation> { root };
            var parents = new List<EntryParticipation> { root };
            for (var depth = 1; depth <= 2; depth++)
            {
                var children = new List<EntryParticipation>();
                foreach (var parent in parents)
                {
                    for (var slot = 1; slot <= 5; slot++)
                    {
                        var customerId = parent.CustomerId * 10 + slot;
                        var child = EntryParticipation.StartUnderRecruiter(
                            1,
                            customerId,
                            parent,
                            EntryTerms,
                            EffectiveFrom);
                        Activate(child);
                        all.Add(child);
                        children.Add(child);
                    }
                }

                parents = children;
            }

            return all;
        }

        private static void Activate(EntryParticipation participation)
        {
            participation.ApplyConfirmedActivationPayment(Payment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                "registration"));
            participation.ApplyConfirmedActivationPayment(Payment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                "activation"));
            participation.ApproveByAdministrator(900, EffectiveFrom.AddMinutes(3));
        }

        private static MemberPayment Payment(
            int customerId,
            MemberPaymentPurpose purpose,
            string suffix)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                600m,
                "Yoco",
                $"{suffix}-{customerId}",
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            return payment;
        }

        private sealed record EvidenceGraph(
            OnyxGraduationDecision Decision,
            OnyxLoanAgreement Loan,
            AQGreenV2GraduationEvidence Evidence,
            IReadOnlyList<AQGreenNetworkPlacement> Placements);
    }
}
