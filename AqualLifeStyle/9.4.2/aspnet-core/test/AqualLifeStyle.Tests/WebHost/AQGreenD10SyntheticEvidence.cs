using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Tests.WebHost
{
    // Test-only models of independently verified operator inputs. These are not actual
    // OPERATIONAL_EVIDENCE, B6 VerifyFinal results or owner authorization records.
    public sealed class AQGreenD10SyntheticEvidence
    {
        public AQGreenD10Decision Decision { get; set; }
        public AQGreenD10VerifiedAuthority Authority { get; set; }
        public JsonObject Final { get; set; }
        public static string Hash(string value) => AQGreenB6CanonicalEvidence.Hash(Encoding.UTF8.GetBytes(value));
        public static DateTime EffectiveAt => new LatestClosedCommissionWeekResolver().Resolve(DateTime.UtcNow).PeriodStartUtc.AddDays(7);
        public static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["App:Recovery:Enabled"] = "false", ["App:WeeklyCommissions:Enabled"] = "false",
            ["App:EntryMonthlyObligations:Enabled"] = "false", ["Bird:Enabled"] = "false", ["Yoco:Mode"] = "",
            ["AQGreenV2Demo:Enabled"] = "false", ["AQGreenV2Pilot:Enabled"] = "false",
            ["App:ServerRootAddress"] = "https://api.example.test", ["App:ClientRootAddress"] = "https://app.example.test",
            ["App:CorsOrigins"] = "https://app.example.test", ["Deployment:EnvironmentId"] = "synthetic-d10",
            ["Deployment:BuildId"] = "synthetic-d10-offline"
        }).Build();

        public static AQGreenD10SyntheticEvidence Create(IConfiguration configuration,
            List<AQGreenD10MemberIdentity> members = null, List<AQGreenD10PlacementEvidence> placements = null,
            AQGreenReconciliationSourceBoundary boundary = null)
        {
            var area = Guid.NewGuid(); var participant = Guid.NewGuid();
            members ??= new() { new() { CustomerId = 1, ParticipantId = participant, AreaId = area, ComponentDigest = Hash("synthetic-component"),
                ParticipantSnapshotKey = Guid.NewGuid().ToString("D") } };
            placements ??= new() { new() { PlacementId = Guid.NewGuid(), ParticipantId = participant, PlacementTreeScopeId = Guid.NewGuid(),
                CanonicalPath = "", PlacedAtUtc = EffectiveAt.AddDays(-3), RulesVersion = AQGreenPlacementRules.CurrentVersion } };
            boundary ??= new() { SourceDatabaseIdentityEvidence = Hash("synthetic-database"), SourceSchemaVersion = Hash("synthetic-schema"), SourceAssemblyDigest = Hash("synthetic-ef-assembly") };
            var d = new AQGreenD10Decision
            {
                DecisionId = "AQG-V2-D10", DecisionReference = "SYNTHETIC-D10-DECISION", Outcome = "AUTHORIZED", TenantId = 1,
                TenantSnapshotKey = "SYNTHETIC-TENANT", PopulationScope = "ALL_TENANT_AQGREEN_PARTICIPATIONS",
                AuthorizedAreaIds = members.Select(m => m.AreaId).Distinct().ToArray(), V2EffectiveAtUtc = EffectiveAt,
                AuthorizedAtUtc = EffectiveAt.AddHours(-1), AllowProspectiveAdmissions = true, EnableGraduation = false,
                SourceSnapshotDigest = Hash("synthetic-frozen-source"), BaseManifestDigest = Hash("synthetic-base-manifest"),
                AcceptedComponentDigest = AQGreenB6CanonicalEvidence.Tuples(members.Select(m => m.ComponentDigest).Distinct().Select(id => new[] { id })),
                PopulationDigests = new() { ["canonicalisation"] = Hash("synthetic-canonicalisation"), ["qualificationEligibility"] = Hash("synthetic-qualification"),
                    ["historicalImpactInventory"] = Hash("synthetic-historical-impact") }, HistoricalMembers = members, ReconciledPlacements = placements,
                RequiredConfigurationDigest = AQGreenD10Commissioning.ConfigurationDigest(configuration),
                RequiredDatabaseIdentityDigest = boundary.SourceDatabaseIdentityEvidence, RequiredSchemaDigest = boundary.SourceSchemaVersion,
                RequiredAssemblyDigest = boundary.SourceAssemblyDigest, RequiredRuntimeAssemblyDigest = AQGreenD10Runtime.RuntimeAssemblyDigest(boundary.SourceAssemblyDigest)
            };
            d.MembershipDigest = AQGreenD10Commissioning.MembershipDigest(members); d.ReconciledPlacementDigest = AQGreenD10Commissioning.PlacementDigest(placements);
            d.PopulationParticipantKeys = new() { ["canonicalisation"] = members.Where(m => placements.Any(p => p.ParticipantId == m.ParticipantId))
                .Select(m => m.ParticipantSnapshotKey).ToArray(), ["qualificationEligibility"] = members.Where(m => placements.Any(p => p.ParticipantId == m.ParticipantId))
                .Select(m => m.ParticipantSnapshotKey).ToArray(), ["historicalImpactInventory"] = members.Select(m => m.ParticipantSnapshotKey).ToArray() };
            foreach (var population in d.PopulationParticipantKeys)
                d.PopulationDigests[population.Key] = AQGreenB6CanonicalEvidence.Tuples(population.Value.Select(key => new[] { key }));
            var final = new JsonObject
            {
                ["manifestStage"] = "FINAL_ANALYSIS", ["toolchainIdentity"] = new JsonObject { ["evidenceClassification"] = "OPERATIONAL_EVIDENCE" },
                ["authorizedTenantSnapshotKey"] = d.TenantSnapshotKey, ["populationScope"] = d.PopulationScope,
                ["overallSnapshotDigest"] = d.SourceSnapshotDigest, ["baseManifestHash"] = d.BaseManifestDigest,
                ["analysisSnapshotCutoff"] = d.AuthorizedAtUtc.AddHours(-2), ["sourceObservedAt"] = d.AuthorizedAtUtc.AddHours(-1),
                ["analysisDigests"] = new JsonObject { ["acceptedComponentDigest"] = d.AcceptedComponentDigest,
                    ["quarantinedComponentDigest"] = AQGreenB6CanonicalEvidence.Tuples(Array.Empty<string[]>()),
                    ["canonicalisationPopulationDigest"] = d.PopulationDigests["canonicalisation"],
                    ["qualificationEligibilityDigest"] = d.PopulationDigests["qualificationEligibility"],
                    ["historicalImpactInventoryDigest"] = d.PopulationDigests["historicalImpactInventory"] }
            };
            d.FinalManifestDigest = AQGreenB6CanonicalEvidence.Digest(final); final["manifestHash"] = d.FinalManifestDigest;
            var a = new AQGreenD10VerifiedAuthority
            {
                AuthorityReference = "SYNTHETIC-INDEPENDENT-AUTHORITY", DecisionReference = d.DecisionReference,
                DecisionDigest = AQGreenB6CanonicalEvidence.Digest(d), TenantId = d.TenantId, TenantSnapshotKey = d.TenantSnapshotKey,
                PopulationScope = d.PopulationScope, MembershipDigest = d.MembershipDigest, FinalManifestDigest = d.FinalManifestDigest,
                ReconciledPlacementDigest = d.ReconciledPlacementDigest, V2EffectiveAtUtc = d.V2EffectiveAtUtc,
                AuthorizedAtUtc = d.AuthorizedAtUtc.AddMinutes(-1), ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                OwnerAuthorizationEvidenceDigest = Hash("synthetic-owner"), B6FinalVerificationEvidenceDigest = Hash("synthetic-final-verification"),
                B6ReconciliationAuthorityEvidenceDigest = Hash("synthetic-reconciliation-authority"), B6AppliedReconciliationEvidenceDigest = Hash("synthetic-applied"),
                BackupRestoreEvidenceDigest = Hash("synthetic-restore"), ReleasePrerequisiteEvidenceDigest = Hash("synthetic-release"),
                FinancialBoundaryEvidenceDigest = Hash("synthetic-financial-boundary"), WriteFenceEvidenceDigest = Hash("synthetic-write-fence")
            };
            return new() { Decision = d, Authority = a, Final = final };
        }
        public AQGreenD10Commissioning Bind() => AQGreenD10Commissioning.BindVerified(Decision, Authority, Final);
    }
}
