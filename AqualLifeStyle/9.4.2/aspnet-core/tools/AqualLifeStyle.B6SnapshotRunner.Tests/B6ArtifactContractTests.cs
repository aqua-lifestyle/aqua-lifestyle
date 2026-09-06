using System.Diagnostics;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.B6SnapshotRunner.Tests;

public sealed class B6ArtifactContractTests : IDisposable
{
    private readonly string _snapshot = RequiredEnvironment("B6_TEST_SNAPSHOT");
    private readonly string _raw = RequiredEnvironment("B6_TEST_RAW");
    private readonly string _baseManifest = RequiredEnvironment("B6_TEST_BASE_MANIFEST");
    private readonly string _finalManifest = RequiredEnvironment("B6_TEST_FINAL_MANIFEST");
    private readonly string _rootDispositions = RequiredEnvironment("B6_TEST_ROOT_DISPOSITIONS");
    private readonly string _receipt = RequiredEnvironment("B6_TEST_RECEIPT");
    private readonly string _runtimeState = RequiredEnvironment("B6_TEST_RUNTIME_STATE");
    private readonly string _analysis = RequiredEnvironment("B6_TEST_ANALYSIS");
    private readonly string _simulation = RequiredEnvironment("B6_TEST_SIMULATION");
    private readonly string _impact = RequiredEnvironment("B6_TEST_IMPACT");
    private readonly string _inventory = RequiredEnvironment("B6_TEST_INVENTORY");
    private readonly string _driftInventory = RequiredEnvironment("B6_TEST_DRIFT_INVENTORY");
    private readonly string _secret = RequiredEnvironment("B6_TEST_SECRET");
    private readonly string _runner = RequiredEnvironment("B6_TEST_RUNNER");
    private readonly string _driftSnapshot = RequiredEnvironment("B6_TEST_DRIFT_SNAPSHOT");
    private readonly string _driftManifest = RequiredEnvironment("B6_TEST_DRIFT_MANIFEST");
    private readonly List<string> _temporaryRoots = [];
    private readonly B6Allowlist _allowlist = new(null);

    [Fact]
    public void BaseAndFinalManifests_AuthoritativelyReverify()
    {
        Run("verify", "--snapshot", _snapshot, "--manifest", _baseManifest).ShouldBe(0);
        var final = RunWithInventoryAuthorityCaptured("verify", "b6_snapshot", "--snapshot", _snapshot,
            "--manifest", _finalManifest, "--root-dispositions", _rootDispositions, "--inventory", _inventory);
        final.ExitCode.ShouldBe(0, final.Error);
    }

    [Fact]
    public void PublishedJsonContracts_MatchGeneratedArtifactFields()
    {
        var package = B6Paths.PackageRoot();
        AssertSchemaFields(Path.Combine(package, "manifest-schema.json"), _baseManifest);
        AssertSchemaFields(Path.Combine(package, "manifest-schema.json"), _finalManifest);
        AssertSchemaFields(Path.Combine(package, "handoff-receipt-schema.json"), _receipt);
        CanonicalJson.Read(Path.Combine(package, "root-disposition-schema.json"), controlled: false)
            .AsObject()["$schema"].ShouldNotBeNull();
    }

    [Fact]
    public void TamperedFinalAnalysisDigest_FailsEvenWhenManifestHashIsResealed()
    {
        var root = PrivateRoot();
        var path = Path.Combine(root, "final.json");
        var value = CanonicalJson.Read(_finalManifest).AsObject();
        value["analysisDigests"]!["acceptedComponentDigest"] = new string('f', 64);
        value.Remove("manifestHash");
        value["manifestHash"] = CanonicalJson.Digest(value);
        CanonicalJson.WriteNew(path, value);

        RunWithInventoryAuthority("verify", "b6_snapshot", "--snapshot", _snapshot, "--manifest", path,
            "--root-dispositions", _rootDispositions, "--inventory", _inventory).ShouldBe(2);
    }

    [Fact]
    public void CallerSuppliedSimulation_IsNotARecognisedFinalizationInput()
    {
        Run("finalize-manifest", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--root-dispositions", _rootDispositions, "--inventory", _inventory,
            "--simulation", _simulation).ShouldBe(2);
        Run("impact-report", "--snapshot", _snapshot, "--manifest", _finalManifest,
            "--root-dispositions", _rootDispositions, "--inventory", _inventory,
            "--simulation", _baseManifest,
            "--output", Path.Combine(PrivateRoot(), "impact.json")).ShouldBe(2);
    }

    [Fact]
    public void OperationalEvidence_IsRejectedForThisDirtyUnreviewedExecutionSet()
    {
        var result = RunCaptured(B6Contract.OperationalEvidence, "verify", "--snapshot", _snapshot,
            "--manifest", _baseManifest);

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("exact clean reviewed Git worktree");
    }

    [Fact]
    public void ArtifactVerification_RejectsEveryRequiredAssemblyMismatchAndMissingDependency()
    {
        AssertToolchainMutationRejected(metadata =>
            metadata["toolchainIdentity"]!["runner"]!["assemblySha256"] = new string('f', 64));
        AssertToolchainMutationRejected(metadata =>
            metadata["toolchainIdentity"]!["firstPartyDependencies"]!.AsArray()[0]!["assemblySha256"] = new string('f', 64));
        AssertToolchainMutationRejected(metadata =>
            metadata["toolchainIdentity"]!["firstPartyDependencies"]!.AsArray()[1]!["assemblySha256"] = new string('f', 64));
        AssertToolchainMutationRejected(metadata =>
            metadata["toolchainIdentity"]!["firstPartyDependencies"]!.AsArray().RemoveAt(1));

        var dependencies = CanonicalJson.Read(Path.Combine(_snapshot, "snapshot-metadata.json"))
            ["toolchainIdentity"]!["firstPartyDependencies"]!.AsArray()
            .Select(value => value!["assemblyName"]!.GetValue<string>()).ToList();
        dependencies.ShouldBe(["AqualLifeStyle.Core", "AqualLifeStyle.EntityFrameworkCore"]);
    }

