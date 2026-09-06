using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public sealed class AQGreenLegacyCanonicalisationSimulatorTests
    {
        private static readonly DateTime EffectiveAt =
            new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ScopeId =
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid RootId =
            Guid.Parse("10000000-0000-0000-0000-000000000000");

        [Fact]
        public void RepeatRun_IsLogicallyAndByteDigestDeterministic()
        {
            var candidates = BuildOverflowCandidates().OrderByDescending(row => row.ParticipantSnapshotKey).ToList();
            var request = Request(candidates);

            var first = AQGreenLegacyCanonicalisationSimulator.Simulate(request);
            var second = AQGreenLegacyCanonicalisationSimulator.Simulate(request);

            first.IsCanonicalisable.ShouldBeTrue();
            second.IsCanonicalisable.ShouldBeTrue();
            first.ManifestDigest.ShouldBe(second.ManifestDigest);
            Project(first).ShouldBe(Project(second));
            first.StructuralImpacts.Select(ProjectImpact)
                .ShouldBe(second.StructuralImpacts.Select(ProjectImpact));
        }

        [Fact]
        public void MoreThanFiveLegacyChildren_UsesRealSponsorLocalOverflowWithoutChangingCredit()
        {
            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(
                Request(BuildOverflowCandidates().ToList()));

            result.IsCanonicalisable.ShouldBeTrue();
            var children = result.Placements.Skip(1).ToList();
            var firstFive = children.Take(5).ToList();
            firstFive.All(row => row.PlacementParentSnapshotKey == RootId).ShouldBeTrue();
            firstFive.Select(row => row.PlacementSlot).ShouldBe(new int?[] { 1, 2, 3, 4, 5 });

            var overflow = children.Skip(5).ToList();
            var firstChild = firstFive[0].ParticipantSnapshotKey;
            overflow.Select(row => row.PlacementParentSnapshotKey)
                .ShouldBe(new Guid?[] { firstChild, firstChild, firstChild });
            overflow.Select(row => row.PlacementSlot).ShouldBe(new int?[] { 1, 2, 3 });
            overflow.All(row => row.LegacyCreditedSponsorSnapshotKey == RootId).ShouldBeTrue();
            overflow.All(row => row.PlacementSource == "LegacyCanonicalisation").ShouldBeTrue();
        }

        [Fact]
        public void EqualOrderingTimes_UseExtractionDerivedOrdinalWithoutProductionIds()
        {
            var laterDefaultGuid = Guid.Parse("01000000-0000-0000-0000-000000000000");
            var earlierCanonicalGuid = Guid.Parse("00000001-0000-0000-0000-000000000000");
            var candidates = new[]
            {
                Root(),
                Candidate(laterDefaultGuid, RootId, EffectiveAt.AddMinutes(1), orderingOrdinal: 2),
                Candidate(earlierCanonicalGuid, RootId, EffectiveAt.AddMinutes(1), orderingOrdinal: 1)
            };

            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(Request(candidates));

            result.IsCanonicalisable.ShouldBeTrue();
            result.Placements[1].ParticipantSnapshotKey.ShouldBe(earlierCanonicalGuid);
            result.Placements[1].PlacementSlot.ShouldBe(1);
            result.Placements[2].ParticipantSnapshotKey.ShouldBe(laterDefaultGuid);
            result.Placements[2].PlacementSlot.ShouldBe(2);
        }

        [Fact]
        public void MixedEndianGuidVector_UsesActualDotNetGuidComparatorOrdinal()
        {
            var earlier = Guid.Parse("00000000-0001-0100-8000-000000000001");
            var later = Guid.Parse("00000000-0100-0001-8000-000000000001");
            earlier.CompareTo(later).ShouldBeLessThan(0);
            // Guid.ToByteArray reverses the first three fields; a raw-byte intuition
            // would put this pair in the opposite order.
            earlier.ToByteArray().AsSpan(0, 8).SequenceCompareTo(
                later.ToByteArray().AsSpan(0, 8)).ShouldBeGreaterThan(0);

            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(Request(new[]
            {
                Root(),
                Candidate(earlier, RootId, EffectiveAt.AddMinutes(1), orderingOrdinal: 2),
                Candidate(later, RootId, EffectiveAt.AddMinutes(1), orderingOrdinal: 3)
            }));

            result.IsCanonicalisable.ShouldBeTrue();
            result.Placements[1].ParticipantSnapshotKey.ShouldBe(earlier);
            result.Placements[2].ParticipantSnapshotKey.ShouldBe(later);
        }

        [Fact]
        public void MultipleRoots_AreAnExceptionAndProduceNoTopology()
        {
            var candidates = new[]
            {
                Root(),
                Root(Guid.Parse("20000000-0000-0000-0000-000000000000"))
            };

            AssertAnomaly(candidates, AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot);
        }

        [Fact]
        public void MissingRoot_IsAnExceptionAndProducesNoTopology()
        {
            AssertAnomaly(
                new[] { Candidate(Guid.Parse("20000000-0000-0000-0000-000000000000"), RootId, EffectiveAt) },
                AQGreenLegacyCanonicalisationAnomalyCode.MissingRootEvidence);
        }

        [Fact]
        public void MissingSponsor_IsAnExceptionAndProducesNoTopology()
        {
            AssertAnomaly(
                new[]
                {
                    Root(),
                    Candidate(Guid.Parse("20000000-0000-0000-0000-000000000000"), null, EffectiveAt)
                },
                AQGreenLegacyCanonicalisationAnomalyCode.MissingSponsor);
        }

        [Fact]
        public void MissingOrderingEvidence_IsAnExceptionAndProducesNoTopology()
        {
            AssertAnomaly(
                new[]
                {
                    Root(),
                    Candidate(Guid.Parse("20000000-0000-0000-0000-000000000000"), RootId, null)
                },
                AQGreenLegacyCanonicalisationAnomalyCode.MissingOrderingEvidence);
        }

        [Fact]
        public void Cycle_IsAnExceptionAndProducesNoTopology()
        {
            var a = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var b = Guid.Parse("30000000-0000-0000-0000-000000000000");
            AssertAnomaly(
                new[] { Root(), Candidate(a, b, EffectiveAt), Candidate(b, a, EffectiveAt) },
                AQGreenLegacyCanonicalisationAnomalyCode.Cycle);
        }

        [Fact]
        public void DanglingSponsor_IsAnExceptionAndProducesNoTopology()
        {
            AssertAnomaly(
                new[]
                {
                    Root(),
                    Candidate(
                        Guid.Parse("20000000-0000-0000-0000-000000000000"),
                        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                        EffectiveAt)
                },
                AQGreenLegacyCanonicalisationAnomalyCode.DanglingSponsor);
        }

        [Fact]
        public void CrossTenantCandidate_IsAnExceptionAndProducesNoTopology()
        {
            var candidate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                tenantId: 2);
            AssertAnomaly(
                new[] { Root(), candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.CrossTenant);
        }

        [Fact]
        public void DuplicateCandidate_IsAnExceptionAndProducesNoTopology()
        {
            var duplicate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt);
            AssertAnomaly(
                new[] { Root(), duplicate, duplicate },
                AQGreenLegacyCanonicalisationAnomalyCode.DuplicateParticipation);
        }

        [Fact]
        public void LegacyUnknownAcquisition_IsAllowedWhenSponsorAndRootAreDeterministic()
        {
            var candidate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                acquisitionProvenance: AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition);
            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(
                Request(new[] { Root(), candidate }));
            result.IsCanonicalisable.ShouldBeTrue();
        }

        [Fact]
        public void SponsorUncertainty_IsNotTreatedAsUnknownAcquisition()
        {
            var candidate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                sponsorEvidenceStatus: AQGreenLegacySponsorEvidenceStatus.AmbiguousSponsor);
            AssertAnomaly(
                new[] { Root(), candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty);
        }

        [Fact]
        public void RootCandidate_IsNotAnAcceptedRootWithoutReviewedDisposition()
        {
            var candidate = Root(rootEvidenceStatus: AQGreenLegacyRootEvidenceStatus.RootCandidate);
            AssertAnomaly(
                new[] { candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.MissingRootEvidence);
        }

        [Fact]
        public void LegacyUnknownAcquisitionAndNullSponsor_DoNotAcceptRoot()
        {
            var candidate = Root(
                acquisitionProvenance: AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition,
                rootEvidenceStatus: AQGreenLegacyRootEvidenceStatus.RootCandidate);
            AssertAnomaly(
                new[] { candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.MissingRootEvidence);
        }

        [Fact]
        public void SponsorAndRootUncertainty_AreDistinctTypedFailures()
        {
            var uncertainRoot = Root(
                rootEvidenceStatus: AQGreenLegacyRootEvidenceStatus.AmbiguousRoot);
            var uncertainSponsor = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                sponsorEvidenceStatus: AQGreenLegacySponsorEvidenceStatus.AmbiguousSponsor);

            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(
                Request(new[] { uncertainRoot, uncertainSponsor }));

            result.Anomalies.Select(value => value.Code)
                .ShouldContain(AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot);
            result.Anomalies.Select(value => value.Code)
                .ShouldContain(AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty);
        }

        [Fact]
        public void UnsupportedAcquisitionProvenance_IsNotSponsorOrRootUncertainty()
        {
            var candidate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                acquisitionProvenance: (AQGreenLegacyAcquisitionProvenance)99);

            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(
                Request(new[] { Root(), candidate }));

            result.Anomalies.Select(value => value.Code)
                .ShouldContain(AQGreenLegacyCanonicalisationAnomalyCode.UnsupportedAcquisitionProvenance);
            result.Anomalies.Select(value => value.Code)
                .ShouldNotContain(AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty);
            result.Anomalies.Select(value => value.Code)
                .ShouldNotContain(AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot);
        }

        [Fact]
        public void UnsupportedRootEvidence_IsAReportedFailureRatherThanAnException()
        {
            var candidate = Root(
                rootEvidenceStatus: (AQGreenLegacyRootEvidenceStatus)99);

            AssertAnomaly(
                new[] { candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.AmbiguousRoot);
        }

        [Fact]
        public void UnsupportedSponsorEvidence_IsAReportedFailureRatherThanAnException()
        {
            var candidate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                sponsorEvidenceStatus: (AQGreenLegacySponsorEvidenceStatus)99);

            AssertAnomaly(
                new[] { Root(), candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.SponsorEvidenceUncertainty);
        }

        [Fact]
        public void MissingOrderingTieBreakOrdinal_IsAnException()
        {
            var candidate = Candidate(
                Guid.Parse("20000000-0000-0000-0000-000000000000"),
                RootId,
                EffectiveAt,
                orderingOrdinal: null);
            AssertAnomaly(
                new[] { Root(), candidate },
                AQGreenLegacyCanonicalisationAnomalyCode.MissingOrderingTieBreakOrdinal);
        }

        [Fact]
        public void DuplicateOrderingTieBreakOrdinal_IsAnException()
        {
            AssertAnomaly(
                new[]
                {
                    Root(),
                    Candidate(Guid.Parse("20000000-0000-0000-0000-000000000000"), RootId, EffectiveAt, orderingOrdinal: 2),
                    Candidate(Guid.Parse("30000000-0000-0000-0000-000000000000"), RootId, EffectiveAt, orderingOrdinal: 2)
                },
                AQGreenLegacyCanonicalisationAnomalyCode.DuplicateOrderingTieBreakOrdinal);
        }

        [Fact]
        public void InvalidHistoricalStructuralEvidence_IsAnExceptionRatherThanAValidLookingLevel()
        {
            AssertAnomaly(
                new[]
                {
                    Root(effectiveV1Level: (AQGreenStructuralCompletionLevel)99)
                },
                AQGreenLegacyCanonicalisationAnomalyCode.InvalidStructuralEvidence);
        }

        [Fact]
        public void StructuralImpact_DistinguishesChangedAndAmbiguousWithoutTouchingHistory()
        {
            var candidates = new List<AQGreenLegacyCanonicalisationCandidate>
            {
                Root(effectiveV1Level: AQGreenStructuralCompletionLevel.Level1)
            };
            for (var index = 1; index <= 30; index++)
            {
                candidates.Add(Candidate(
                    Guid.Parse($"{index + 1:00000000}-0000-0000-0000-000000000000"),
                    RootId,
                    EffectiveAt.AddMinutes(index)));
            }

            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(Request(candidates));

            var rootImpact = result.StructuralImpacts.Single(row => row.ParticipantSnapshotKey == RootId);
            rootImpact.EffectiveV1StructuralLevel.ShouldBe(AQGreenStructuralCompletionLevel.Level1);
            rootImpact.SimulatedV2StructuralLevel.ShouldBe(AQGreenStructuralCompletionLevel.Level2);
            rootImpact.Classification.ShouldBe(AQGreenLegacyStructuralImpactClassification.Changed);
            result.StructuralImpacts
                .Where(row => row.ParticipantSnapshotKey != RootId)
                .ShouldAllBe(row =>
                    row.Classification == AQGreenLegacyStructuralImpactClassification.Ambiguous);
        }

        [Fact]
        public void SimulationSurface_ContainsNoPiiFields_AndDoesNotMutateInput()
        {
            var candidates = BuildOverflowCandidates().ToList();
            var before = candidates.Select(row => (
                row.ParticipantSnapshotKey,
                row.AcceptedLegacySponsorSnapshotKey,
                row.QualifiedUnderRecruiterAtUtc,
                row.LegacyOrderingTieBreakOrdinal,
                row.AcquisitionProvenance,
                row.SourceEvidenceDigest)).ToList();

            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(Request(candidates));

            var exposedNames = typeof(AQGreenLegacyCanonicalPlacementProposal)
                .GetProperties()
                .Select(property => property.Name)
                .ToList();
            exposedNames.ShouldNotContain("CustomerId");
            exposedNames.ShouldNotContain("Name");
            exposedNames.ShouldNotContain("Email");
            exposedNames.ShouldNotContain("PhoneNumber");
            exposedNames.ShouldNotContain("PaymentReference");
            before.ShouldBe(candidates.Select(row => (
                row.ParticipantSnapshotKey,
                row.AcceptedLegacySponsorSnapshotKey,
                row.QualifiedUnderRecruiterAtUtc,
                row.LegacyOrderingTieBreakOrdinal,
                row.AcquisitionProvenance,
                row.SourceEvidenceDigest)).ToList());
            result.IsCanonicalisable.ShouldBeTrue();
        }

        private static IReadOnlyList<string> Project(AQGreenLegacyCanonicalisationResult result) =>
            result.Placements.Select(row => string.Join("|",
                row.TenantSnapshotKey,
                row.PlacementTreeScopeSnapshotKey,
                row.ParticipantSnapshotKey,
                row.LegacyCreditedSponsorSnapshotKey,
                row.LegacyOrderingTieBreakOrdinal,
                row.CanonicalInputOrdinal,
                row.PlacementParentSnapshotKey,
                row.PlacementSlot,
                row.CanonicalPath,
                row.CanonicalPosition,
                row.PlacementSequence,
                row.PlacementSource,
                row.RulesVersion,
                row.SimulationVersion,
                row.ComparatorVersion,
                row.SourceEvidenceDigest)).ToList();

        private static string ProjectImpact(AQGreenLegacyStructuralImpact impact) =>
            string.Join("|",
                impact.ParticipantSnapshotKey,
                impact.LegacyOrderingTieBreakOrdinal,
                impact.EffectiveV1StructuralLevel,
                impact.SimulatedV2StructuralLevel,
                impact.Classification);

        private static void AssertAnomaly(
            IReadOnlyCollection<AQGreenLegacyCanonicalisationCandidate> candidates,
            AQGreenLegacyCanonicalisationAnomalyCode expected)
        {
            var result = AQGreenLegacyCanonicalisationSimulator.Simulate(Request(candidates));
            result.IsCanonicalisable.ShouldBeFalse();
            result.Placements.ShouldBeEmpty();
            result.ManifestDigest.ShouldBeNull();
            result.Anomalies.Select(anomaly => anomaly.Code).ShouldContain(expected);
        }

        private static AQGreenLegacyCanonicalisationRequest Request(
            IReadOnlyCollection<AQGreenLegacyCanonicalisationCandidate> candidates) =>
            new()
            {
                TenantSnapshotKey = 1,
                PlacementTreeScopeSnapshotKey = ScopeId,
                AnalysisSnapshotCutoffUtc = EffectiveAt,
                Candidates = candidates
            };

        private static IEnumerable<AQGreenLegacyCanonicalisationCandidate> BuildOverflowCandidates()
        {
            yield return Root();
            for (var index = 1; index <= 8; index++)
            {
                yield return Candidate(
                    Guid.Parse($"{index + 1:00000000}-0000-0000-0000-000000000000"),
                    RootId,
                    EffectiveAt.AddMinutes(index));
            }
        }

        private static AQGreenLegacyCanonicalisationCandidate Root(
            Guid? id = null,
            AQGreenStructuralCompletionLevel? effectiveV1Level = null,
            AQGreenLegacyAcquisitionProvenance acquisitionProvenance =
                AQGreenLegacyAcquisitionProvenance.Known,
            AQGreenLegacyRootEvidenceStatus rootEvidenceStatus =
                AQGreenLegacyRootEvidenceStatus.AcceptedRoot) =>
            new()
            {
                TenantSnapshotKey = 1,
                ParticipantSnapshotKey = id ?? RootId,
                LegacyOrderingTieBreakOrdinal = OrderingOrdinal(id ?? RootId),
                SponsorEvidenceStatus = AQGreenLegacySponsorEvidenceStatus.NotApplicableToRoot,
                RootEvidenceStatus = rootEvidenceStatus,
                AcquisitionProvenance = acquisitionProvenance,
                EffectiveV1StructuralLevel = effectiveV1Level,
                SourceEvidenceDigest = EvidenceDigest(id ?? RootId)
            };

        private static AQGreenLegacyCanonicalisationCandidate Candidate(
            Guid id,
            Guid? sponsor,
            DateTime? orderedAt,
            int tenantId = 1,
            AQGreenLegacyAcquisitionProvenance acquisitionProvenance =
                AQGreenLegacyAcquisitionProvenance.Known,
            long? orderingOrdinal = -1,
            AQGreenLegacySponsorEvidenceStatus sponsorEvidenceStatus =
                AQGreenLegacySponsorEvidenceStatus.AcceptedSponsor) =>
            new()
            {
                TenantSnapshotKey = tenantId,
                ParticipantSnapshotKey = id,
                AcceptedLegacySponsorSnapshotKey = sponsor,
                QualifiedUnderRecruiterAtUtc = orderedAt,
                LegacyOrderingTieBreakOrdinal = orderingOrdinal == -1
                    ? OrderingOrdinal(id)
                    : orderingOrdinal,
                SponsorEvidenceStatus = sponsorEvidenceStatus,
                RootEvidenceStatus = AQGreenLegacyRootEvidenceStatus.NotRoot,
                AcquisitionProvenance = acquisitionProvenance,
                SourceEvidenceDigest = EvidenceDigest(id)
            };

        private static long OrderingOrdinal(Guid id) =>
            Convert.ToInt64(id.ToString("N")[..8], 16) + 1;

        private static string EvidenceDigest(Guid id) =>
            id.ToString("N") + id.ToString("N");
    }
}
