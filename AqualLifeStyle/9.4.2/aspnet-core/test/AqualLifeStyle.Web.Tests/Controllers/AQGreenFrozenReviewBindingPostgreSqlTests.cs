using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public sealed partial class AQGreenExistingMemberDiscoveryPostgreSqlTests
    {
        [DiscoveryPostgreSqlFact]
        public async Task ReviewBinding_ValidAcceptance_ExactReplay_DeterministicReceipt_WithoutBusinessWrites()
        {
            var source = await ReviewSourceAsync();
            var inventory = ReviewInventory(source);
            var (decisions, authorities) = ReviewDecisions(source, inventory);
            var before = await BusinessSnapshotAsync();
            var sourceBytes = AQGreenB6CanonicalEvidence.Bytes(source);
            var result = AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, authorities);
            result.ReviewCoverage.ShouldBe("COMPLETE"); result.PopulationApprovalState.ShouldBe("ACCEPTED");
            result.Components.Single().EffectiveDisposition.ShouldBe("ACCEPTED");
            result.Populations.Count.ShouldBe(3); result.Populations.ShouldAllBe(p => p.EffectiveDisposition == "ACCEPTED");
            result.EvidenceClassification.ShouldBe("OFFLINE_REVIEW_BINDING_ONLY");
            result.ReceiptDigest.ShouldBe(AQGreenB6CanonicalEvidence.Digest(result, "receiptDigest"));
            var replay = AQGreenFrozenReconciliationReview.Process(Clone(source), Clone(inventory), decisions.AsEnumerable().Reverse().ToArray(), authorities.AsEnumerable().Reverse().ToArray());
            AQGreenB6CanonicalEvidence.Bytes(result).ShouldBe(AQGreenB6CanonicalEvidence.Bytes(replay));
            AQGreenFrozenReconciliationReview.VerifyReceipt(source, inventory, decisions, authorities, Clone(result));
            var changedDecision = Clone(decisions); changedDecision[0].Disposition = "QUARANTINED";
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.VerifyReceipt(source, inventory, changedDecision, authorities, result))
                .Message.ShouldBe("REVIEW_RECEIPT_DECISION_CONFLICT");
            var forged = Clone(result); forged.ReviewCoverage = "PARTIAL";
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.VerifyReceipt(source, inventory, decisions, authorities, forged))
                .Message.ShouldBe("INVALID_REVIEW_RECEIPT_DIGEST");
            var b6AssemblyPath = Environment.GetEnvironmentVariable("AQGREEN_B6_CANONICAL_TOOL_ASSEMBLY");
            if (!string.IsNullOrEmpty(b6AssemblyPath))
            {
                // Exercise existing offline B6 encoding tooling, without invoking
                // its operational extraction/analysis/application entry points.
                var assembly = System.Reflection.Assembly.LoadFrom(b6AssemblyPath);
                var tuples = assembly.GetType("AqualLifeStyle.B6SnapshotRunner.B6Digests").GetMethod("Tuples");
                var memberTuples = source.Records.Select(r => new[] { r.DiscoveryId, r.RecordDigest });
                ((string)tuples.Invoke(null, new object[] { memberTuples })).ShouldBe(inventory.Components.Single().MembershipDigest);
                var json = assembly.GetType("AqualLifeStyle.B6SnapshotRunner.CanonicalJson").GetMethod("Bytes");
                ((byte[])json.Invoke(null, new object[] { System.Text.Json.Nodes.JsonNode.Parse(AQGreenB6CanonicalEvidence.Bytes(result)) }))
                    .ShouldBe(AQGreenB6CanonicalEvidence.Bytes(result));
            }
            AQGreenB6CanonicalEvidence.Bytes(source).ShouldBe(sourceBytes);
            (await BusinessSnapshotAsync()).ShouldBe(before);
            var path = Environment.GetEnvironmentVariable("AQGREEN_SYNTHETIC_REVIEW_ARTIFACTS");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.Directory.CreateDirectory(path);
                await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(path, "review-receipt.json"), AQGreenB6CanonicalEvidence.Bytes(result));
                await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(path, "review-inventory.json"), AQGreenB6CanonicalEvidence.Bytes(inventory));
                await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(path, "review-decisions.json"), AQGreenB6CanonicalEvidence.Bytes(decisions));
            }
        }

        [DiscoveryPostgreSqlFact]
        public async Task ReviewBinding_RejectsStaleScopeIdentityMembershipVersionsAuthorityAndConflictingDecisions()
        {
            var source = await ReviewSourceAsync(); var inventory = ReviewInventory(source);
            var (decisions, authorities) = ReviewDecisions(source, inventory);
            var before = await BusinessSnapshotAsync();
            var invalid = new Action<AQGreenFrozenReviewDecision>[]
            {
                d => d.Binding.SourceSnapshotDigest = new string('a', 64),
                d => d.Binding.BaseManifestDigest = new string('a', 64),
                d => d.Binding.TenantSnapshotKey++,
                d => d.Binding.PopulationScope = "ALL_TENANTS",
                d => d.Binding.PopulationScopeDigest = new string('a', 64),
                d => d.Binding.EvidenceVersion = "unsupported",
                d => d.Binding.ReviewContractVersion = "unsupported",
                d => d.InventoryDigest = new string('a', 64),
                d => d.SubjectId = new string('a', 64),
                d => d.EvidenceDigest = new string('a', 64),
                d => d.ReviewedByRole = "self-declared",
                d => d.AuthorityEvidenceDigest = new string('a', 64),
                d => d.AuthorityReference = "UNKNOWN_AUTHORITY",
                d => d.ReviewedAt = source.Manifest.SourceBoundary.SourceObservedAtUtc.AddDays(2),
                d => d.Disposition = "APPROVED_FOR_EXECUTION",
                d => d.ReasonCodes = Array.Empty<string>()
            };
            foreach (var mutate in invalid)
            {
                var changed = Clone(decisions); mutate(changed[0]);
                Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(source, inventory, changed, authorities));
            }
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, null)).Message.ShouldBe("MISSING_REVIEWER_AUTHORITY");
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, Array.Empty<AQGreenFrozenReviewerAuthority>()));
            var wrongAuthority = Clone(authorities); wrongAuthority[0].Binding.TenantSnapshotKey++;
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, wrongAuthority)).Message.ShouldBe("REVIEWER_AUTHORITY_SCOPE_MISMATCH");
            var conflict = Clone(decisions[0]); conflict.Disposition = "QUARANTINED"; conflict.DecisionReference = "SYNTHETIC_CONFLICT";
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(source, inventory, decisions.Append(conflict).ToArray(), authorities)).Message.ShouldBe("DUPLICATED_OR_CONFLICTING_DECISION");
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, authorities.Append(authorities[0]).ToArray())).Message.ShouldBe("MISSING_OR_AMBIGUOUS_REVIEWER_AUTHORITY");
            // Even another observation of identical source content is a different package.
            var changedPackage = Clone(source);
            changedPackage.Manifest.SourceBoundary.SourceObservedAtUtc = changedPackage.Manifest.SourceBoundary.SourceObservedAtUtc.AddTicks(1);
            changedPackage = AQGreenReconciliationEvidencePackage.Freeze(changedPackage.Manifest, changedPackage.Records);
            changedPackage.Manifest.OverallSnapshotDigest.ShouldBe(source.Manifest.OverallSnapshotDigest);
            changedPackage.Manifest.ManifestHash.ShouldNotBe(source.Manifest.ManifestHash);
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(changedPackage, inventory, decisions, authorities));
            changedPackage = Clone(source); changedPackage.Records[0].HistoricalEvidenceIssues = new[] { "JOINING_PAYMENT_LINKS_CONFLICT" };
            changedPackage = AQGreenReconciliationEvidencePackage.Freeze(changedPackage.Manifest, changedPackage.Records);
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(changedPackage, inventory, decisions, authorities));
            var populations = Clone(inventory.Populations); populations[0].IncludedDiscoveryIds = Array.Empty<string>();
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.SealInventory(source, inventory.Components, populations)).Message.ShouldBe("OMITTED_OR_DUPLICATE_POPULATION_MEMBERSHIP");
            var duplicate = Clone(inventory.Components[0]); duplicate.ComponentDigest = new string('a', 64);
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.SealInventory(source, inventory.Components.Append(duplicate).ToArray(), inventory.Populations)).Message.ShouldBe("DUPLICATE_OR_INVALID_COMPONENT_MEMBERSHIP");
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task ReviewBinding_PreservesUnknowns_QuarantinesDependants_ExposesPartialAndCyclicReviews()
        {
            var source = await ReviewSourceAsync(legacyMembers: 2);
            var inventory = ReviewInventory(source);
            var cleanDigest = inventory.Components.Single(c => c.MemberDiscoveryIds.Contains(source.Records[0].DiscoveryId)).ComponentDigest;
            var legacyDigest = inventory.Components.Single(c => c.MemberDiscoveryIds.Contains(source.Records[1].DiscoveryId)).ComponentDigest;
            inventory.Components.Single(c => c.ComponentDigest == cleanDigest).DependentComponentDigests = new[] { legacyDigest };
            inventory = AQGreenFrozenReconciliationReview.SealInventory(source, inventory.Components, inventory.Populations);
            var (decisions, authorities) = ReviewDecisions(source, inventory);
            var legacyComponent = inventory.Components.Single(c => c.MemberDiscoveryIds.Contains(source.Records[1].DiscoveryId));
            decisions.Single(d => d.SubjectId == legacyComponent.ComponentDigest).Disposition = "QUARANTINED";
            var before = await BusinessSnapshotAsync(); var bytes = AQGreenB6CanonicalEvidence.Bytes(source);
            var result = AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, authorities);
            result.PopulationApprovalState.ShouldBe("QUARANTINED"); result.ReviewCoverage.ShouldBe("COMPLETE");
            result.Components.ShouldContain(c => c.HistoricalEvidenceIssues.Any(i => i.EndsWith(":SPONSOR_HISTORY_UNVERIFIABLE")));
            result.Components.ShouldContain(c => c.RequestedDisposition == "ACCEPTED" && c.EffectiveDisposition == "QUARANTINED");
            result.Components.Single(c => c.SubjectId == cleanDigest).ReasonCodes.ShouldContain("DEPENDENT_COMPONENT_NOT_ACCEPTED");
            result.Populations.ShouldAllBe(p => p.EffectiveDisposition == "QUARANTINED");
            var partial = AQGreenFrozenReconciliationReview.Process(source, inventory, decisions.Skip(1).ToArray(), authorities);
            partial.ReviewCoverage.ShouldBe("PARTIAL"); partial.PopulationApprovalState.ShouldBe("QUARANTINED");
            partial.Components.Concat(partial.Populations).ShouldContain(c => c.RequestedDisposition == "UNREVIEWED" && c.ReasonCodes.Contains("OMITTED_REVIEW_DECISION"));
            var omittedInventory = AQGreenFrozenReconciliationReview.SealInventory(source, inventory.Components.Take(1).Select(c => new AQGreenFrozenReviewComponent
                { ComponentDigest = c.ComponentDigest, MemberDiscoveryIds = c.MemberDiscoveryIds, DependentComponentDigests = Array.Empty<string>() }).ToArray(), inventory.Populations);
            var (omittedDecisions, omittedAuthorities) = ReviewDecisions(source, omittedInventory);
            var omitted = AQGreenFrozenReconciliationReview.Process(source, omittedInventory, omittedDecisions, omittedAuthorities);
            omitted.OmittedDiscoveryIds.Length.ShouldBe(2); omitted.ReviewCoverage.ShouldBe("PARTIAL");
            omitted.PopulationApprovalState.ShouldBe("QUARANTINED");
            inventory.Components.Single(c => c.ComponentDigest == legacyDigest).DependentComponentDigests = new[] { cleanDigest };
            inventory = AQGreenFrozenReconciliationReview.SealInventory(source, inventory.Components, inventory.Populations);
            (decisions, authorities) = ReviewDecisions(source, inventory);
            var cyclic = AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, authorities);
            cyclic.Components.Count(c => c.ReasonCodes.Contains("DEPENDENCY_CYCLE")).ShouldBe(2);
            AQGreenB6CanonicalEvidence.Bytes(source).ShouldBe(bytes); (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        private async Task<AQGreenReconciliationEvidencePackage> ReviewSourceAsync(int legacyMembers = 0)
        {
            var fixture = await SeedAsync(central: true, allTenants: true, exportPermission: true);
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fixture.Foreign.CustomerId);
                var area = await context.Areas.IgnoreQueryFilters().SingleAsync(a => a.Id == customer.AreaId);
                var legacyMembershipId = customer.MembershipId; customer.ChangeMembership(null);
                var entry = EntryParticipation.StartIndependently(fixture.ForeignTenantId, customer.Id, Terms, RecordedAt);
                var payment = MemberPayment.CreatePending(fixture.ForeignTenantId, customer.Id, MemberPaymentPurpose.AQGreenJoining,
                    1200m, "Synthetic", "review-binding-" + Guid.NewGuid().ToString("N"), RecordedAt);
                payment.Confirm(RecordedAt.AddMinutes(1)); entry.ApplyConfirmedJoiningPayment(payment);
                entry.ApproveByAdministrator(fixture.Reviewer.Id, RecordedAt.AddMinutes(2));
                context.EntryParticipations.Add(entry); context.MemberPayments.Add(payment); await context.SaveChangesAsync();
                var attribution = AQGreenRecruitmentAttribution.Create(fixture.ForeignTenantId, entry.Id, null,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot, AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    Guid.NewGuid(), RecordedAt, fixture.Reviewer.Id, "Synthetic review source", AQGreenRecruitmentAttributionRules.CurrentVersion);
                context.AQGreenRecruitmentAttributions.Add(attribution); await context.SaveChangesAsync();
                context.AQGreenRecruitmentAttributionConfirmations.Add(AQGreenRecruitmentAttributionConfirmation.Confirm(attribution,
                    RecordedAt, fixture.Reviewer.Id, AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation,
                    attribution.SourceReferenceId, AQGreenRecruitmentAttributionRules.CurrentVersion));
                var scope = AQGreenPlacementTreeScope.Create(fixture.ForeignTenantId); context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateRoot(scope, entry.Id, entry.ActivatedAt.Value, AQGreenPlacementRules.CurrentVersion));
                await context.SaveChangesAsync();
                for (var i = 0; i < legacyMembers; i++) await SubjectAsync(context, area, legacyMembershipId, false, false, fixture.Reviewer.Id);
            });
            await SignInAsync(fixture.Reviewer);
            var source = await ExportAsync(new ExportLegacyAQGreenReconciliationEvidenceInput
            { TenantId = fixture.ForeignTenantId, WholeTenant = true, AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) });
            source.Records[0].HistoricalEvidenceIssues.ShouldBeEmpty();
            return source;
        }
        private static AQGreenFrozenReviewInventory ReviewInventory(AQGreenReconciliationEvidencePackage source) =>
            AQGreenFrozenReconciliationReview.SealInventory(source, source.Records.Select(r => new AQGreenFrozenReviewComponent
            {
                // Synthetic B6-style component identities, not inferred legacy roots.
                ComponentDigest = AQGreenB6CanonicalEvidence.Tuples(new[] { new[] { r.DiscoveryId, r.RecordDigest } }),
                MemberDiscoveryIds = new[] { r.DiscoveryId }, DependentComponentDigests = Array.Empty<string>()
            }).ToArray(), AQGreenFrozenReconciliationReview.PopulationNames.Select(name => new AQGreenFrozenReviewPopulation
            {
                Name = name, IncludedDiscoveryIds = source.Records.Where(r => r.HistoricalEvidenceIssues.Length == 0).Select(r => r.DiscoveryId).ToArray(),
                ExcludedDiscoveryIds = Array.Empty<string>(), UnverifiableDiscoveryIds = source.Records.Where(r => r.HistoricalEvidenceIssues.Length > 0).Select(r => r.DiscoveryId).ToArray()
            }).ToArray());
        private static (List<AQGreenFrozenReviewDecision>, List<AQGreenFrozenReviewerAuthority>) ReviewDecisions(
            AQGreenReconciliationEvidencePackage source, AQGreenFrozenReviewInventory inventory)
        {
            var subjects = inventory.Components.Select(c => (Type: "COMPONENT", Id: c.ComponentDigest, Digest: c.MembershipDigest))
                .Concat(inventory.Populations.Select(p => (Type: "POPULATION", Id: p.Name, Digest: p.MembershipDigest))).ToArray();
            var decisions = new List<AQGreenFrozenReviewDecision>(); var authorities = new List<AQGreenFrozenReviewerAuthority>();
            for (var i = 0; i < subjects.Length; i++)
            {
                var s = subjects[i]; var reference = "SYNTHETIC_DECISION:" + i; var authority = "SYNTHETIC_AUTHORITY:" + i;
                var evidenceDigest = AQGreenB6CanonicalEvidence.Hash(Encoding.UTF8.GetBytes(authority + ":" + inventory.InventoryDigest));
                decisions.Add(new AQGreenFrozenReviewDecision { Binding = Clone(inventory.Binding), InventoryDigest = inventory.InventoryDigest,
                    SubjectType = s.Type, SubjectId = s.Id, EvidenceType = AQGreenFrozenReconciliationReview.Version, EvidenceDigest = s.Digest,
                    Disposition = "ACCEPTED", ReasonCodes = new[] { "SYNTHETIC_REVIEW_ONLY" }, DecisionReference = reference,
                    ReviewedAt = source.Manifest.SourceBoundary.SourceObservedAtUtc.AddMinutes(1), ReviewedByRole = AQGreenFrozenReconciliationReview.ReviewerRole,
                    AuthorityReference = authority, AuthorityEvidenceDigest = evidenceDigest });
                // Explicitly synthetic independently supplied trusted authority fixture.
                authorities.Add(new AQGreenFrozenReviewerAuthority { Binding = Clone(inventory.Binding), InventoryDigest = inventory.InventoryDigest,
                    SubjectType = s.Type, SubjectId = s.Id, DecisionReference = reference, ReviewedByRole = AQGreenFrozenReconciliationReview.ReviewerRole,
                    AuthorityReference = authority, EvidenceType = "SyntheticOwnerReviewAuthorityV1", EvidenceDigest = evidenceDigest,
                    AuthorizedAt = source.Manifest.SourceBoundary.SourceObservedAtUtc, ExpiresAt = source.Manifest.SourceBoundary.SourceObservedAtUtc.AddHours(1) });
            }
            return (decisions, authorities);
        }
        private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(AQGreenB6CanonicalEvidence.Bytes(value), AQGreenB6CanonicalEvidence.Options);
    }
}