    [Fact]
    public void FutureCutoff_IsRejectedAtArtifactTrustBoundaries_WhileEqualityIsAccepted()
    {
        var futureRaw = CopyControlledDirectory(_raw, "raw-future");
        RewriteJson(Path.Combine(futureRaw, "source-metadata.json"), value =>
        {
            var observedAt = B6Values.ParseTimestamp(value["sourceObservedAt"]!.GetValue<string>());
            value["analysisSnapshotCutoff"] = B6Values.Timestamp(observedAt.AddTicks(10));
        });
        var futureRawResult = RunCaptured(B6Contract.PrecommitEvidence, "sanitize", "--input", futureRaw,
            "--output", Path.Combine(Path.GetDirectoryName(futureRaw)!, "snapshot-from-future-raw"),
            "--key-file", _secret, "--snapshot-id", "B6-FUTURE-RAW-0001");
        futureRawResult.ExitCode.ShouldBe(2);
        futureRawResult.Error.ShouldContain("cannot be later than source observation");

        var equalRaw = CopyControlledDirectory(_raw, "raw-equal");
        RewriteJson(Path.Combine(equalRaw, "source-metadata.json"), value =>
            value["analysisSnapshotCutoff"] = value["sourceObservedAt"]!.DeepClone());
        Run("sanitize", "--input", equalRaw,
            "--output", Path.Combine(Path.GetDirectoryName(equalRaw)!, "snapshot-from-equal-raw"),
            "--key-file", _secret, "--snapshot-id", "B6-EQUAL-RAW-0001",
            "--created-at", "2026-09-03T00:00:00Z").ShouldBe(0);

        var (futureSnapshot, _) = CopySnapshot();
        var futureMetadata = Path.Combine(futureSnapshot, "snapshot-metadata.json");
        var future = CanonicalJson.Read(futureMetadata).AsObject();
        var observed = B6Values.ParseTimestamp(future["sourceObservedAt"]!.GetValue<string>());
        future["analysisSnapshotCutoff"] = B6Values.Timestamp(observed.AddTicks(10));
        SecureFiles.DeleteControlledFile(futureMetadata);
        CanonicalJson.WriteNew(futureMetadata, future);
        var futureManifest = Path.Combine(Path.GetDirectoryName(futureSnapshot)!, "future-base.json");
        Run("manifest", "--snapshot", futureSnapshot, "--output", futureManifest).ShouldBe(2);
        Run("verify", "--snapshot", futureSnapshot, "--manifest", _baseManifest).ShouldBe(2);
        RunWithInventoryAuthority("finalize-manifest", "b6_snapshot", "--snapshot", futureSnapshot, "--base-manifest", _baseManifest,
            "--root-dispositions", _rootDispositions, "--inventory", _inventory,
            "--analysis-output", Path.Combine(Path.GetDirectoryName(futureSnapshot)!, "future-analysis.json"),
            "--simulation-output", Path.Combine(Path.GetDirectoryName(futureSnapshot)!, "future-simulation.json"),
            "--impact-output", Path.Combine(Path.GetDirectoryName(futureSnapshot)!, "future-impact.json"),
            "--output", Path.Combine(Path.GetDirectoryName(futureSnapshot)!, "future-final.json")).ShouldBe(2);
        RunWithInventoryAuthority("verify", "b6_snapshot", "--snapshot", futureSnapshot, "--manifest", _finalManifest,
            "--root-dispositions", _rootDispositions, "--inventory", _inventory).ShouldBe(2);

        var futureDatabaseManifest = Path.Combine(Path.GetDirectoryName(futureSnapshot)!, "future-database-base.json");
        var futureDatabase = CanonicalJson.Read(_baseManifest).AsObject();
        futureDatabase["analysisSnapshotCutoff"] = B6Values.Timestamp(observed.AddTicks(10));
        ResealManifest(futureDatabase);
        CanonicalJson.WriteNew(futureDatabaseManifest, futureDatabase);
        var futureDatabaseResult = RunCaptured(B6Contract.PrecommitEvidence, "verify-db",
            "--runtime-state", _runtimeState, "--schema", "b6_snapshot", "--manifest", futureDatabaseManifest);
        futureDatabaseResult.ExitCode.ShouldBe(2);
        futureDatabaseResult.Error.ShouldContain("cannot be later than source observation");

        var (equalSnapshot, _) = CopySnapshot();
        var equalMetadata = Path.Combine(equalSnapshot, "snapshot-metadata.json");
        var equal = CanonicalJson.Read(equalMetadata).AsObject();
        equal["analysisSnapshotCutoff"] = equal["sourceObservedAt"]!.DeepClone();
        SecureFiles.DeleteControlledFile(equalMetadata);
        CanonicalJson.WriteNew(equalMetadata, equal);
        Run("manifest", "--snapshot", equalSnapshot, "--output",
            Path.Combine(Path.GetDirectoryName(equalSnapshot)!, "equal-base.json")).ShouldBe(0);
    }

    [Fact]
    public void UnrelatedPostCutoffActiveParticipant_IsExcludedWithoutContaminatingAcceptedComponent()
    {
        var snapshot = SnapshotWithFutureActiveParticipant(out var futureKey, out _);
        var analysis = Analyse(snapshot, "future-unrelated");

        analysis["counts"]!["acceptedComponents"]!.GetValue<int>().ShouldBe(1);
        analysis["populations"]!["canonicalisation"]!.AsArray()
            .ShouldNotContain(value => value!.GetValue<string>() == futureKey);
        analysis["populations"]!["qualificationEligibility"]!.AsArray()
            .ShouldNotContain(value => value!.GetValue<string>() == futureKey);
        analysis["components"]!.AsArray().ShouldNotContain(component => component!["participantSnapshotKeys"]!
            .AsArray().Any(value => value!.GetValue<string>() == futureKey));
    }

    [Fact]
    public void CutoffEffectiveSponsorEdgeToPostCutoffParticipant_QuarantinesAffectedComponent()
    {
        var snapshot = SnapshotWithFutureActiveParticipant(out var futureKey, out var futureCustomer);
        var accepted = AcceptedComponentParticipantKeys();
        var child = Rows(snapshot, "EntryParticipations").Single(row => accepted.Contains(row["Id"]) && row["RecruiterCustomerId"] != B6Contract.Null);
        child["RecruiterCustomerId"] = futureCustomer;
        Rewrite(snapshot, "EntryParticipations", rows =>
            rows.Single(row => row["Id"] == child["Id"])["RecruiterCustomerId"] = futureCustomer);
        Rewrite(snapshot, "EntryRecruiterCorrections", rows =>
            rows.Where(row => row["EntryParticipationId"] == child["Id"]).OrderBy(row => row["CorrectedAt"]).Last()["NewRecruiterCustomerId"] = futureCustomer);

        var analysis = Analyse(snapshot, "future-sponsor");
        var component = analysis["components"]!.AsArray().Single(item => item!["participantSnapshotKeys"]!
            .AsArray().Any(value => value!.GetValue<string>() == child["Id"]))!;
        component["status"]!.GetValue<string>().ShouldBe("QUARANTINED");
        component["participantSnapshotKeys"]!.AsArray().ShouldContain(value => value!.GetValue<string>() == futureKey);
        component["anomalies"]!.AsArray().ShouldContain(value =>
            value!["code"]!.GetValue<string>() == "CONTRADICTORY_POST_CUTOFF_TOPOLOGY");
    }

