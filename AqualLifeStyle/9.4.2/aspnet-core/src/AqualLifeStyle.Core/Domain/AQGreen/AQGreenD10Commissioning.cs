using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Abp.Dependency;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenD10CommissioningSource
    {
        // Trusted operator boundary, never an API-uploaded decision or role claim.
        AQGreenD10Commissioning Read();
    }

    public sealed class UncommissionedAQGreenD10Source : IAQGreenD10CommissioningSource, ISingletonDependency
    {
        public static readonly UncommissionedAQGreenD10Source Instance = new();
        public AQGreenD10Commissioning Read() => null;
    }

    /// <summary>Explicit D10 scope; neither a B6 application command nor a new reconciliation format.</summary>
    public sealed class AQGreenD10Decision
    {
        [JsonRequired] public string ContractVersion { get; set; } = "AQGreenD10CutoverV1";
        public string DecisionId { get; set; }
        public string DecisionReference { get; set; }
        public string Outcome { get; set; }
        public int TenantId { get; set; }
        public string TenantSnapshotKey { get; set; }
        public string PopulationScope { get; set; }
        public Guid[] AuthorizedAreaIds { get; set; }
        public DateTime V2EffectiveAtUtc { get; set; }
        public DateTime AuthorizedAtUtc { get; set; }
        public bool? AllowProspectiveAdmissions { get; set; }
        public bool? EnableGraduation { get; set; }
        public string SourceSnapshotDigest { get; set; }
        public string BaseManifestDigest { get; set; }
        public string FinalManifestDigest { get; set; }
        public string AcceptedComponentDigest { get; set; }
        public SortedDictionary<string, string> PopulationDigests { get; set; }
        public SortedDictionary<string, string[]> PopulationParticipantKeys { get; set; }
        public string MembershipDigest { get; set; }
        public string ReconciledPlacementDigest { get; set; }
        public string RequiredConfigurationDigest { get; set; }
        public string RequiredDatabaseIdentityDigest { get; set; }
        public string RequiredSchemaDigest { get; set; }
        public string RequiredAssemblyDigest { get; set; }
        public string RequiredRuntimeAssemblyDigest { get; set; }
        public List<AQGreenD10MemberIdentity> HistoricalMembers { get; set; }
        public List<AQGreenD10PlacementEvidence> ReconciledPlacements { get; set; }
    }

    public sealed class AQGreenD10MemberIdentity
    {
        public int CustomerId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid AreaId { get; set; }
        public string ComponentDigest { get; set; }
        public string ParticipantSnapshotKey { get; set; }
    }

    public sealed class AQGreenD10PlacementEvidence
    {
        public Guid PlacementId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid PlacementTreeScopeId { get; set; }
        public Guid? ParentParticipantId { get; set; }
        public int? Slot { get; set; }
        public string CanonicalPath { get; set; }
        public DateTime PlacedAtUtc { get; set; }
        public string RulesVersion { get; set; }
    }

    // Supplied separately after independent owner/operator verification, as with
    // existing frozen-review trusted authority input. Hashes alone authenticate no person.
    public sealed class AQGreenD10VerifiedAuthority
    {
        [JsonRequired] public string ContractVersion { get; set; } = "AQGreenD10VerifiedAuthorityV1";
        public string AuthorityReference { get; set; }
        public string DecisionReference { get; set; }
        public string DecisionDigest { get; set; }
        public string OwnerAuthorizationEvidenceDigest { get; set; }
        public string B6FinalVerificationEvidenceDigest { get; set; }
        public string B6ReconciliationAuthorityEvidenceDigest { get; set; }
        public string B6AppliedReconciliationEvidenceDigest { get; set; }
        public string BackupRestoreEvidenceDigest { get; set; }
        public string ReleasePrerequisiteEvidenceDigest { get; set; }
        public string FinancialBoundaryEvidenceDigest { get; set; }
        public string WriteFenceEvidenceDigest { get; set; }
        public string FinancialWorkersCommissioningEvidenceDigest { get; set; }
        public int TenantId { get; set; }
        public string TenantSnapshotKey { get; set; }
        public string PopulationScope { get; set; }
        public string MembershipDigest { get; set; }
        public string FinalManifestDigest { get; set; }
        public string ReconciledPlacementDigest { get; set; }
        public DateTime V2EffectiveAtUtc { get; set; }
        public DateTime AuthorizedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public sealed class AQGreenD10Commissioning
    {
        private readonly byte[] _decision;
        private readonly DateTime _expiresAt;
        public bool FinancialWorkersCommissioned { get; }
        public AQGreenD10Decision Decision => JsonSerializer.Deserialize<AQGreenD10Decision>(_decision, AQGreenB6CanonicalEvidence.Options);
        private AQGreenD10Commissioning(AQGreenD10Decision decision, DateTime expiresAt, bool workersCommissioned)
        { _decision = AQGreenB6CanonicalEvidence.Bytes(decision); _expiresAt = expiresAt; FinancialWorkersCommissioned = workersCommissioned; }

        public static AQGreenD10Commissioning BindVerified(AQGreenD10Decision d,
            AQGreenD10VerifiedAuthority a, JsonObject independentlyVerifiedB6FinalManifest)
        {
            Require(d != null && a != null && independentlyVerifiedB6FinalManifest != null, "D10_REQUIRED_AUTHORITY_MISSING");
            Require(d.ContractVersion == "AQGreenD10CutoverV1" && a.ContractVersion == "AQGreenD10VerifiedAuthorityV1" &&
                d.DecisionId == "AQG-V2-D10" && d.Outcome == "AUTHORIZED" && Code(d.DecisionReference) && Code(a.AuthorityReference), "D10_DECISION_UNAUTHORIZED");
            Require(d.TenantId > 0 && Code(d.TenantSnapshotKey) && d.PopulationScope == "ALL_TENANT_AQGREEN_PARTICIPATIONS" &&
                d.AuthorizedAreaIds?.Length > 0 && d.AuthorizedAreaIds.All(x => x != Guid.Empty) &&
                d.AuthorizedAreaIds.Distinct().Count() == d.AuthorizedAreaIds.Length, "D10_SCOPE_UNSUPPORTED_OR_INCOMPLETE");
            Require(Utc(d.AuthorizedAtUtc) && Utc(d.V2EffectiveAtUtc) && d.AuthorizedAtUtc <= d.V2EffectiveAtUtc,
                "D10_EFFECTIVE_BOUNDARY_INVALID");
            AQGreenCommissionWeek.FromStartUtc(d.V2EffectiveAtUtc); // Supported cutover: one canonical financial boundary.
            Require(d.HistoricalMembers?.Count > 0 && d.ReconciledPlacements?.Count > 0 && d.PopulationDigests != null,
                "D10_POPULATION_INCOMPLETE");
            Require(d.AllowProspectiveAdmissions.HasValue && d.EnableGraduation.HasValue, "D10_CAPABILITY_SELECTION_MISSING");
            Require(d.HistoricalMembers.All(m => m != null && m.CustomerId > 0 && m.ParticipantId != Guid.Empty &&
                    d.AuthorizedAreaIds.Contains(m.AreaId) && Digest(m.ComponentDigest) && Code(m.ParticipantSnapshotKey)) &&
                d.HistoricalMembers.Select(m => m.CustomerId).Distinct().Count() == d.HistoricalMembers.Count &&
                d.HistoricalMembers.Select(m => m.ParticipantId).Distinct().Count() == d.HistoricalMembers.Count &&
                d.HistoricalMembers.Select(m => m.ParticipantSnapshotKey).Distinct().Count() == d.HistoricalMembers.Count,
                "D10_DUPLICATE_OR_INVALID_MEMBERSHIP");
            Require(d.ReconciledPlacements.All(p => p != null && p.PlacementId != Guid.Empty && p.PlacementTreeScopeId != Guid.Empty &&
                    d.HistoricalMembers.Any(m => m.ParticipantId == p.ParticipantId) && Utc(p.PlacedAtUtc) && p.PlacedAtUtc <= d.V2EffectiveAtUtc &&
                    p.RulesVersion == AQGreenPlacementRules.CurrentVersion) &&
                d.ReconciledPlacements.Select(p => p.ParticipantId).Distinct().Count() == d.ReconciledPlacements.Count &&
                d.ReconciledPlacements.Select(p => p.PlacementId).Distinct().Count() == d.ReconciledPlacements.Count,
                "D10_RECONCILED_PLACEMENTS_INVALID");
            Require(d.MembershipDigest == MembershipDigest(d.HistoricalMembers) &&
                d.ReconciledPlacementDigest == PlacementDigest(d.ReconciledPlacements) &&
                d.AcceptedComponentDigest == AQGreenB6CanonicalEvidence.Tuples(d.HistoricalMembers.Select(m => m.ComponentDigest)
                    .Distinct(StringComparer.Ordinal).Select(x => new[] { x })), "D10_POPULATION_DIGEST_MISMATCH");
            var f = independentlyVerifiedB6FinalManifest;
            var cutoff = f["analysisSnapshotCutoff"]?.Deserialize<DateTime>() ?? default;
            var observed = f["sourceObservedAt"]?.Deserialize<DateTime>() ?? default;
            Require(Utc(cutoff) && Utc(observed) && cutoff <= observed && observed <= d.AuthorizedAtUtc, "D10_B6_OBSERVATION_BOUNDARY_MISMATCH");
            Require(f["manifestStage"]?.GetValue<string>() == "FINAL_ANALYSIS" &&
                f["toolchainIdentity"]?["evidenceClassification"]?.GetValue<string>() == "OPERATIONAL_EVIDENCE" &&
                f["manifestHash"]?.GetValue<string>() == d.FinalManifestDigest &&
                AQGreenB6CanonicalEvidence.Digest(f, "manifestHash") == d.FinalManifestDigest &&
                f["overallSnapshotDigest"]?.GetValue<string>() == d.SourceSnapshotDigest &&
                f["baseManifestHash"]?.GetValue<string>() == d.BaseManifestDigest &&
                f["authorizedTenantSnapshotKey"]?.GetValue<string>() == d.TenantSnapshotKey &&
                f["populationScope"]?.GetValue<string>() == d.PopulationScope &&
                f["analysisDigests"]?["acceptedComponentDigest"]?.GetValue<string>() == d.AcceptedComponentDigest &&
                f["analysisDigests"]?["quarantinedComponentDigest"]?.GetValue<string>() == AQGreenB6CanonicalEvidence.Tuples(Array.Empty<string[]>()),
                "D10_B6_FINAL_SCOPE_OR_AUTHORITY_MISMATCH");
            var fields = new Dictionary<string, string> { ["canonicalisation"] = "canonicalisationPopulationDigest",
                ["qualificationEligibility"] = "qualificationEligibilityDigest", ["historicalImpactInventory"] = "historicalImpactInventoryDigest" };
            Require(d.PopulationDigests.Count == fields.Count && fields.All(p => d.PopulationDigests.TryGetValue(p.Key, out var digest) &&
                Digest(digest) && f["analysisDigests"]?[p.Value]?.GetValue<string>() == digest), "D10_B6_POPULATION_MISMATCH");
            Require(d.PopulationParticipantKeys != null && d.PopulationParticipantKeys.Count == fields.Count && fields.All(p =>
                d.PopulationParticipantKeys.TryGetValue(p.Key, out var keys) && keys != null && keys.Distinct().Count() == keys.Length &&
                keys.All(key => d.HistoricalMembers.Any(m => m.ParticipantSnapshotKey == key)) &&
                AQGreenB6CanonicalEvidence.Tuples(keys.Select(key => new[] { key })) == d.PopulationDigests[p.Key]),
                "D10_B6_NATIVE_POPULATION_MAPPING_MISMATCH");
            var canonicalKeys = d.PopulationParticipantKeys["canonicalisation"];
            Require(d.ReconciledPlacements.Count == canonicalKeys.Length && d.ReconciledPlacements.All(p =>
                canonicalKeys.Contains(d.HistoricalMembers.Single(m => m.ParticipantId == p.ParticipantId).ParticipantSnapshotKey)),
                "D10_RECONCILED_CANONICAL_POPULATION_INCOMPLETE");
            Require(a.DecisionDigest == AQGreenB6CanonicalEvidence.Digest(d) && a.DecisionReference == d.DecisionReference &&
                a.TenantId == d.TenantId && a.TenantSnapshotKey == d.TenantSnapshotKey && a.PopulationScope == d.PopulationScope &&
                a.MembershipDigest == d.MembershipDigest && a.FinalManifestDigest == d.FinalManifestDigest &&
                a.ReconciledPlacementDigest == d.ReconciledPlacementDigest && a.V2EffectiveAtUtc == d.V2EffectiveAtUtc &&
                Utc(a.AuthorizedAtUtc) && Utc(a.ExpiresAtUtc) && a.AuthorizedAtUtc <= d.AuthorizedAtUtc &&
                a.ExpiresAtUtc > d.V2EffectiveAtUtc, "D10_INDEPENDENT_AUTHORITY_BINDING_MISMATCH");
            Require(new[] { d.SourceSnapshotDigest, d.BaseManifestDigest, d.FinalManifestDigest,
                a.OwnerAuthorizationEvidenceDigest, a.B6FinalVerificationEvidenceDigest, a.B6ReconciliationAuthorityEvidenceDigest,
                a.B6AppliedReconciliationEvidenceDigest, a.BackupRestoreEvidenceDigest, a.ReleasePrerequisiteEvidenceDigest,
                a.FinancialBoundaryEvidenceDigest, a.WriteFenceEvidenceDigest, d.RequiredConfigurationDigest,
                d.RequiredDatabaseIdentityDigest, d.RequiredSchemaDigest, d.RequiredAssemblyDigest,
                d.RequiredRuntimeAssemblyDigest }.All(Digest), "D10_PREREQUISITE_EVIDENCE_MISSING");
            Require(a.FinancialWorkersCommissioningEvidenceDigest == null || Digest(a.FinancialWorkersCommissioningEvidenceDigest), "D10_WORKER_AUTHORITY_INVALID");
            return new AQGreenD10Commissioning(d, a.ExpiresAtUtc, a.FinancialWorkersCommissioningEvidenceDigest != null);
        }

        public void EnsureCurrent(DateTime nowUtc) => Require(Utc(nowUtc) && nowUtc <= _expiresAt, "D10_AUTHORITY_EXPIRED");
        public void EnsureConfiguration(IConfiguration configuration)
        {
            Require(Decision.RequiredConfigurationDigest == ConfigurationDigest(configuration), "D10_CONFIGURATION_MISMATCH");
            Require(Code(configuration["Deployment:EnvironmentId"]) && Code(configuration["Deployment:BuildId"]), "D10_DEPLOYMENT_IDENTITY_MISSING");
            Require(!configuration.GetValue<bool>("App:EntryMonthlyObligations:Enabled"), "D10_MONTHLY_AUTOMATION_NOT_COMMISSIONED");
            if (configuration.GetValue<bool>("App:WeeklyCommissions:Enabled")) Require(FinancialWorkersCommissioned, "D10_FINANCIAL_WORKER_AUTHORITY_MISSING");
        }
        public static string ConfigurationDigest(IConfiguration configuration) => AQGreenB6CanonicalEvidence.Digest(
            new SortedDictionary<string, string>(new[] { "App:Recovery:Enabled", "App:WeeklyCommissions:Enabled",
                "App:EntryMonthlyObligations:Enabled", "Bird:Enabled", "Yoco:Mode", "AQGreenV2Demo:Enabled",
                "AQGreenV2Pilot:Enabled", "App:ServerRootAddress", "App:ClientRootAddress", "App:CorsOrigins",
                "Deployment:EnvironmentId", "Deployment:BuildId" }
                .ToDictionary(key => key, key => configuration[key]), StringComparer.Ordinal));
        public static string MembershipDigest(IEnumerable<AQGreenD10MemberIdentity> members) => AQGreenB6CanonicalEvidence.Tuples(
            members.Select(m => new[] { m.CustomerId.ToString(CultureInfo.InvariantCulture), m.ParticipantId.ToString("D"),
                m.AreaId.ToString("D"), m.ComponentDigest, m.ParticipantSnapshotKey }));
        public static string PlacementDigest(IEnumerable<AQGreenD10PlacementEvidence> placements) => AQGreenB6CanonicalEvidence.Digest(
            placements.OrderBy(p => p.ParticipantId).ToArray());
        public static void Require(bool condition, string code)
        { if (!condition) throw new InvalidOperationException(code); }
        private static bool Utc(DateTime time) => time != default && time.Kind == DateTimeKind.Utc;
        private static bool Digest(string value) => value != null && Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
        private static bool Code(string value) => value != null && Regex.IsMatch(value, "\\A[A-Za-z0-9._:+/@-]{1,128}\\z", RegexOptions.CultureInvariant);
    }
}
