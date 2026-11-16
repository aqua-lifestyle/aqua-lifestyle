using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Tests.WebHost;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    // Focused verification of the successor acceptance logic only: every
    // recomputable binding is exercised against a real decision digest. These
    // tests are not the post-Normal transition validation, which requires a
    // genuinely commissioned Normal state on disposable resources.
    public class AQGreenD10BackendSuccessorTests
    {
        private static readonly string Revision = new('a', 40);
        private static readonly string ExecutorSha = new('b', 64);

        private static AQGreenD10SyntheticEvidence Evidence(AQGreenReconciliationSourceBoundary boundary = null) =>
            AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration(),
                boundary: boundary);

        private static string PayloadJson(AQGreenD10SyntheticEvidence evidence, string schemaTransition,
            string revision = null)
        {
            var d = evidence.Decision;
            var payload = new Dictionary<string, object>
            {
                ["contractVersion"] = AQGreenD10BackendSuccessor.ContractVersion,
                ["predecessorDecisionSha256"] = AQGreenB6CanonicalEvidence.Digest(d),
                ["predecessorSchemaSha256"] = d.RequiredSchemaDigest,
                ["predecessorAssemblySha256"] = d.RequiredAssemblyDigest,
                ["predecessorRuntimeAssemblySha256"] = d.RequiredRuntimeAssemblyDigest,
                ["predecessorDatabaseIdentitySha256"] = d.RequiredDatabaseIdentityDigest,
                ["predecessorBuildId"] = "synthetic-d10-offline",
                ["successorRevisionSha256"] = revision ?? Revision,
                ["successorSchemaSha256"] = schemaTransition == AQGreenD10BackendSuccessor.SchemaTransitionNone
                    ? d.RequiredSchemaDigest : AQGreenD10SyntheticEvidence.Hash("successor-schema"),
                ["successorAssemblySha256"] = AQGreenD10SyntheticEvidence.Hash("successor-ef"),
                ["successorApplicationSha256"] = AQGreenB6CanonicalEvidence.Hash(
                    File.ReadAllBytes(typeof(AQGreenD10BackendSuccessor).Assembly.Location)),
                ["successorCoreSha256"] = AQGreenB6CanonicalEvidence.Hash(
                    File.ReadAllBytes(typeof(AQGreenD10Decision).Assembly.Location)),
                ["schemaTransition"] = schemaTransition,
                ["releaseRunId"] = 43429695806L, ["releaseRunAttempt"] = 1,
                ["ciRunId"] = 43429695777L, ["ciRunAttempt"] = 1,
                ["executorSha256"] = ExecutorSha
            };
            return JsonSerializer.Serialize(payload);
        }

        private static (IConfigurationRoot Configuration, string PayloadJson) Staged(
            AQGreenD10SyntheticEvidence evidence, string schemaTransition, Action<Dictionary<string, string>> mutate = null,
            string revision = null)
        {
            var json = PayloadJson(evidence, schemaTransition, revision);
            var bytes = Encoding.UTF8.GetBytes(json);
            var values = new Dictionary<string, string>
            {
                ["App:AQGreenV2:D10:BackendSuccessorJsonBase64"] = Convert.ToBase64String(bytes),
                ["App:AQGreenV2:D10:BackendSuccessorSha256"] = AQGreenB6CanonicalEvidence.Hash(bytes),
                ["Deployment:BuildId"] = "synthetic-d10-offline",
                ["RENDER_GIT_COMMIT"] = revision ?? Revision
            };
            mutate?.Invoke(values);
            return (new ConfigurationBuilder().AddInMemoryCollection(values).Build(), json);
        }

        private static void AssertCode(Action accept, string code) =>
            Should.Throw<InvalidOperationException>(accept).Message.ShouldBe(code);

        [Fact]
        public void AbsentInputLeavesHistoricalGateInCharge() =>
            AQGreenD10BackendSuccessor.Accept(Evidence().Decision, new(), new ConfigurationBuilder().Build())
                .ShouldBeFalse();

        [Fact]
        public void UnchangedSchemaAcceptsDeclaredNoneTransition()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone);
            AQGreenD10BackendSuccessor.Accept(evidence.Decision,
                new() { SourceSchemaVersion = evidence.Decision.RequiredSchemaDigest,
                    SourceAssemblyDigest = AQGreenD10SyntheticEvidence.Hash("successor-ef") }, configuration).ShouldBeTrue();
        }

        [Fact]
        public void ChangedSchemaAcceptsDeclaredAdditiveTransition()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionAdditive);
            AQGreenD10BackendSuccessor.Accept(evidence.Decision,
                new() { SourceSchemaVersion = AQGreenD10SyntheticEvidence.Hash("successor-schema"),
                    SourceAssemblyDigest = AQGreenD10SyntheticEvidence.Hash("successor-ef") }, configuration)
                .ShouldBeTrue();
        }

        [Theory]
        [InlineData("schema")]
        [InlineData("ef")]
        [InlineData("application")]
        [InlineData("core")]
        public void ObservedSuccessorDigestCannotBeReplacedByARepinnedStagedPayload(string changed)
        {
            var evidence = Evidence();
            var (configuration, json) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone);
            var boundary = new AQGreenReconciliationSourceBoundary {
                SourceSchemaVersion = evidence.Decision.RequiredSchemaDigest,
                SourceAssemblyDigest = AQGreenD10SyntheticEvidence.Hash("successor-ef") };
            if (changed == "schema") boundary.SourceSchemaVersion = AQGreenD10SyntheticEvidence.Hash("other-schema");
            if (changed == "ef") boundary.SourceAssemblyDigest = AQGreenD10SyntheticEvidence.Hash("other-ef");
            if (changed == "application" || changed == "core")
            {
                var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                properties[changed == "application" ? "successorApplicationSha256" : "successorCoreSha256"] =
                    JsonSerializer.SerializeToElement(AQGreenD10SyntheticEvidence.Hash("unapproved-binary"));
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(properties));
                configuration[AQGreenD10BackendSuccessor.PayloadKey] = Convert.ToBase64String(bytes);
                configuration[AQGreenD10BackendSuccessor.PayloadPinKey] = AQGreenB6CanonicalEvidence.Hash(bytes);
            }
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, boundary, configuration),
                changed == "schema" ? "D10_BACKEND_SUCCESSOR_SCHEMA_TRANSITION_MISMATCH" :
                    "D10_BACKEND_SUCCESSOR_OBSERVED_RELEASE_MISMATCH");
        }

        [Fact]
        public void TamperedPayloadFailsTheRawPin()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone,
                values => values["App:AQGreenV2:D10:BackendSuccessorSha256"] = AQGreenD10SyntheticEvidence.Hash("other"));
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, new(), configuration),
                "D10_BACKEND_SUCCESSOR_PINNED_INPUT_CHANGED");
        }

        [Fact]
        public void PinWithoutPayloadIsRejectedLoudly()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone,
                values => values.Remove("App:AQGreenV2:D10:BackendSuccessorJsonBase64"));
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, new(), configuration),
                "D10_BACKEND_SUCCESSOR_PAYLOAD_MISSING");
        }

        [Fact]
        public void RevisionMustMatchTheRunningDeploymentIdentity()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone,
                values => values["RENDER_GIT_COMMIT"] = new('c', 40));
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, new(), configuration),
                "D10_BACKEND_SUCCESSOR_REVISION_MISMATCH");
        }

        [Fact]
        public void TransitionDeclarationMustMatchTheObservedSchema()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionAdditive);
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision,
                    new() { SourceSchemaVersion = evidence.Decision.RequiredSchemaDigest }, configuration),
                "D10_BACKEND_SUCCESSOR_SCHEMA_TRANSITION_MISMATCH");
        }

        [Fact]
        public void UnknownPropertiesAreRejected()
        {
            var evidence = Evidence();
            var (configuration, json) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone);
            var extra = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            extra["extra"] = "value";
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(extra));
            var values = new Dictionary<string, string>
            {
                ["App:AQGreenV2:D10:BackendSuccessorJsonBase64"] = Convert.ToBase64String(bytes),
                ["App:AQGreenV2:D10:BackendSuccessorSha256"] = AQGreenB6CanonicalEvidence.Hash(bytes),
                ["Deployment:BuildId"] = "synthetic-d10-offline", ["RENDER_GIT_COMMIT"] = Revision
            };
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, new(),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build()),
                "D10_BACKEND_SUCCESSOR_INPUT_SHAPE_INVALID");
        }

        [Fact]
        public void PredecessorPinsMustMatchTheCommissionedDecision()
        {
            var evidence = Evidence();
            var (configuration, json) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone);
            var replaced = json.Replace(evidence.Decision.RequiredSchemaDigest, AQGreenD10SyntheticEvidence.Hash("other-schema"));
            var bytes = Encoding.UTF8.GetBytes(replaced);
            var values = new Dictionary<string, string>
            {
                ["App:AQGreenV2:D10:BackendSuccessorJsonBase64"] = Convert.ToBase64String(bytes),
                ["App:AQGreenV2:D10:BackendSuccessorSha256"] = AQGreenB6CanonicalEvidence.Hash(bytes),
                ["Deployment:BuildId"] = "synthetic-d10-offline", ["RENDER_GIT_COMMIT"] = Revision
            };
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, new(),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build()),
                "D10_BACKEND_SUCCESSOR_PREDECESSOR_BINDING_MISMATCH");
        }

        [Fact]
        public void ChangedDecisionCannotCarryThePredecessorDigest()
        {
            var evidence = Evidence();
            var (configuration, _) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone);
            var d = evidence.Decision;
            var mutated = new AQGreenD10Decision
            {
                DecisionId = d.DecisionId, DecisionReference = d.DecisionReference, Outcome = d.Outcome,
                TenantId = 2, TenantSnapshotKey = d.TenantSnapshotKey, PopulationScope = d.PopulationScope,
                AuthorizedAreaIds = d.AuthorizedAreaIds, V2EffectiveAtUtc = d.V2EffectiveAtUtc,
                AuthorizedAtUtc = d.AuthorizedAtUtc, AllowProspectiveAdmissions = d.AllowProspectiveAdmissions,
                EnableGraduation = d.EnableGraduation, SourceSnapshotDigest = d.SourceSnapshotDigest,
                BaseManifestDigest = d.BaseManifestDigest, FinalManifestDigest = d.FinalManifestDigest,
                AcceptedComponentDigest = d.AcceptedComponentDigest, PopulationDigests = d.PopulationDigests,
                PopulationParticipantKeys = d.PopulationParticipantKeys, MembershipDigest = d.MembershipDigest,
                ReconciledPlacementDigest = d.ReconciledPlacementDigest,
                RequiredConfigurationDigest = d.RequiredConfigurationDigest,
                RequiredDatabaseIdentityDigest = d.RequiredDatabaseIdentityDigest,
                RequiredSchemaDigest = d.RequiredSchemaDigest, RequiredAssemblyDigest = d.RequiredAssemblyDigest,
                RequiredRuntimeAssemblyDigest = d.RequiredRuntimeAssemblyDigest,
                HistoricalMembers = d.HistoricalMembers, ReconciledPlacements = d.ReconciledPlacements
            };
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(mutated, new(), configuration),
                "D10_BACKEND_SUCCESSOR_PREDECESSOR_DECISION_CHANGED");
        }

        [Fact]
        public void PredecessorBuildMustMatchTheCommissionedConfiguration()
        {
            var evidence = Evidence();
            var (configuration, json) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone,
                values => values["Deployment:BuildId"] = "other-build");
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision, new(), configuration),
                "D10_BACKEND_SUCCESSOR_PREDECESSOR_BUILD_MISMATCH");
        }

        [Fact]
        public void ReleaseProvenanceMustBeWellFormed()
        {
            var evidence = Evidence();
            var (configuration, json) = Staged(evidence, AQGreenD10BackendSuccessor.SchemaTransitionNone);
            var bytes = Encoding.UTF8.GetBytes(json.Replace("\"releaseRunAttempt\":1", "\"releaseRunAttempt\":0"));
            var values = new Dictionary<string, string>
            {
                ["App:AQGreenV2:D10:BackendSuccessorJsonBase64"] = Convert.ToBase64String(bytes),
                ["App:AQGreenV2:D10:BackendSuccessorSha256"] = AQGreenB6CanonicalEvidence.Hash(bytes),
                ["Deployment:BuildId"] = "synthetic-d10-offline", ["RENDER_GIT_COMMIT"] = Revision
            };
            AssertCode(() => AQGreenD10BackendSuccessor.Accept(evidence.Decision,
                    new() { SourceSchemaVersion = evidence.Decision.RequiredSchemaDigest },
                    new ConfigurationBuilder().AddInMemoryCollection(values).Build()),
                "D10_BACKEND_SUCCESSOR_RELEASE_PROVENANCE_INVALID");
        }
    }
}