    [Fact]
    public void CutoffEffectiveCorrectionChainThroughPostCutoffParticipant_QuarantinesWithoutSkipping()
    {
        var snapshot = SnapshotWithFutureActiveParticipant(out var futureKey, out var futureCustomer);
        var accepted = AcceptedComponentParticipantKeys();
        var child = Rows(snapshot, "EntryParticipations").Single(row => accepted.Contains(row["Id"]) && row["RecruiterCustomerId"] != B6Contract.Null);
        var currentRecruiter = child["RecruiterCustomerId"];
        Rewrite(snapshot, "EntryRecruiterCorrections", rows =>
        {
            var template = new Dictionary<string, string>(rows.First(), StringComparer.Ordinal);
            template["Id"] = "72000000-0000-0000-8000-000000000001";
            template["EntryParticipationId"] = child["Id"];
            template["PreviousRecruiterCustomerId"] = currentRecruiter;
            template["NewRecruiterCustomerId"] = futureCustomer;
            template["CorrectedAt"] = "2026-08-14T00:00:00.000000Z";
            rows.Add(template);
            var restore = new Dictionary<string, string>(template, StringComparer.Ordinal)
            {
                ["Id"] = "72000000-0000-0000-8000-000000000002",
                ["PreviousRecruiterCustomerId"] = futureCustomer,
                ["NewRecruiterCustomerId"] = currentRecruiter,
                ["CorrectedAt"] = "2026-08-15T00:00:00.000000Z"
            };
            rows.Add(restore);
        });

        var analysis = Analyse(snapshot, "future-correction");
        var component = analysis["components"]!.AsArray().Single(item => item!["participantSnapshotKeys"]!
            .AsArray().Any(value => value!.GetValue<string>() == child["Id"]))!;
        component["status"]!.GetValue<string>().ShouldBe("QUARANTINED");
        component["participantSnapshotKeys"]!.AsArray().ShouldContain(value => value!.GetValue<string>() == futureKey);
        component["anomalies"]!.AsArray().ShouldContain(value =>
            value!["code"]!.GetValue<string>() == "CONTRADICTORY_POST_CUTOFF_TOPOLOGY");
    }

    [Fact]
    public void PostCutoffParticipantHistoricalFinance_RemainsImpactOnly()
    {
        var snapshot = SnapshotWithFutureActiveParticipant(out var futureKey, out var futureCustomer);
        Rewrite(snapshot, "EntryWeeklyCommissions", rows =>
        {
            var row = rows[0];
            row["EntryParticipationId"] = futureKey;
            row["CustomerId"] = futureCustomer;
        });
        var analysis = Analyse(snapshot, "future-finance");

        analysis["populations"]!["historicalImpactInventory"]!.AsArray()
            .ShouldContain(value => value!.GetValue<string>() == futureKey);
        analysis["populations"]!["canonicalisation"]!.AsArray()
            .ShouldNotContain(value => value!.GetValue<string>() == futureKey);
    }

