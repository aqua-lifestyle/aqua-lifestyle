using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AqualLifeStyle.Domain.AQGreen
{
    // Adapter for discovery evidence, not a replacement for B6 FINAL_ANALYSIS,
    // root acceptance, authority verification, execution receipts or cutover.
    // Reuses B6 sourceSnapshotDigest/baseManifestDigest, componentDigest,
    // controlled decision references, reviewer role, B6TUPLE1 and receiptDigest.
    public static class AQGreenFrozenReconciliationReview
    {
        public const string Version = "AQGreenB6DiscoveryReviewBindingV1";
        public const string ReviewerRole = "AuthorisedB6RootReviewer";
        public static readonly string[] PopulationNames = { "canonicalisation", "qualificationEligibility", "historicalImpactInventory" };
        private static readonly Regex DigestPattern = new("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
        private static readonly Regex CodePattern = new("\\A[A-Za-z0-9._:+/@-]{1,128}\\z", RegexOptions.CultureInvariant);

        public static AQGreenFrozenReviewInventory SealInventory(AQGreenReconciliationEvidencePackage source,
            IReadOnlyCollection<AQGreenFrozenReviewComponent> components, IReadOnlyCollection<AQGreenFrozenReviewPopulation> populations)
        {
            source.Verify();
            Require(components != null && populations != null, "MISSING_REVIEW_INVENTORY");
            Require(components.All(c => c != null) && populations.All(p => p != null), "INVALID_REVIEW_INVENTORY_SUBJECT");
            // Take a detached copy so normalisation never mutates caller evidence.
            var inventory = System.Text.Json.JsonSerializer.Deserialize<AQGreenFrozenReviewInventory>(
                AQGreenB6CanonicalEvidence.Bytes(new AQGreenFrozenReviewInventory
                { Binding = Binding(source), Components = components.ToList(), Populations = populations.ToList() }), AQGreenB6CanonicalEvidence.Options);
            inventory.Components = inventory.Components.OrderBy(c => c.ComponentDigest, StringComparer.Ordinal).ToList();
            inventory.Populations = inventory.Populations.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            foreach (var c in inventory.Components)
            {
                Require(c.MemberDiscoveryIds != null && c.DependentComponentDigests != null, "MISSING_COMPONENT_MEMBERSHIP");
                c.MemberDiscoveryIds = c.MemberDiscoveryIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                c.DependentComponentDigests = c.DependentComponentDigests.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                c.MembershipDigest = MembershipDigest(source, c.MemberDiscoveryIds);
            }
            foreach (var p in inventory.Populations)
            {
                Require(p.IncludedDiscoveryIds != null && p.ExcludedDiscoveryIds != null && p.UnverifiableDiscoveryIds != null, "MISSING_POPULATION_CLASSIFICATION");
                p.IncludedDiscoveryIds = p.IncludedDiscoveryIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                p.ExcludedDiscoveryIds = p.ExcludedDiscoveryIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                p.UnverifiableDiscoveryIds = p.UnverifiableDiscoveryIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                p.MembershipDigest = PopulationDigest(source, p);
            }
            inventory.InventoryDigest = AQGreenB6CanonicalEvidence.Digest(inventory, "inventoryDigest");
            ValidateInventory(source, inventory);
            return inventory;
        }

        // verifiedAuthorities are independently verified owner/operator authority
        // evidence, supplied separately from the untrusted decision document. A role
        // string or a digest supplied by the decision is never its own authority.
        public static AQGreenFrozenReviewReceipt Process(AQGreenReconciliationEvidencePackage source,
            AQGreenFrozenReviewInventory inventory, IReadOnlyCollection<AQGreenFrozenReviewDecision> decisions,
            IReadOnlyCollection<AQGreenFrozenReviewerAuthority> verifiedAuthorities)
        {
            source.Verify(); ValidateInventory(source, inventory);
            Require(decisions != null && verifiedAuthorities != null, "MISSING_REVIEWER_AUTHORITY");
            var subjects = inventory.Components.Select(c => (Type: "COMPONENT", Id: c.ComponentDigest, Digest: c.MembershipDigest))
                .Concat(inventory.Populations.Select(p => (Type: "POPULATION", Id: p.Name, Digest: p.MembershipDigest)))
                .ToDictionary(s => (s.Type, s.Id));
            var unique = new Dictionary<(string, string), AQGreenFrozenReviewDecision>();
            var decisionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in decisions)
            {
                Require(d != null && SameBinding(d.Binding, inventory.Binding) && d.InventoryDigest == inventory.InventoryDigest, "STALE_OR_INCORRECT_REVIEW_SOURCE");
                (string Type, string Id, string Digest) subject = default;
                Require(d.SubjectType != null && d.SubjectId != null && subjects.TryGetValue((d.SubjectType, d.SubjectId), out subject), "INCORRECT_REVIEW_SUBJECT");
                Require(d.EvidenceType == Version && d.EvidenceDigest == subject.Digest, "INCORRECT_COMPONENT_OR_POPULATION_MEMBERSHIP");
                Require(Code(d.DecisionReference) && decisionIds.Add(d.DecisionReference) && unique.TryAdd((d.SubjectType, d.SubjectId), d), "DUPLICATED_OR_CONFLICTING_DECISION");
                Require(d.Disposition is "ACCEPTED" or "QUARANTINED" && d.ReasonCodes?.Length > 0 && d.ReasonCodes.All(Code) &&
                    d.ReasonCodes.Distinct(StringComparer.Ordinal).Count() == d.ReasonCodes.Length, "INVALID_REVIEW_OUTCOME_OR_REASONS");
                Require(d.ReviewedByRole == ReviewerRole && d.ReviewedAt.Kind == DateTimeKind.Utc &&
                    d.ReviewedAt >= source.Manifest.SourceBoundary.SourceObservedAtUtc && Code(d.AuthorityReference) && Digest(d.AuthorityEvidenceDigest), "INVALID_REVIEWER_AUTHORITY");
                var authorities = verifiedAuthorities.Where(a => a != null && a.AuthorityReference == d.AuthorityReference &&
                    a.EvidenceDigest == d.AuthorityEvidenceDigest).ToList();
                Require(authorities.Count == 1, "MISSING_OR_AMBIGUOUS_REVIEWER_AUTHORITY");
                var a = authorities.Single();
                Require(SameBinding(a.Binding, inventory.Binding) && a.InventoryDigest == inventory.InventoryDigest &&
                    a.SubjectType == d.SubjectType && a.SubjectId == d.SubjectId && a.DecisionReference == d.DecisionReference &&
                    a.ReviewedByRole == ReviewerRole && Code(a.EvidenceType) && a.AuthorizedAt.Kind == DateTimeKind.Utc &&
                    a.ExpiresAt.Kind == DateTimeKind.Utc && a.AuthorizedAt <= d.ReviewedAt && d.ReviewedAt <= a.ExpiresAt,
                    "REVIEWER_AUTHORITY_SCOPE_MISMATCH");
            }
            var records = source.Records.ToDictionary(r => r.DiscoveryId, StringComparer.Ordinal);
            var components = inventory.Components.Select(c =>
            {
                unique.TryGetValue(("COMPONENT", c.ComponentDigest), out var d);
                return SubjectResult("COMPONENT", c.ComponentDigest, c.MemberDiscoveryIds, d, records);
            }).ToList();
            var byComponent = components.ToDictionary(c => c.SubjectId, StringComparer.Ordinal);
            // Cyclic dependencies cannot become accepted by mutually citing acceptance.
            foreach (var c in inventory.Components)
                if (Reaches(c.ComponentDigest, c.ComponentDigest, inventory.Components, new HashSet<string>()))
                    Block(byComponent[c.ComponentDigest], "DEPENDENCY_CYCLE");
            bool changed;
            do
            {
                changed = false;
                foreach (var c in inventory.Components)
                    if ((byComponent[c.ComponentDigest].EffectiveDisposition == "ACCEPTED" ||
                         source.Manifest.PackageVersion == AQGreenReconciliationEvidenceManifest.Version) &&
                        c.DependentComponentDigests.Any(id => byComponent[id].EffectiveDisposition != "ACCEPTED"))
                    {
                        changed |= byComponent[c.ComponentDigest].EffectiveDisposition == "ACCEPTED";
                        Block(byComponent[c.ComponentDigest], "DEPENDENT_COMPONENT_NOT_ACCEPTED");
                    }
            } while (changed);
            foreach (var c in inventory.Components)
                byComponent[c.ComponentDigest].DependentComponentDigests = c.DependentComponentDigests;
            var membership = inventory.Components.SelectMany(c => c.MemberDiscoveryIds.Select(id => (Id: id, Component: c.ComponentDigest)))
                .ToDictionary(x => x.Id, x => x.Component, StringComparer.Ordinal);
            var omitted = records.Keys.Except(membership.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var populations = inventory.Populations.Select(p =>
            {
                unique.TryGetValue(("POPULATION", p.Name), out var d);
                var result = SubjectResult("POPULATION", p.Name, records.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(), d, records);
                if (p.UnverifiableDiscoveryIds.Length > 0) Block(result, "UNVERIFIABLE_POPULATION_MEMBERSHIP");
                if (omitted.Length > 0) Block(result, "OMITTED_SOURCE_COMPONENT_MEMBERSHIP");
                if (components.Any(c => c.EffectiveDisposition != "ACCEPTED")) Block(result, "COMPONENT_REVIEW_NOT_ACCEPTED");
                return result;
            }).ToList();
            var normalized = decisions.OrderBy(d => d.SubjectType, StringComparer.Ordinal).ThenBy(d => d.SubjectId, StringComparer.Ordinal)
                .Select(d => new[] { d.SubjectType, d.SubjectId, d.EvidenceType, d.EvidenceDigest, d.DecisionReference,
                    d.Disposition, string.Join(',', d.ReasonCodes.OrderBy(x => x, StringComparer.Ordinal)),
                    d.ReviewedAt.ToString("O", CultureInfo.InvariantCulture), d.ReviewedByRole, d.AuthorityReference, d.AuthorityEvidenceDigest });
            var receipt = new AQGreenFrozenReviewReceipt
            {
                Binding = inventory.Binding, InventoryDigest = inventory.InventoryDigest,
                ReviewDecisionDigest = AQGreenB6CanonicalEvidence.Tuples(new[] { new[] { "SNAPSHOT_AUTHORITY", inventory.Binding.SourceSnapshotDigest,
                    inventory.Binding.BaseManifestDigest, inventory.Binding.PopulationScopeDigest, inventory.InventoryDigest } }.Concat(normalized)),
                Components = components, Populations = populations, OmittedDiscoveryIds = omitted,
                ReviewCoverage = omitted.Length == 0 && unique.Count == subjects.Count ? "COMPLETE" : "PARTIAL",
                PopulationApprovalState = source.Manifest.WholeTenant && omitted.Length == 0 && unique.Count == subjects.Count &&
                    components.Concat(populations).All(s => s.EffectiveDisposition == "ACCEPTED") ? "ACCEPTED" : "QUARANTINED",
                AcceptedComponentDigest = AQGreenB6CanonicalEvidence.Tuples(components.Where(c => c.EffectiveDisposition == "ACCEPTED").Select(c => new[] { c.SubjectId })),
                QuarantinedComponentDigest = AQGreenB6CanonicalEvidence.Tuples(components.Where(c => c.EffectiveDisposition != "ACCEPTED").Select(c => new[] { c.SubjectId }))
            };
            receipt.ReceiptDigest = AQGreenB6CanonicalEvidence.Digest(receipt, "receiptDigest");
            return receipt;
        }

        private static AQGreenFrozenReviewSubjectResult SubjectResult(string type, string id, string[] members,
            AQGreenFrozenReviewDecision d, IReadOnlyDictionary<string, AQGreenReconciliationEvidenceRecord> records)
        {
            var unresolved = members.SelectMany(key => records[key].HistoricalEvidenceIssues
                .Concat(records[key].AreaHistoryEvidenceIssues ?? Array.Empty<string>()).Select(issue => key + ":" + issue))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var result = new AQGreenFrozenReviewSubjectResult { SubjectType = type, SubjectId = id, MemberDiscoveryIds = members,
                DecisionReference = d?.DecisionReference, RequestedDisposition = d?.Disposition ?? "UNREVIEWED",
                EffectiveDisposition = d?.Disposition ?? "QUARANTINED", HistoricalEvidenceIssues = unresolved,
                ReasonCodes = d?.ReasonCodes.OrderBy(x => x, StringComparer.Ordinal).ToArray() ?? new[] { "OMITTED_REVIEW_DECISION" } };
            if (unresolved.Length > 0) Block(result, "UNRESOLVED_HISTORICAL_EVIDENCE");
            return result;
        }
        // Revalidate an archived receipt against its original decisions and trusted
        // authority inputs. Hash integrity alone never establishes acceptance.
        public static void VerifyReceipt(AQGreenReconciliationEvidencePackage source, AQGreenFrozenReviewInventory inventory,
            IReadOnlyCollection<AQGreenFrozenReviewDecision> decisions, IReadOnlyCollection<AQGreenFrozenReviewerAuthority> verifiedAuthorities,
            AQGreenFrozenReviewReceipt receipt)
        {
            Require(receipt != null && SameBinding(receipt.Binding, Binding(source)) && receipt.InventoryDigest == inventory.InventoryDigest,
                "STALE_OR_INCORRECT_REVIEW_RECEIPT");
            Require(receipt.ReceiptDigest == AQGreenB6CanonicalEvidence.Digest(receipt, "receiptDigest"), "INVALID_REVIEW_RECEIPT_DIGEST");
            var replay = Process(source, inventory, decisions, verifiedAuthorities);
            Require(receipt.ReceiptDigest == replay.ReceiptDigest, "REVIEW_RECEIPT_DECISION_CONFLICT");
        }
        private static void Block(AQGreenFrozenReviewSubjectResult result, string reason)
        { result.EffectiveDisposition = "QUARANTINED"; result.ReasonCodes = result.ReasonCodes.Append(reason).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(); }
        private static bool Reaches(string start, string key, IReadOnlyCollection<AQGreenFrozenReviewComponent> components, HashSet<string> seen) =>
            seen.Add(key) && components.Single(c => c.ComponentDigest == key).DependentComponentDigests.Any(next => next == start || Reaches(start, next, components, seen));
        private static bool Code(string value) => value != null && CodePattern.IsMatch(value);
        private static bool Digest(string value) => value != null && DigestPattern.IsMatch(value);
        private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
        private static bool SameBinding(AQGreenFrozenReviewBinding a, AQGreenFrozenReviewBinding b) => a != null && b != null &&
            AQGreenB6CanonicalEvidence.Digest(a) == AQGreenB6CanonicalEvidence.Digest(b);
        private static AQGreenFrozenReviewBinding Binding(AQGreenReconciliationEvidencePackage source) => new()
        {
            EvidenceVersion = source.Manifest.PackageVersion, ReviewContractVersion = Version, TenantSnapshotKey = source.Manifest.TenantId,
            SourceSnapshotDigest = source.Manifest.OverallSnapshotDigest, BaseManifestDigest = source.Manifest.ManifestHash,
            PopulationScope = source.Manifest.WholeTenant ? "ALL_TENANT_AQGREEN_DISCOVERY" : "AUTHORIZED_AREA_AQGREEN_DISCOVERY",
            PopulationScopeDigest = AQGreenB6CanonicalEvidence.Digest(new { source.Manifest.TenantId, source.Manifest.WholeTenant,
                source.Manifest.AuthorizedAreaIds, source.Manifest.PopulationDefinition, source.Manifest.CandidatePopulationDigest, source.Manifest.AnalysisSnapshotCutoffUtc })
        };
        private static string MembershipDigest(AQGreenReconciliationEvidencePackage source, IEnumerable<string> keys)
        {
            var records = source.Records.ToDictionary(r => r.DiscoveryId, StringComparer.Ordinal);
            Require(keys != null && keys.All(records.ContainsKey), "MEMBER_OUTSIDE_FROZEN_POPULATION");
            return AQGreenB6CanonicalEvidence.Tuples(keys.Select(key => new[] { key, records[key].RecordDigest }));
        }
        private static string PopulationDigest(AQGreenReconciliationEvidencePackage source, AQGreenFrozenReviewPopulation p) =>
            AQGreenB6CanonicalEvidence.Tuples(new[] { new[] { "INCLUDED", MembershipDigest(source, p.IncludedDiscoveryIds) },
                new[] { "EXCLUDED", MembershipDigest(source, p.ExcludedDiscoveryIds) }, new[] { "UNVERIFIABLE", MembershipDigest(source, p.UnverifiableDiscoveryIds) } });
        private static void ValidateInventory(AQGreenReconciliationEvidencePackage source, AQGreenFrozenReviewInventory inventory)
        {
            Require(inventory != null && SameBinding(inventory.Binding, Binding(source)) &&
                inventory.InventoryDigest == AQGreenB6CanonicalEvidence.Digest(inventory, "inventoryDigest"), "STALE_OR_INCORRECT_REVIEW_INVENTORY");
            Require(inventory.Components != null && inventory.Populations != null &&
                inventory.Components.Select(c => c.ComponentDigest).Distinct().Count() == inventory.Components.Count, "DUPLICATE_COMPONENT_IDENTITY");
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            var componentIds = inventory.Components.Select(c => c.ComponentDigest).ToHashSet(StringComparer.Ordinal);
            foreach (var c in inventory.Components)
            {
                Require(Digest(c.ComponentDigest) && c.MemberDiscoveryIds?.Length > 0 && c.MemberDiscoveryIds.All(assigned.Add), "DUPLICATE_OR_INVALID_COMPONENT_MEMBERSHIP");
                Require(c.MembershipDigest == MembershipDigest(source, c.MemberDiscoveryIds) && c.DependentComponentDigests != null &&
                    c.DependentComponentDigests.Distinct().Count() == c.DependentComponentDigests.Length &&
                    c.DependentComponentDigests.All(componentIds.Contains), "INCORRECT_COMPONENT_DEPENDENCY_OR_DIGEST");
            }
            Require(inventory.Populations.Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(PopulationNames.OrderBy(x => x, StringComparer.Ordinal)), "MISSING_OR_DUPLICATE_D09_POPULATION");
            foreach (var p in inventory.Populations)
            {
                Require(p.IncludedDiscoveryIds != null && p.ExcludedDiscoveryIds != null && p.UnverifiableDiscoveryIds != null, "MISSING_POPULATION_CLASSIFICATION");
                var all = p.IncludedDiscoveryIds.Concat(p.ExcludedDiscoveryIds).Concat(p.UnverifiableDiscoveryIds).ToArray();
                Require(all.Distinct().Count() == all.Length && all.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(source.Records.Select(r => r.DiscoveryId).OrderBy(x => x, StringComparer.Ordinal)) &&
                    p.MembershipDigest == PopulationDigest(source, p), "OMITTED_OR_DUPLICATE_POPULATION_MEMBERSHIP");
            }
            // Source sponsor/recruiter/correction references are dependencies to review,
            // never inferred historical roots or placement-derived sponsor attribution.
            var byRecord = inventory.Components.SelectMany(c => c.MemberDiscoveryIds.Select(key => (key, c.ComponentDigest))).ToDictionary(x => x.key, x => x.ComponentDigest);
            var byCustomer = source.Records.ToDictionary(r => r.CustomerId, r => r.DiscoveryId);
            var byParticipant = source.Records.Where(r => r.ParticipationId.HasValue).ToDictionary(r => r.ParticipationId.Value, r => r.DiscoveryId);
            foreach (var r in source.Records.Where(r => byRecord.ContainsKey(r.DiscoveryId)))
            {
                var related = new List<string>();
                if (r.Attribution?.CreditedSponsorParticipantId is Guid sponsor && byParticipant.TryGetValue(sponsor, out var sponsorKey)) related.Add(sponsorKey);
                foreach (var id in new[] { r.CurrentRecruiterCustomerId }.Concat(r.RecruiterCorrections.SelectMany(c => new[] { c.PreviousRecruiterCustomerId, c.NewRecruiterCustomerId })))
                    if (id.HasValue && byCustomer.TryGetValue(id.Value, out var key)) related.Add(key);
                foreach (var key in related)
                    Require(byRecord.ContainsKey(key) && (byRecord[key] == byRecord[r.DiscoveryId] ||
                        inventory.Components.Single(c => c.ComponentDigest == byRecord[r.DiscoveryId]).DependentComponentDigests.Contains(byRecord[key])), "SOURCE_DEPENDENCY_OMITTED");
            }
        }
    }
    public sealed class AQGreenFrozenReviewBinding
    {
        public string EvidenceVersion { get; set; }
        public string ReviewContractVersion { get; set; }
        public int TenantSnapshotKey { get; set; }
        public string SourceSnapshotDigest { get; set; }
        public string BaseManifestDigest { get; set; }
        public string PopulationScope { get; set; }
        public string PopulationScopeDigest { get; set; }
    }
    public sealed class AQGreenFrozenReviewInventory
    {
        public AQGreenFrozenReviewBinding Binding { get; set; }
        public List<AQGreenFrozenReviewComponent> Components { get; set; }
        public List<AQGreenFrozenReviewPopulation> Populations { get; set; }
        public string InventoryDigest { get; set; }
    }
    public sealed class AQGreenFrozenReviewComponent
    {
        public string ComponentDigest { get; set; }
        public string[] MemberDiscoveryIds { get; set; }
        public string MembershipDigest { get; set; }
        public string[] DependentComponentDigests { get; set; }
    }
    public sealed class AQGreenFrozenReviewPopulation
    {
        public string Name { get; set; }
        public string[] IncludedDiscoveryIds { get; set; }
        public string[] ExcludedDiscoveryIds { get; set; }
        public string[] UnverifiableDiscoveryIds { get; set; }
        public string MembershipDigest { get; set; }
    }
    public sealed class AQGreenFrozenReviewDecision
    {
        public AQGreenFrozenReviewBinding Binding { get; set; }
        public string InventoryDigest { get; set; }
        public string SubjectType { get; set; }
        public string SubjectId { get; set; }
        public string Disposition { get; set; }
        public string[] ReasonCodes { get; set; }
        public string EvidenceType { get; set; }
        public string EvidenceDigest { get; set; }
        public string DecisionReference { get; set; }
        public DateTime ReviewedAt { get; set; }
        public string ReviewedByRole { get; set; }
        public string AuthorityReference { get; set; }
        public string AuthorityEvidenceDigest { get; set; }
    }
    public sealed class AQGreenFrozenReviewerAuthority
    {
        public AQGreenFrozenReviewBinding Binding { get; set; }
        public string InventoryDigest { get; set; }
        public string SubjectType { get; set; }
        public string SubjectId { get; set; }
        public string DecisionReference { get; set; }
        public string ReviewedByRole { get; set; }
        public string AuthorityReference { get; set; }
        public string EvidenceType { get; set; }
        public string EvidenceDigest { get; set; }
        public DateTime AuthorizedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
    public sealed class AQGreenFrozenReviewSubjectResult
    {
        public string SubjectType { get; set; }
        public string SubjectId { get; set; }
        public string[] MemberDiscoveryIds { get; set; }
        public string[] DependentComponentDigests { get; set; } = Array.Empty<string>();
        public string DecisionReference { get; set; }
        public string RequestedDisposition { get; set; }
        public string EffectiveDisposition { get; set; }
        public string[] HistoricalEvidenceIssues { get; set; }
        public string[] ReasonCodes { get; set; }
    }
    public sealed class AQGreenFrozenReviewReceipt
    {
        public string EvidenceClassification { get; set; } = "OFFLINE_REVIEW_BINDING_ONLY";
        public AQGreenFrozenReviewBinding Binding { get; set; }
        public string InventoryDigest { get; set; }
        public string ReviewDecisionDigest { get; set; }
        public string AcceptedComponentDigest { get; set; }
        public string QuarantinedComponentDigest { get; set; }
        public List<AQGreenFrozenReviewSubjectResult> Components { get; set; }
        public List<AQGreenFrozenReviewSubjectResult> Populations { get; set; }
        public string[] OmittedDiscoveryIds { get; set; }
        public string ReviewCoverage { get; set; }
        public string PopulationApprovalState { get; set; }
        public string ReceiptDigest { get; set; }
    }
}