    [Fact]
    public void Inventory_IsBoundToVerifiedSnapshotBaseQueryAndFinalManifest()
    {
        var baseline = RunWithInventoryAuthorityCaptured("verify-inventory", "b6_snapshot",
            "--snapshot", _snapshot, "--base-manifest", _baseManifest, "--inventory", _inventory);
        baseline.ExitCode.ShouldBe(0, baseline.Error);
        RunWithInventoryAuthority("verify-inventory", "b6_snapshot", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--inventory", _driftInventory).ShouldBe(2);
        RunWithInventoryAuthority("verify-inventory", "b6_drift", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--inventory", _inventory).ShouldBe(2);
        RunWithInventoryAuthority("verify", "b6_snapshot", "--snapshot", _snapshot, "--manifest", _finalManifest,
            "--root-dispositions", _rootDispositions, "--inventory", _driftInventory).ShouldBe(2);
        Run("finalize-manifest", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--root-dispositions", _rootDispositions, "--inventory-digest", new string('f', 64)).ShouldBe(2);

        var tampered = Path.Combine(PrivateRoot(), "inventory-tampered.json");
        var value = CanonicalJson.Read(_inventory).AsObject();
        value["resultSets"]!.AsArray()[0]!["rows"]!.AsArray().Add(new JsonArray("tampered"));
        value["inventoryResultDigest"] = CanonicalJson.Digest(value["resultSets"]!);
        ResealInventory(value);
        CanonicalJson.WriteNew(tampered, value);
        RunWithInventoryAuthority("verify-inventory", "b6_snapshot", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--inventory", tampered).ShouldBe(2);

        var wrongQuery = Path.Combine(PrivateRoot(), "inventory-query.json");
        value = CanonicalJson.Read(_inventory).AsObject();
        value["inventoryQueryDigest"] = new string('f', 64);
        ResealInventory(value);
        CanonicalJson.WriteNew(wrongQuery, value);
        RunWithInventoryAuthority("verify-inventory", "b6_snapshot", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--inventory", wrongQuery).ShouldBe(2);

        var changedSql = Path.Combine(PrivateRoot(), "inventory.sql");
        SecureFiles.WriteNew(changedSql, stream =>
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(File.ReadAllText(B6Paths.InventorySql()));
            writer.WriteLine("-- changed authoritative definition");
            writer.Flush();
        });
        RunWithInventoryAuthority("verify-inventory", "b6_snapshot", changedSql,
            "--snapshot", _snapshot, "--base-manifest", _baseManifest, "--inventory", _inventory).ShouldBe(2);

        var wrongSource = Path.Combine(PrivateRoot(), "inventory-source.json");
        value = CanonicalJson.Read(_inventory).AsObject();
        value["sourceSnapshotDigest"] = new string('f', 64);
        ResealInventory(value);
        CanonicalJson.WriteNew(wrongSource, value);
        RunWithInventoryAuthority("verify-inventory", "b6_snapshot", "--snapshot", _snapshot, "--base-manifest", _baseManifest,
            "--inventory", wrongSource).ShouldBe(2);

        var final = CanonicalJson.Read(_finalManifest).AsObject();
        final["analysisDigests"]!["inventoryDigest"]!.GetValue<string>()
            .ShouldBe(CanonicalJson.Read(_inventory)["inventoryDigest"]!.GetValue<string>());
    }

    [Fact]
    public void RestoredSchemaAndReviewedRoots_AreBoundToExactSnapshotAndBaseAuthority()
    {
        var (identitySnapshot, _) = CopySnapshot();
        RewriteJson(Path.Combine(identitySnapshot, "snapshot-metadata.json"), value =>
            value["snapshotId"] = "B6-SYNTHETIC-IDENTITY");
        var identityBase = Path.Combine(Path.GetDirectoryName(identitySnapshot)!, "identity-base.json");
        Run("manifest", "--snapshot", identitySnapshot, "--output", identityBase).ShouldBe(0);
        Run("restore", "--snapshot", identitySnapshot, "--manifest", identityBase,
            "--runtime-state", _runtimeState, "--schema", "b6_identity").ShouldBe(0);

        RunWithInventoryAuthority("verify-inventory", "b6_identity", "--snapshot", _snapshot,
            "--base-manifest", _baseManifest, "--inventory", _inventory).ShouldBe(2);

        var (changedAuthoritySnapshot, _) = CopySnapshot();
        RewriteJson(Path.Combine(changedAuthoritySnapshot, "snapshot-metadata.json"), value =>
            value["sourceMvccSnapshotEvidence"] = new string('e', 64));
        var changedAuthorityBase = Path.Combine(Path.GetDirectoryName(changedAuthoritySnapshot)!, "changed-authority-base.json");
        Run("manifest", "--snapshot", changedAuthoritySnapshot, "--output", changedAuthorityBase).ShouldBe(0);
        Run("analyse", "--snapshot", changedAuthoritySnapshot, "--manifest", changedAuthorityBase,
            "--root-dispositions", _rootDispositions,
            "--output", Path.Combine(Path.GetDirectoryName(changedAuthoritySnapshot)!, "changed-authority-analysis.json")).ShouldBe(2);
    }

    [Fact]
    public void Receipt_RequiresVerifiedFinalManifestAndBindsIt()
    {
        RunWithInventoryAuthority("handoff-receipt", "b6_snapshot", "--snapshot", _snapshot, "--manifest", _baseManifest,
            "--root-dispositions", _rootDispositions, "--inventory", _inventory,
            "--approved-destination", "TEST",
            "--output", Path.Combine(PrivateRoot(), "receipt.json")).ShouldBe(2);
        var receipt = CanonicalJson.Read(_receipt).AsObject();
        receipt["verificationResult"]!.GetValue<string>().ShouldBe("PASSED");
        receipt["finalManifestDigest"]!.GetValue<string>().ShouldBe(
            CanonicalJson.Read(_finalManifest)["manifestHash"]!.GetValue<string>());
    }

    [Fact]
    public void Extraction_IsTenantBoundedAndMinimal()
    {
        Rows(_raw, "AbpTenants").Select(row => row["Id"]).ShouldBe(["1"]);
        Rows(_raw, "EntryParticipations").Select(row => row["TenantId"]).Distinct().ShouldBe(["1"]);
        Rows(_raw, "EntryParticipations").Select(row => row["Id"])
            .ShouldNotContain("20000000-0000-0000-8000-000000000001");
        var invitations = Rows(_raw, "ProgrammeInvitations");
        invitations.Count.ShouldBe(1);
        invitations[0]["Id"].ShouldBe("30000000-0000-0000-8000-000000000011");
        invitations.ShouldAllBe(row => row["ProgrammeKey"] == "AQGREEN" && row["IsDeleted"] == "false");
    }

    [Fact]
    public void AttributionShapesAndChronology_AreSealed()
    {
        var rows = Rows(_snapshot, "AQGreenRecruitmentAttributions");
        rows.Select(row => (row["AttributionKind"], row["AcquisitionSource"]))
            .ToHashSet().SetEquals([("2", "2"), ("1", "1")]).ShouldBeTrue();
        var direct = rows.Single(row => row["AcquisitionSource"] == "2");
        var member = rows.Single(row => row["AcquisitionSource"] == "1");
        var invitations = Rows(_snapshot, "ProgrammeInvitations").Select(row => row["Id"]).ToHashSet();
        invitations.ShouldNotContain(direct["SourceReferenceId"]);
        invitations.ShouldContain(member["SourceReferenceId"]);
        direct["ActorPresent"].ShouldBe("true");
        direct["AssignmentReasonPresent"].ShouldBe("true");
        member["AssignmentReasonPresent"].ShouldBe("false");
        var confirmations = Rows(_snapshot, "AQGreenRecruitmentAttributionConfirmations");
        confirmations.ShouldAllBe(row => row["ActorPresent"] == "true");
        foreach (var confirmation in confirmations)
        {
            var attribution = rows.Single(row => row["Id"] == confirmation["AttributionId"]);
            B6Values.ParseTimestamp(confirmation["ConfirmedAt"])
                .ShouldBeGreaterThanOrEqualTo(B6Values.ParseTimestamp(attribution["AttributedAt"]));
        }
    }

    [Fact]
    public void FixtureExtractionSets_AreExactForTopologyAndHistoricalImpact()
    {
        Rows(_raw, "EntryRecruiterCorrections").Select(row => row["Id"]).ToHashSet().SetEquals([
            "22000000-0000-0000-8000-000000000010",
            "22000000-0000-0000-8000-000000000011"]).ShouldBeTrue();
        Rows(_raw, "AQGreenNetworkPlacements").Select(row => row["Id"])
            .ShouldBe(["41000000-0000-0000-8000-000000000001"]);
        Rows(_raw, "EntryWeeklyCommissions").Select(row => row["PayoutStatus"]).ToHashSet()
            .SetEquals(["3", "4"]).ShouldBeTrue();
        Rows(_raw, "OnyxLoanAgreements").Select(row => row["Id"])
            .ShouldBe(["60000000-0000-0000-8000-000000000001"]);
        Rows(_raw, "OnyxGraduationDecisions").Select(row => row["Id"])
            .ShouldBe(["62000000-0000-0000-8000-000000000001"]);
        Rows(_raw, "OnyxParticipations").Select(row => row["Id"])
            .ShouldBe(["61000000-0000-0000-8000-000000000001"]);
    }

    [Fact]
    public void DuplicateDeliveredPrimaryKey_IsRejectedBeforeMapMaterialization()
    {
        var (snapshot, manifest) = CopySnapshot();
        var table = _allowlist.ByName["EntryParticipations"];
        var path = Path.Combine(snapshot, "EntryParticipations.csv");
        var rows = CsvContract.Read(path, table.Fields.Select(field => field.Output).ToList());
        rows.Add(new Dictionary<string, string>(rows[0], StringComparer.Ordinal));
        SecureFiles.DeleteControlledFile(path);
        CsvContract.WriteNew(path, table.Fields.Select(field => field.Output).ToList(), rows);

        Run("verify", "--snapshot", snapshot, "--manifest", manifest).ShouldBe(2);
    }

    [Fact]
    public void UnresolvedRecruiterRelationship_FailsDelivery()
    {
        var (snapshot, manifest) = CopySnapshot();
        Rewrite(snapshot, "EntryParticipations", rows => rows[1]["RecruiterCustomerId"] = "1999999999");

        Run("verify", "--snapshot", snapshot, "--manifest", manifest).ShouldBe(2);
    }

    [Fact]
    public void ConfirmationBeforeAttribution_IsRejected()
    {
        var (snapshot, manifest) = CopySnapshot();
        Rewrite(snapshot, "AQGreenRecruitmentAttributionConfirmations", rows =>
            rows[0]["ConfirmedAt"] = "2025-01-01T00:00:00.000000Z");

        Run("verify", "--snapshot", snapshot, "--manifest", manifest).ShouldBe(2);
    }

    [Fact]
    public void RootReviewAndExceptionalComponents_AreDistinct()
    {
        var analysis = CanonicalJson.Read(_analysis).AsObject();
        analysis["counts"]!["acceptedComponents"]!.GetValue<int>().ShouldBe(1);
        var components = analysis["components"]!.AsArray();
        var codes = components.SelectMany(component => component!["anomalies"]!.AsArray())
            .Select(anomaly => anomaly!["code"]!.GetValue<string>()).ToHashSet();
        codes.ShouldContain("MISSING_ROOT_EVIDENCE");
        codes.ShouldContain("MISSING_APPROVAL_EVIDENCE");
        codes.ShouldContain("EXCEPTION_ANCESTOR");
        var accepted = components.Single(component => component!["status"]!.GetValue<string>() == "ACCEPTED")!;
        accepted["rootSnapshotKey"].ShouldNotBeNull();
        accepted["participantSnapshotKeys"]!.AsArray().Select(value => value!.GetValue<string>()).ToHashSet()
            .SetEquals(analysis["populations"]!["canonicalisation"]!.AsArray().Select(value => value!.GetValue<string>())).ShouldBeTrue();
    }

    [Fact]
    public void PaidFixture_UsesNativeEnumAndCoherentTimestamps()
    {
        var rows = Rows(_snapshot, "EntryWeeklyCommissions");
        var paid = rows.Single(row => row["PayoutStatus"] == "4");
        paid["ReleasedAt"].ShouldNotBe(B6Contract.Null);
        paid["PaidAt"].ShouldNotBe(B6Contract.Null);
        var released = rows.Single(row => row["PayoutStatus"] == "3");
        released["ReleasedAt"].ShouldNotBe(B6Contract.Null);
        released["PaidAt"].ShouldBe(B6Contract.Null);
    }

    [Fact]
    public void QuarantinedComponent_HistoricalImpactIsInventoryOnlyAndFullyReported()
    {
        var analysis = CanonicalJson.Read(_analysis).AsObject();
        var simulation = CanonicalJson.Read(_simulation).AsObject();
        var impact = CanonicalJson.Read(_impact).AsObject();
        var quarantinedKeys = analysis["components"]!.AsArray()
            .Where(component => component!["status"]!.GetValue<string>() == "QUARANTINED")
            .SelectMany(component => component!["participantSnapshotKeys"]!.AsArray())
            .Select(value => value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var historicalParticipant = Rows(_snapshot, "OnyxLoanAgreements").Single()["EntryParticipationId"];
        quarantinedKeys.ShouldContain(historicalParticipant);

        impact["historicalFactsModified"]!.GetValue<bool>().ShouldBeFalse();
        impact["historicalCommissionIntersections"]!.GetValue<int>().ShouldBeGreaterThanOrEqualTo(1);
        impact["payoutStateIntersections"]!["3"]!.GetValue<int>().ShouldBe(1);
        impact["loanIntersections"]!.GetValue<int>().ShouldBe(1);
        impact["aqGreenGraduationIntersections"]!.GetValue<int>().ShouldBe(1);
        impact["onyxParticipationIntersections"]!.GetValue<int>().ShouldBe(1);
        impact["structural"]!["quarantinedComponentParticipants"]!.GetValue<int>().ShouldBeGreaterThan(0);

        var participantImpact = impact["participantImpacts"]!.AsArray().Single(item =>
            item!["participantSnapshotKey"]!.GetValue<string>() == historicalParticipant)!;
        participantImpact["componentStatus"]!.GetValue<string>().ShouldBe("QUARANTINED");
        participantImpact["structuralClassification"]!.GetValue<string>().ShouldBe("STRUCTURALLY_AMBIGUOUS");
        participantImpact["simulatedV2StructuralLevel"]!.GetValue<string>()
            .ShouldBe("NOT_SIMULATED_COMPONENT_QUARANTINED");
        participantImpact["isImpactIntersection"]!.GetValue<bool>().ShouldBeTrue();
        participantImpact["historicalEvidence"]!["commissionRecords"]!.GetValue<int>().ShouldBe(1);
        participantImpact["historicalEvidence"]!["loanRecords"]!.GetValue<int>().ShouldBe(1);
        participantImpact["historicalEvidence"]!["graduationRecords"]!.GetValue<int>().ShouldBe(1);
        participantImpact["historicalEvidence"]!["resultingOnyxParticipationRecords"]!.GetValue<int>().ShouldBe(1);

        simulation["placements"]!.AsArray().ShouldNotContain(item =>
            quarantinedKeys.Contains(item!["participantSnapshotKey"]!.GetValue<string>()));
        simulation["structuralImpacts"]!.AsArray().ShouldNotContain(item =>
            quarantinedKeys.Contains(item!["participantSnapshotKey"]!.GetValue<string>()));
        analysis["populations"]!["canonicalisation"]!.AsArray().ShouldNotContain(value =>
            quarantinedKeys.Contains(value!.GetValue<string>()));

        var acceptedHistoricalParticipant = Rows(_snapshot, "EntryWeeklyCommissions")
            .Single(row => row["PayoutStatus"] == "4")["EntryParticipationId"];
        var acceptedImpact = impact["participantImpacts"]!.AsArray().Single(item =>
            item!["participantSnapshotKey"]!.GetValue<string>() == acceptedHistoricalParticipant)!;
        acceptedImpact["componentStatus"]!.GetValue<string>().ShouldBe("ACCEPTED");
        acceptedImpact["structuralClassification"]!.GetValue<string>().ShouldBe("DETERMINISTIC_SAME");
        acceptedImpact["isImpactIntersection"]!.GetValue<bool>().ShouldBeFalse();
        impact["payoutStateIntersections"]!.AsObject().ContainsKey("4").ShouldBeFalse();
    }

    [Fact]
    public void QuarantinedHistoricalImpact_ChangesAuthoritativelyRecomputedImpactAndFinalDigests()
    {
        var (snapshot, _) = CopySnapshot();
        Rewrite(snapshot, "EntryWeeklyCommissions", rows =>
            rows.RemoveAll(row => row["PayoutStatus"] == "3"));
        var root = Path.GetDirectoryName(snapshot)!;
        var baseManifest = Path.Combine(root, "changed-impact-base.json");
        var analysis = Path.Combine(root, "changed-impact-analysis.json");
        var simulation = Path.Combine(root, "changed-impact-simulation.json");
        var impact = Path.Combine(root, "changed-impact.json");
        var inventory = Path.Combine(root, "changed-impact-inventory.json");
        var finalManifest = Path.Combine(root, "changed-impact-final.json");

        Run("manifest", "--snapshot", snapshot, "--output", baseManifest).ShouldBe(0);
        Run("restore", "--snapshot", snapshot, "--manifest", baseManifest,
            "--runtime-state", _runtimeState, "--schema", "b6_changed_impact").ShouldBe(0);
        Run("inventory", "--snapshot", snapshot, "--base-manifest", baseManifest,
            "--runtime-state", _runtimeState, "--schema", "b6_changed_impact",
            "--sql-file", B6Paths.InventorySql(), "--output", inventory).ShouldBe(0);
        var changedRoots = BindRootDispositions(baseManifest, "changed-impact");
        var finalize = RunWithInventoryAuthorityCaptured("finalize-manifest", "b6_changed_impact", "--snapshot", snapshot, "--base-manifest", baseManifest,
            "--root-dispositions", changedRoots, "--inventory", inventory, "--analysis-output", analysis,
            "--simulation-output", simulation, "--impact-output", impact, "--output", finalManifest);
        finalize.ExitCode.ShouldBe(0, finalize.Error);
        var verify = RunWithInventoryAuthorityCaptured("verify", "b6_changed_impact", "--snapshot", snapshot,
            "--manifest", finalManifest, "--root-dispositions", changedRoots, "--inventory", inventory);
        verify.ExitCode.ShouldBe(0, verify.Error);

        var originalFinal = CanonicalJson.Read(_finalManifest).AsObject();
        var changedFinal = CanonicalJson.Read(finalManifest).AsObject();
        changedFinal["analysisDigests"]!["inventoryDigest"]!.GetValue<string>()
            .ShouldNotBe(originalFinal["analysisDigests"]!["inventoryDigest"]!.GetValue<string>());
        changedFinal["analysisDigests"]!["impactReportDigest"]!.GetValue<string>()
            .ShouldNotBe(originalFinal["analysisDigests"]!["impactReportDigest"]!.GetValue<string>());
        changedFinal["manifestHash"]!.GetValue<string>().ShouldNotBe(originalFinal["manifestHash"]!.GetValue<string>());
    }

    [Fact]
    public void ControlledArtifacts_AreOwnerOnlyAndDoNotDiscloseKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(_raw).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.GetUnixFileMode(_snapshot).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            foreach (var path in new[] { _baseManifest, _finalManifest, _analysis, _simulation, _impact, _receipt,
                         _runtimeState, _rootDispositions, _secret })
                (File.GetUnixFileMode(path) & ~(UnixFileMode.UserRead | UnixFileMode.UserWrite)).ShouldBe((UnixFileMode)0, path);
            foreach (var path in Directory.GetFiles(_raw).Concat(Directory.GetFiles(_snapshot)))
                (File.GetUnixFileMode(path) & ~(UnixFileMode.UserRead | UnixFileMode.UserWrite)).ShouldBe((UnixFileMode)0, path);
        }
        var key = File.ReadAllBytes(_secret);
        var searchable = string.Concat(File.ReadAllText(_baseManifest), File.ReadAllText(_finalManifest),
            File.ReadAllText(_receipt), File.ReadAllText(_impact));
        searchable.ShouldNotContain(Convert.ToHexString(key));
        searchable.ShouldNotContain(Convert.ToBase64String(key));
    }

    [Fact]
    public void SymlinkedOutputTarget_IsRejectedWithoutChangingVictim()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = PrivateRoot();
        var victim = Path.Combine(root, "victim");
        SecureFiles.WriteNew(victim, stream => stream.Write("unchanged"u8));
        var output = Path.Combine(root, "manifest.json");
        File.CreateSymbolicLink(output, victim);

        Run("manifest", "--snapshot", _snapshot, "--output", output).ShouldBe(2);
        File.ReadAllText(victim).ShouldBe("unchanged");
    }

    [Fact]
    public void ExternalDestinationDsnAndFakeOwnedState_AreRejected()
    {
        Run("restore", "--snapshot", _snapshot, "--manifest", _baseManifest,
            "--dsn", "postgresql://external.invalid/production").ShouldBe(2);
        var root = PrivateRoot();
        var fake = Path.Combine(root, "runtime.json");
        CanonicalJson.WriteNew(fake, new JsonObject { ["runtimeVersion"] = "claimed" });
        Run("restore", "--snapshot", _snapshot, "--manifest", _baseManifest, "--runtime-state", fake).ShouldBe(2);
        Run("runtime-destroy", "--state", fake).ShouldBe(2);
        File.Exists(fake).ShouldBeTrue();
    }

    [Fact]
    public void RuntimeState_ProvesLoopbackPackageOwnedPostgresBoundary()
    {
        var state = CanonicalJson.Read(_runtimeState).AsObject();
        state["host"]!.GetValue<string>().ShouldBe("127.0.0.1");
        state["runtimeVersion"]!.GetValue<string>().ShouldBe(B6Contract.RuntimeVersion);
        state["workerBoundary"]!.GetValue<string>().ShouldBe("POSTGRES_ONLY_CONTAINER_NO_AQUA_APPLICATION_ATTACHED_BY_PACKAGE");
    }

    [Fact]
    public void SourceAndDestinationDatabaseIdentity_CannotBeEqual()
    {
        var (snapshot, _) = CopySnapshot();
        var metadataPath = Path.Combine(snapshot, "snapshot-metadata.json");
        var metadata = CanonicalJson.Read(metadataPath).AsObject();
        metadata["sourceDatabaseIdentityEvidence"] = CanonicalJson.Read(_runtimeState)["databaseIdentityEvidence"]!.DeepClone();
        SecureFiles.DeleteControlledFile(metadataPath);
        CanonicalJson.WriteNew(metadataPath, metadata);
        var manifest = Path.Combine(Path.GetDirectoryName(snapshot)!, "equal-source-base.json");
        Run("manifest", "--snapshot", snapshot, "--output", manifest).ShouldBe(0);

        Run("restore", "--snapshot", snapshot, "--manifest", manifest,
            "--runtime-state", _runtimeState).ShouldBe(2);
    }

    [Fact]
    public void FrozenSourceDrift_ChangesPackageAndPopulationIdentity()
    {
        var original = CanonicalJson.Read(_baseManifest).AsObject();
        var drift = CanonicalJson.Read(_driftManifest).AsObject();
        drift["overallSnapshotDigest"]!.GetValue<string>().ShouldNotBe(original["overallSnapshotDigest"]!.GetValue<string>());
        drift["candidatePopulationDigest"]!.GetValue<string>().ShouldNotBe(original["candidatePopulationDigest"]!.GetValue<string>());
        Run("verify", "--snapshot", _driftSnapshot, "--manifest", _baseManifest).ShouldBe(2);
    }

    [Fact]
    public void InvalidCorrectionHistory_QuarantinesEveryImplicatedRootComponent()
    {
        var (snapshot, _) = CopySnapshot();
        var alternateRecruiter = Rows(snapshot, "EntryParticipations")[3]["CustomerId"];
        Rewrite(snapshot, "EntryRecruiterCorrections", rows =>
            rows.Single(row => row["CorrectedAt"].StartsWith("2026-08-12", StringComparison.Ordinal))
                ["NewRecruiterCustomerId"] = alternateRecruiter);
        var baseManifest = Path.Combine(Path.GetDirectoryName(snapshot)!, "invalid-correction-base.json");
        var analysisPath = Path.Combine(Path.GetDirectoryName(snapshot)!, "invalid-correction-analysis.json");
        Run("manifest", "--snapshot", snapshot, "--output", baseManifest).ShouldBe(0);
        var invalidCorrectionRoots = BindRootDispositions(baseManifest, "invalid-correction");
        Run("analyse", "--snapshot", snapshot, "--manifest", baseManifest,
            "--root-dispositions", invalidCorrectionRoots, "--output", analysisPath).ShouldBe(0);

        var analysis = CanonicalJson.Read(analysisPath).AsObject();
        analysis["counts"]!["acceptedComponents"]!.GetValue<int>().ShouldBe(0);
        var acceptedRoot = CanonicalJson.Read(_rootDispositions)["dispositions"]![0]!["participantSnapshotKey"]!.GetValue<string>();
        var implicated = analysis["components"]!.AsArray().Single(component => component!["participantSnapshotKeys"]!
            .AsArray().Any(value => value!.GetValue<string>() == acceptedRoot))!;
        implicated["status"]!.GetValue<string>().ShouldBe("QUARANTINED");
        implicated["rootCandidateSnapshotKeys"]!.AsArray().Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    public void Dispose()
    {
        foreach (var root in _temporaryRoots)
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private List<Dictionary<string, string>> Rows(string directory, string table)
    {
        var contract = _allowlist.ByName[table];
        var fields = Path.GetFullPath(directory) == Path.GetFullPath(_raw) ? contract.SourceFields : contract.Fields;
        return CsvContract.Read(Path.Combine(directory, table + ".csv"), fields.Select(field => field.Output).ToList());
    }

    private void Rewrite(string snapshot, string table, Action<List<Dictionary<string, string>>> mutation)
    {
        var contract = _allowlist.ByName[table];
        var path = Path.Combine(snapshot, table + ".csv");
        var rows = CsvContract.Read(path, contract.Fields.Select(field => field.Output).ToList());
        mutation(rows);
        SecureFiles.DeleteControlledFile(path);
        CsvContract.WriteNew(path, contract.Fields.Select(field => field.Output).ToList(), rows);
    }

    private (string Snapshot, string Manifest) CopySnapshot()
    {
        var root = PrivateRoot();
        var target = CopyControlledDirectory(_snapshot, "snapshot", root);
        var manifest = Path.Combine(root, "manifest.json");
        File.Copy(_baseManifest, manifest);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(manifest, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return (target, manifest);
    }

    private string CopyControlledDirectory(string sourceDirectory, string name, string? root = null)
    {
        root ??= PrivateRoot();
        var target = Path.Combine(root, name);
        CreatePrivateTestDirectory(target);
        foreach (var source in Directory.GetFiles(sourceDirectory))
        {
            var destination = Path.Combine(target, Path.GetFileName(source));
            File.Copy(source, destination);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return target;
    }

    private string SnapshotWithFutureActiveParticipant(out string participantKey, out string customerKey)
    {
        var (snapshot, _) = CopySnapshot();
        var participant = Rows(snapshot, "EntryParticipations").Single(row => row["Status"] == "0");
        participantKey = participant["Id"];
        var originalCustomer = Rows(snapshot, "Customers").Single(row => row["Id"] == participant["CustomerId"]);
        customerKey = checked(Rows(snapshot, "Customers").Select(row => int.Parse(row["Id"]))
            .Max() + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var isolatedCustomerKey = customerKey;
        Rewrite(snapshot, "Customers", rows =>
        {
            var customer = new Dictionary<string, string>(originalCustomer, StringComparer.Ordinal)
            {
                ["Id"] = isolatedCustomerKey
            };
            rows.Add(customer);
        });
        Rewrite(snapshot, "EntryParticipations", rows =>
        {
            var row = rows.Single(value => value["Id"] == participant["Id"]);
            row["CustomerId"] = isolatedCustomerKey;
            row["Status"] = "2";
            row["ActivatedAt"] = "2026-09-03T01:00:00.000000Z";
        });
        Rewrite(snapshot, "EntryParticipationApprovalDecisions", rows =>
        {
            var row = new Dictionary<string, string>(rows[0], StringComparer.Ordinal)
            {
                ["Id"] = "71000000-0000-0000-8000-000000000001",
                ["EntryParticipationId"] = participant["Id"],
                ["Approved"] = "true",
                ["DecidedAt"] = "2026-09-03T01:00:00.000000Z",
                ["ActorPresent"] = "true"
            };
            rows.Add(row);
        });
        return snapshot;
    }

    private JsonObject Analyse(string snapshot, string name)
    {
        var root = Path.GetDirectoryName(snapshot)!;
        var manifest = Path.Combine(root, name + "-base.json");
        var analysis = Path.Combine(root, name + "-analysis.json");
        Run("manifest", "--snapshot", snapshot, "--output", manifest).ShouldBe(0);
        var roots = BindRootDispositions(manifest, name);
        Run("analyse", "--snapshot", snapshot, "--manifest", manifest,
            "--root-dispositions", roots, "--output", analysis).ShouldBe(0);
        return CanonicalJson.Read(analysis).AsObject();
    }

    private string BindRootDispositions(string baseManifestPath, string name)
    {
        var baseManifest = CanonicalJson.Read(baseManifestPath).AsObject();
        var dispositions = CanonicalJson.Read(_rootDispositions).AsObject();
        dispositions["snapshotId"] = baseManifest["snapshotId"]!.DeepClone();
        dispositions["sourceSnapshotDigest"] = baseManifest["overallSnapshotDigest"]!.DeepClone();
        dispositions["baseManifestDigest"] = baseManifest["manifestHash"]!.DeepClone();
        var path = Path.Combine(Path.GetDirectoryName(baseManifestPath)!, name + "-root-dispositions.json");
        CanonicalJson.WriteNew(path, dispositions);
        return path;
    }

    private HashSet<string> AcceptedComponentParticipantKeys() => CanonicalJson.Read(_analysis)["components"]!.AsArray()
        .Single(component => component!["status"]!.GetValue<string>() == "ACCEPTED")!["participantSnapshotKeys"]!
        .AsArray().Select(value => value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    private static void ResealInventory(JsonObject value)
    {
        value.Remove("inventoryDigest");
        value["inventoryDigest"] = CanonicalJson.Digest(value);
    }

    private static void ResealManifest(JsonObject value)
    {
        value.Remove("manifestHash");
        value["manifestHash"] = CanonicalJson.Digest(value);
    }

    private static void RewriteJson(string path, Action<JsonObject> mutation)
    {
        var value = CanonicalJson.Read(path).AsObject();
        mutation(value);
        SecureFiles.DeleteControlledFile(path);
        CanonicalJson.WriteNew(path, value);
    }

    private void AssertToolchainMutationRejected(Action<JsonObject> mutation)
    {
        var (snapshot, _) = CopySnapshot();
        RewriteJson(Path.Combine(snapshot, "snapshot-metadata.json"), mutation);
        var result = RunCaptured(B6Contract.PrecommitEvidence, "manifest", "--snapshot", snapshot,
            "--output", Path.Combine(Path.GetDirectoryName(snapshot)!, "rejected-toolchain-base.json"));
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("toolchain");
    }

    private string PrivateRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "b6-dotnet-test-" + Guid.NewGuid().ToString("N"));
        CreatePrivateTestDirectory(path);
        _temporaryRoots.Add(path);
        return path;
    }

    private int Run(params string[] arguments)
        => RunWithClassification(B6Contract.PrecommitEvidence, arguments);

    private int RunWithInventoryAuthority(string command, string schema, params string[] arguments) =>
        Run(new[] { command }.Concat(arguments).Concat([
            "--runtime-state", _runtimeState, "--schema", schema, "--sql-file", B6Paths.InventorySql()
        ]).ToArray());

    private ProcessResult RunWithInventoryAuthorityCaptured(string command, string schema, params string[] arguments) =>
        RunCaptured(B6Contract.PrecommitEvidence, new[] { command }.Concat(arguments).Concat([
            "--runtime-state", _runtimeState, "--schema", schema, "--sql-file", B6Paths.InventorySql()
        ]).ToArray());

    private int RunWithInventoryAuthority(string command, string schema, string sqlFile, params string[] arguments) =>
        Run(new[] { command }.Concat(arguments).Concat([
            "--runtime-state", _runtimeState, "--schema", schema, "--sql-file", sqlFile
        ]).ToArray());

    private int RunWithClassification(string classification, params string[] arguments)
        => RunCaptured(classification, arguments).ExitCode;

    private ProcessResult RunCaptured(string classification, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { _runner },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        }.AddArguments(arguments);
        start.Environment[B6Contract.EvidenceClassificationEnvironment] = classification;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode is not (0 or 2)) throw new Xunit.Sdk.XunitException(output + error);
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static string RequiredEnvironment(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required by the real-schema B6 contract suite.");

    private static void CreatePrivateTestDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void AssertSchemaFields(string schemaPath, string artifactPath)
    {
        var schema = CanonicalJson.Read(schemaPath, controlled: false).AsObject();
        var artifact = CanonicalJson.Read(artifactPath).AsObject();
        var properties = schema["properties"]!.AsObject().Select(value => value.Key).ToHashSet(StringComparer.Ordinal);
        artifact.Select(value => value.Key).ShouldAllBe(value => properties.Contains(value));
        var required = schema["required"]!.AsArray().Select(value => value!.GetValue<string>());
        required.ShouldAllBe(value => artifact.ContainsKey(value));
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo AddArguments(this ProcessStartInfo value, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments) value.ArgumentList.Add(argument);
        return value;
    }
}
