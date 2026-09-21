using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.B6SnapshotRunner.Tests;

public sealed class B6PrimitiveContractTests
{
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private const string TestSourceMaterializationMetadataName =
        "B6TestSourceMaterializationDirectory";
    private const string CanonicalGraphSha256 =
        "a32d7081f7c687c9ece1511122dd772ac17e7cfc682943e5706ac0caef6fc4a0";
    private const string QualificationGraphSha256 =
        "d94d67e2dc223c42a69f3cf75e943919eeae26f8ef3ffe8b9f40f8ece1b9672b";
    private static readonly DateTimeOffset SyntheticPackageEntryTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DependencyLocksAndAuthoritativeRunner_EncodeNativeLockedReplayContract()
    {
        var repository = TestSourceMaterializationDirectory();
        var lockContracts = new[]
        {
            (RelativePath: "AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/packages.lock.json",
                Count: 86, Sha256: "9f467700282dc48f8bdc172eb8f1830a3167cbc4efaa223a328b1ff00c4acddc"),
            (RelativePath: "AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/packages.lock.json",
                Count: 187, Sha256: "2b1925d618a39d10fa57e3e8455f06c3565de75ccdc3ae4a9021e357c9d07356"),
            (RelativePath: "AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner/packages.lock.json",
                Count: 168, Sha256: "401fafbcfa35347666a4de3fcfa92be9c66399663d8f8410eca420879c3d7b7b")
        };
        var union = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var contract in lockContracts)
        {
            var path = Path.Combine(repository, contract.RelativePath);
            File.Exists(path).ShouldBeTrue($"Missing candidate lockfile {contract.RelativePath}.");
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant().ShouldBe(contract.Sha256);
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            document.RootElement.GetProperty("version").GetInt32().ShouldBe(1);
            var packages = document.RootElement.GetProperty("dependencies").EnumerateObject()
                .SelectMany(framework => framework.Value.EnumerateObject())
                .Where(dependency => dependency.Value.GetProperty("type").GetString() != "Project")
                .ToArray();
            packages.Length.ShouldBe(contract.Count);
            foreach (var package in packages)
            {
                var resolved = package.Value.GetProperty("resolved").GetString();
                resolved.ShouldNotBeNullOrWhiteSpace();
                package.Value.GetProperty("contentHash").GetString().ShouldNotBeNullOrWhiteSpace();
                union.Add($"{package.Name.ToLowerInvariant()}\t{resolved}\n");
            }
        }

        union.Count.ShouldBe(197);
        var canonicalGraph = string.Concat(union);
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalGraph)))
            .ToLowerInvariant().ShouldBe(CanonicalGraphSha256);

        var testLockPath = Path.Combine(repository,
            "AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner.Tests/packages.lock.json");
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(testLockPath)))
            .ToLowerInvariant().ShouldBe("e575bab05aa361751f35ce31340880c0286e6133c7f36b7e3e1fb2ff8bbf0a47");
        using (var testLock = JsonDocument.Parse(File.ReadAllBytes(testLockPath)))
        {
            var testPackages = testLock.RootElement.GetProperty("dependencies").EnumerateObject()
                .SelectMany(framework => framework.Value.EnumerateObject())
                .Where(dependency => dependency.Value.GetProperty("type").GetString() != "Project")
                .Select(package => $"{package.Name.ToLowerInvariant()}\t" +
                                   $"{package.Value.GetProperty("resolved").GetString()}\n")
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            testPackages.Length.ShouldBe(186);
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(testPackages))))
                .ToLowerInvariant().ShouldBe(QualificationGraphSha256);
        }

        var authorityDirectory = Path.Combine(repository,
            "docs/operations/aqgreen-v2-b6/snapshot-package/authority");
        var authorityContracts = new[]
        {
            (Name: "G197.tsv", Count: 197, Sha256: CanonicalGraphSha256),
            (Name: "TQ186.tsv", Count: 186, Sha256: QualificationGraphSha256),
            (Name: "U215.tsv", Count: 215, Sha256: "372c918aa794e976601ee292caa9c56a884784ebcfe04cd53981f76d3e982dd1"),
            (Name: "U1.tsv", Count: 1, Sha256: "9598c6dd7cab8f0dc62a31be8c73a3c030c5d92d26d120689f06bf1b60b0980a"),
            (Name: "U2.tsv", Count: 2, Sha256: "84ba6476727924aecc6b94207f6de1fdc1135804edbea4d32b7984ede77a6b35")
        };
        foreach (var contract in authorityContracts)
        {
            var bytes = File.ReadAllBytes(Path.Combine(authorityDirectory, contract.Name));
            Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                .ShouldBe(contract.Count);
            B6Values.Sha256Hex(bytes).ShouldBe(contract.Sha256);
        }
        var g = File.ReadAllLines(Path.Combine(authorityDirectory, "G197.tsv"));
        var tq = File.ReadAllLines(Path.Combine(authorityDirectory, "TQ186.tsv"));
        var u = File.ReadAllLines(Path.Combine(authorityDirectory, "U215.tsv"));
        g.Concat(tq).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(u);
        File.ReadAllLines(Path.Combine(authorityDirectory, "U1.tsv"))
            .Select(line => string.Join('\t', line.Split('\t').Take(2))).ToArray()
            .ShouldBe(["configured.source.package\t1.0.0"]);
        File.ReadAllLines(Path.Combine(authorityDirectory, "U2.tsv"))
            .Select(line => string.Join('\t', line.Split('\t').Take(2))).ToArray()
            .ShouldBe(["synthetic.b\t2.0.0", "synthetic.package\t1.0.0"]);
        AssertApprovedFixtureDeterminism();

        var bootstrap = File.ReadAllText(CanonicalB6EntrypointFromTestSource());
        foreach (var contract in lockContracts)
            bootstrap.ShouldContain(contract.Sha256);
        bootstrap.ShouldContain("verify_locks");
        bootstrap.ShouldContain("${frozen_dotnet}\" restore");
        bootstrap.ShouldContain("--locked-mode");
        bootstrap.ShouldNotContain("--force-evaluate");
        bootstrap.ShouldNotContain("--ignore-failed-sources");
        bootstrap.ShouldNotContain("ResolverSupport");
        bootstrap.ShouldContain("${frozen_dotnet}\" build");
        bootstrap.ShouldContain("--no-restore");
        bootstrap.ShouldContain("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true");
        bootstrap.ShouldContain("mechanical-network-witness=PASS");
        bootstrap.ShouldContain("--unshare-net");
        bootstrap.ShouldContain("8.0.130");
        bootstrap.ShouldContain("6.8.2.32767");
        bootstrap.ShouldContain("NUGET_CERT_REVOCATION_MODE=offline");
        bootstrap.ShouldContain("FullyQualifiedName~B6PrimitiveContractTests");
        bootstrap.ShouldContain("verify-test-run");
        bootstrap.ShouldContain("--expected-count 61");
        bootstrap.ShouldContain("GATE_Q=PASS classification=SOURCE_QUALIFICATION_MECHANISM");
        bootstrap.ShouldContain("GATE_A=PASS claim=BOUNDED_REPRODUCIBILITY_AND_BEHAVIOR");
        bootstrap.ShouldContain("freshness_limit_seconds='3600'");
        bootstrap.ShouldContain("transaction_id\\tid\\tversion");
        bootstrap.ShouldContain("repository_signer_sha256");
        bootstrap.ShouldContain("signer_policy_sha256");
        bootstrap.ShouldContain("${frozen_dotnet}\" nuget verify --all");
        bootstrap.ShouldContain("NUGET_CERT_REVOCATION_MODE=online");
        bootstrap.ShouldContain("a-l-transaction-receipt.tsv");
        bootstrap.ShouldContain("actual toolchain inventory differs from authority");
        bootstrap.ShouldContain("--list-sdks");
        bootstrap.ShouldContain("runtime inventory mismatch");
        bootstrap.ShouldContain("/usr/bin/mawk");
        bootstrap.ShouldContain("3850bf893d3f4f8ec462c8b59d6d66fdbbe8ddc0715b3d404d9cc574526b781c");
        bootstrap.ShouldContain("qualification-SHA256SUMS");
        bootstrap.ShouldContain("final-verdict.tsv");
        bootstrap.ShouldContain("ElapsedDurationCentiseconds");
        bootstrap.ShouldContain("--json-status-fd");
        bootstrap.ShouldContain("--ro-bind-data");
        bootstrap.ShouldContain("Bubblewrap wait interrupted before process exit");
        bootstrap.ShouldContain("045a8891a4e84d5acec53b3650e2ea4210bc6d3fc4ddc2d6c8de63662afa56aa");
        bootstrap.ShouldContain("747ec92ca0d9f50eccf82079bc8f878a97a3b59a270c6e10b7b1cd3b54ce728a");
        bootstrap.ShouldNotContain("7f3ee778e07ef8cf720531f9e164870369a065f3223515483d68e4030227dc19");
        bootstrap.ShouldNotContain("--ro-bind / /");
        bootstrap.ShouldNotContain("--bind \"${private_root}\" \"${private_root}\"");
        bootstrap.ShouldContain("--ro-bind /etc/ssl /etc/ssl");
        bootstrap.ShouldContain("evidence directory overlaps repository");
        bootstrap.ShouldContain("B6_PHASE_ERROR");
        bootstrap.ShouldContain("B6_FIRST_GATE_FAILURE");
        bootstrap.ShouldNotContain(" self-test");
        bootstrap.ShouldNotContain("synthetic-contract-test");
        bootstrap.ShouldNotContain("--online-trust-ledger");
        bootstrap.ShouldNotContain("--transaction-id");
        File.Exists(Path.Combine(repository, "docs/operations/aqgreen-v2-b6/snapshot-package",
            "authoritative-runner-synthetic-contract-test.sh")).ShouldBeFalse();
        bootstrap.ToLowerInvariant().ShouldNotContain("python");

        var policy = File.ReadAllText(Path.Combine(repository,
            "docs/operations/aqgreen-v2-b6/snapshot-package/qualification-nuget.config"));
        policy.ShouldContain("signatureValidationMode\" value=\"require");
        policy.ShouldContain("0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D");
        policy.ShouldContain("1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D");
        policy.ShouldContain("5A2901D6ADA3D18260B9C6DFE2133C95D74B9EEF6AE0E5DC334C8454D1477DF4");
    }

    [Fact]
    public void ToolchainIdentity_RejectsEveryRequiredAssemblyMismatchAndMissingDependency()
    {
        var runner = Assembly("AqualLifeStyle.B6SnapshotRunner", 'a');
        var core = Assembly("AqualLifeStyle.Core", 'b');
        var ef = Assembly("AqualLifeStyle.EntityFrameworkCore", 'c');
        var identity = new ToolchainIdentity(new string('d', 40), B6Contract.PrecommitEvidence,
            runner, [core, ef]);

        Should.Throw<B6PackageException>(() => identity.RequireEqual(identity with
            { Runner = runner with { AssemblySha256 = new string('e', 64) } }));
        Should.Throw<B6PackageException>(() => identity.RequireEqual(identity with
            { FirstPartyDependencies = [core with { AssemblySha256 = new string('e', 64) }, ef] }));
        Should.Throw<B6PackageException>(() => identity.RequireEqual(identity with
            { FirstPartyDependencies = [core, ef with { AssemblySha256 = new string('e', 64) }] }));
        Should.Throw<B6PackageException>(() => (identity with
            { FirstPartyDependencies = [core] }).RequireComplete());
    }

    [Fact]
    public void ToolchainIdentity_RequiresOrdinalCanonicalAssemblyOrdering()
    {
        var identity = new ToolchainIdentity(new string('d', 40), B6Contract.PrecommitEvidence,
            Assembly("AqualLifeStyle.B6SnapshotRunner", 'a'),
            [Assembly("AqualLifeStyle.EntityFrameworkCore", 'c'), Assembly("AqualLifeStyle.Core", 'b')]);

        Should.Throw<B6PackageException>(() => identity.RequireComplete())
            .Message.ShouldContain("canonical");
    }

    [Fact]
    public void ToolchainIdentity_RejectsAssemblyThatDiffersFromFreshBuildOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "b6-toolchain-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = WriteAssembly(root, "AqualLifeStyle.B6SnapshotRunner", "runner"u8.ToArray());
            var core = WriteAssembly(root, "AqualLifeStyle.Core", "core"u8.ToArray());
            var ef = WriteAssembly(root, "AqualLifeStyle.EntityFrameworkCore", "ef"u8.ToArray());
            var identity = new ToolchainIdentity(new string('d', 40), B6Contract.OperationalEvidence,
                runner, [core, ef]);

            identity.RequireMatchesBuiltArtifacts(root);
            File.WriteAllBytes(Path.Combine(root, "AqualLifeStyle.Core.dll"), "different"u8.ToArray());
            Should.Throw<B6PackageException>(() => identity.RequireMatchesBuiltArtifacts(root))
                .Message.ShouldContain("fresh reviewed-source build");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void C1A_B_C_D_OperationalBootstrapUsesReviewedDoubleBuildBeforeReleasingSensitiveCapability()
    {
        ExerciseGateASimulationVerifierPositive();
        ExerciseCurrentRunnerSuccess();
    }

    [Fact]
    public void OperationalBootstrap_SeedsPrivateNuGetPackagesWithoutMutatingOrTrustingUnsafeInput()
    {
        ExerciseGateASimulationVerifierNegativeMatrix();
        ExerciseCurrentRunnerNegativeMatrix();
    }

    [Fact]
    public void OperationalWrappers_DelegateBuildAndRunnerSelectionToCanonicalEntrypoint()
    {
        var testSourceMaterializationDirectory = TestSourceMaterializationDirectory();
        var package = Path.Combine(testSourceMaterializationDirectory, "docs", "operations",
            "aqgreen-v2-b6", "snapshot-package");
        foreach (var wrapper in new[] { "run-analysis.sh", "operator-extract.sh" })
        {
            var content = File.ReadAllText(Path.Combine(package, wrapper));
            content.ShouldContain("B6_PREAUTHORITY_PROVENANCE");
            content.ShouldContain("B6_VERIFIED_ARTIFACT_ROOT");
            content.ShouldNotContain("dotnet build");
            content.ShouldNotContain("ContinuousIntegrationBuild");
            content.ShouldNotContain("bin/Release");
        }

        var runbook = File.ReadAllText(Path.Combine(package, "operator-runbook.md"));
        runbook.ShouldContain("git -C");
        runbook.ShouldContain("show");
        runbook.ShouldContain("GIT_NO_REPLACE_OBJECTS=1");
        runbook.ShouldContain("authoritative-runner.sh");
        runbook.ShouldNotContain("-p:ContinuousIntegrationBuild");
        runbook.ShouldNotContain("dotnet build AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner");
    }

    [Theory]
    [InlineData("replacement")]
    [InlineData("runner-symlink")]
    [InlineData("root-symlink")]
    public void OperationalAdapter_RejectsReplaceableOrSymlinkedVerifiedArtifact(string attack)
    {
        if (OperatingSystem.IsWindows()) return;
        var testRoot = Path.Combine(Path.GetTempPath(), "b6-artifact-selection-test-" + Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(testRoot, "artifacts");
        var linkRoot = Path.Combine(testRoot, "artifact-link");
        Directory.CreateDirectory(artifactRoot);
        try
        {
            var runner = Path.Combine(artifactRoot, "AqualLifeStyle.B6SnapshotRunner.dll");
            var core = Path.Combine(artifactRoot, "AqualLifeStyle.Core.dll");
            var ef = Path.Combine(artifactRoot, "AqualLifeStyle.EntityFrameworkCore.dll");
            File.WriteAllText(runner, "runner");
            File.WriteAllText(core, "core");
            File.WriteAllText(ef, "ef");
            var runnerHash = B6Values.Sha256Hex(File.ReadAllBytes(runner));
            var coreHash = B6Values.Sha256Hex(File.ReadAllBytes(core));
            var efHash = B6Values.Sha256Hex(File.ReadAllBytes(ef));

            var selectedRoot = artifactRoot;
            if (attack == "replacement") File.WriteAllText(core, "replacement");
            if (attack == "runner-symlink")
            {
                File.Delete(runner);
                File.CreateSymbolicLink(runner, ef);
            }
            if (attack == "root-symlink")
            {
                Directory.CreateSymbolicLink(linkRoot, artifactRoot);
                selectedRoot = linkRoot;
            }

            var testSourceMaterializationDirectory = TestSourceMaterializationDirectory();
            var result = RunCaptured(testSourceMaterializationDirectory, "bash", new Dictionary<string, string>
            {
                ["B6_PREAUTHORITY_PROVENANCE"] = B6Contract.PreAuthorityProvenance,
                ["B6_VERIFIED_ARTIFACT_ROOT"] = selectedRoot,
                ["B6_PREAUTHORITY_RUNNER_SHA256"] = runnerHash,
                ["B6_PREAUTHORITY_CORE_SHA256"] = coreHash,
                ["B6_PREAUTHORITY_EF_SHA256"] = efHash,
                ["B6_SOURCE_DSN"] = "Host=synthetic.invalid",
                ["B6_SOURCE_SYSTEM_ID"] = "SYNTHETIC",
                ["B6_REPOSITORY_COMMIT"] = new string('d', 40),
                ["B6_RAW_ZONE2_DIR"] = Path.Combine(testRoot, "raw"),
                ["B6_PSEUDONYM_KEY_FILE"] = Path.Combine(testRoot, "key"),
                ["B6_ZONE3_DIR"] = Path.Combine(testRoot, "zone3"),
                ["B6_SNAPSHOT_ID"] = "B6-SYNTHETIC",
                ["B6_TENANT_ID"] = "1",
                ["B6_ANALYSIS_SNAPSHOT_CUTOFF"] = "2026-01-01T00:00:00Z"
            }, Path.Combine(TestSourcePackageDirectory(), "operator-extract.sh"));

            result.ExitCode.ShouldNotBe(0);
            Directory.Exists(Path.Combine(testRoot, "raw")).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void TestRunResults_AcceptsNonzeroSuccessfulExecutionAndExpectedCount()
    {
        var path = WriteTrx(total: 2, executed: 2, passed: 2, failed: 0, skipped: 0);
        try
        {
            var result = B6TestRunResults.ReadAndValidate(path, expectedCount: 2);
            result.Discovered.ShouldBe(2);
            result.Executed.ShouldBe(2);
            result.Passed.ShouldBe(2);
            result.Failed.ShouldBe(0);
            result.Skipped.ShouldBe(0);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Aborted")]
    [InlineData("Error")]
    [InlineData("Timeout")]
    [InlineData("Unknown")]
    public void TestRunResults_RejectsNonSuccessfulResultSummaryOutcome(string outcome)
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0,
            outcome: outcome);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("outcome");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsMissingResultSummaryOutcome()
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0,
            outcome: null);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("outcome");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsBlankResultSummaryOutcome()
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0,
            outcome: " ");
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("outcome");
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("aborted")]
    [InlineData("timeout")]
    [InlineData("inconclusive")]
    [InlineData("error")]
    [InlineData("passedButRunAborted")]
    [InlineData("notRunnable")]
    [InlineData("disconnected")]
    [InlineData("warning")]
    [InlineData("completed")]
    [InlineData("inProgress")]
    [InlineData("pending")]
    public void TestRunResults_RejectsEveryEmittedNonSuccessCounter(string counter)
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0,
            counterOverrides: new Dictionary<string, int> { [counter] = 1 });
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain(counter);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsSkippedTestsEvenWhenAccountingIsConsistent()
    {
        var path = WriteTrx(total: 2, executed: 1, passed: 1, failed: 0, skipped: 1);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 2))
                .Message.ShouldContain("notExecuted");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsContradictoryAccounting()
    {
        var path = WriteTrx(total: 2, executed: 2, passed: 1, failed: 0, skipped: 0);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 2))
                .Message.ShouldContain("inconsistent");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsUnknownUnqualifiedStatusCounter()
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0,
            extraCounters: " futureFailure=\"1\"");
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("unsupported");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_IgnoresNamespaceQualifiedCounterMetadata()
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0,
            extraCounters: " xmlns:meta=\"urn:aqua:test-metadata\" meta:collectorVersion=\"1\"");
        try
        {
            B6TestRunResults.ReadAndValidate(path, expectedCount: 1).Passed.ShouldBe(1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsForeignRootElement()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<Envelope xmlns=\"urn:not-trx\"><ResultSummary outcome=\"Completed\">" +
            $"{counters}</ResultSummary></Envelope>");
    }

    [Fact]
    public void TestRunResults_RejectsTestRunRootInForeignNamespace()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"urn:not-trx\"><ResultSummary outcome=\"Completed\">" +
            $"{counters}</ResultSummary></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsAuthoritativeTestRunWrappedByForeignRoot()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<ext:Envelope xmlns:ext=\"urn:not-trx\"><TestRun xmlns=\"{TrxNamespace}\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary></TestRun></ext:Envelope>");
    }

    [Fact]
    public void TestRunResults_RejectsDirectNestedSupportedNamespaceTestRun()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary><TestRun /></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsDeeplyNestedSupportedNamespaceTestRun()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:not-trx\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary>" +
            "<ext:Metadata><ext:Nested><TestRun /></ext:Nested></ext:Metadata></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsNestedSuccessShapedSupportedNamespaceTestRun()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary>" +
            $"<TestRun><ResultSummary outcome=\"Completed\">{counters}</ResultSummary></TestRun></TestRun>");
    }

    [Fact]
    public void TestRunResults_AllowsInertForeignTestRunExtension()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        var path = WriteRawTrx($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:not-trx\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary><ext:TestRun /></TestRun>");
        try
        {
            B6TestRunResults.ReadAndValidate(path, expectedCount: 1).Passed.ShouldBe(1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsAlternatePrefixSameNamespaceTestRun()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:trx=\"{TrxNamespace}\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary><trx:TestRun /></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsForeignResultSummary()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:not-trx\">" +
            $"<ext:ResultSummary outcome=\"Completed\">{counters}</ext:ResultSummary></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsResultSummaryNestedUnderExtensionElement()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:not-trx\"><ext:Metadata>" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary></ext:Metadata></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsForeignCounters()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0)
            .Replace("<Counters", "<ext:Counters", StringComparison.Ordinal);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:not-trx\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsCountersNestedUnderExtensionElement()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:not-trx\">" +
            $"<ResultSummary outcome=\"Completed\"><ext:Metadata>{counters}</ext:Metadata>" +
            "</ResultSummary></TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsCountersOutsideAuthoritativeResultSummary()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\"><ResultSummary outcome=\"Completed\" />" +
            $"{counters}</TestRun>");
    }

    [Fact]
    public void TestRunResults_RejectsMissingAuthoritativeResultSummary()
    {
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\" />");
    }

    [Fact]
    public void TestRunResults_RejectsMissingAuthoritativeCounters()
    {
        AssertTrxRejected($"<TestRun xmlns=\"{TrxNamespace}\"><ResultSummary outcome=\"Completed\" />" +
            "</TestRun>");
    }

    [Fact]
    public void TestRunResults_AllowsInertForeignExtensionMetadataAlongsideAuthority()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        var path = WriteRawTrx($"<TestRun xmlns=\"{TrxNamespace}\" xmlns:ext=\"urn:aqua:test-metadata\">" +
            $"<ext:Metadata ext:collectorVersion=\"1\"/><ResultSummary outcome=\"Completed\">{counters}" +
            "<ext:Metadata ext:collectorVersion=\"1\"/></ResultSummary></TestRun>");
        try
        {
            B6TestRunResults.ReadAndValidate(path, expectedCount: 1).Passed.ShouldBe(1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsMalformedTrx()
    {
        var path = WriteRawTrx("<TestRun><ResultSummary");
        try
        {
            Should.Throw<Exception>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsMultipleCountersAsAmbiguous()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        var path = WriteRawTrx($"<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}{counters}</ResultSummary></TestRun>");
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("exactly one");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsMultipleResultSummariesAsAmbiguous()
    {
        var counters = CountersXml(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        var path = WriteRawTrx($"<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">" +
            $"<ResultSummary outcome=\"Completed\">{counters}</ResultSummary>" +
            $"<ResultSummary outcome=\"Completed\"></ResultSummary></TestRun>");
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("exactly one");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsZeroMatchedTests()
    {
        var path = WriteTrx(total: 0, executed: 0, passed: 0, failed: 0, skipped: 0);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: null))
                .Message.ShouldContain("zero tests");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsFailedSelectedTest()
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 0, failed: 1, skipped: 0);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1))
                .Message.ShouldContain("failed");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestRunResults_RejectsUnexpectedFocusedCount()
    {
        var path = WriteTrx(total: 1, executed: 1, passed: 1, failed: 0, skipped: 0);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 2))
                .Message.ShouldContain("expected 2");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TestPackageFilterRunnerFailureProbe_IsControlledByEnvironment()
    {
        Assert.False(Environment.GetEnvironmentVariable("B6_TEST_FORCE_FAILURE") == "1",
            "Intentional B6 filtered-run failure probe.");
    }

    [Fact]
    public void UnknownCommandOption_IsRejectedBeforeExecution()
    {
        var parsed = Arguments.Parse(["finalize-manifest", "--simulation", "caller.json"]);

        Should.Throw<B6PackageException>(() => parsed.EnsureOnly(new HashSet<string>(StringComparer.Ordinal)))
            .Message.ShouldContain("--simulation");
    }

    [Fact]
    public void WeakOrAllZeroOperationalKey_IsRejected()
    {
        Should.Throw<B6PackageException>(() => new Pseudonymizer(new byte[32]));
        Should.Throw<B6PackageException>(() => new Pseudonymizer(Enumerable.Repeat((byte)7, 32).ToArray()));
    }

    [Fact]
    public void PseudonymCollision_IsFatalWithoutCounterFallback()
    {
        var secret = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var pseudonymizer = new Pseudonymizer(secret, _ => Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());

        Should.Throw<B6PackageException>(() => pseudonymizer.Prepare("uuid", "Participation", ["one", "two"]))
            .Message.ShouldContain("Fatal pseudonym collision");
    }

    [Fact]
    public void CanonicalEncoding_DistinguishesNullEmptyAndUtf8Lengths()
    {
        B6Digests.EncodeField(B6Contract.Null).ShouldBe([(byte)'N']);
        Encoding.ASCII.GetString(B6Digests.EncodeField(string.Empty)).ShouldBe("V0:");
        Encoding.ASCII.GetString(B6Digests.EncodeField("é")[..3]).ShouldBe("V2:");
    }

    [Fact]
    public void MixedEndianGuidOrdinal_UsesNativeDotNetComparison()
    {
        var earlier = Guid.Parse("00000000-0001-0100-8000-000000000001");
        var later = Guid.Parse("00000000-0100-0001-8000-000000000001");

        earlier.CompareTo(later).ShouldBeLessThan(0);
        earlier.ToByteArray().AsSpan(0, 8).SequenceCompareTo(later.ToByteArray().AsSpan(0, 8)).ShouldBeGreaterThan(0);
    }

    private static FirstPartyAssemblyIdentity Assembly(string name, char digest) =>
        new(name, new string(digest, 64), "1.0.0");

    private static FirstPartyAssemblyIdentity WriteAssembly(string root, string name, byte[] content)
    {
        File.WriteAllBytes(Path.Combine(root, name + ".dll"), content);
        return new FirstPartyAssemblyIdentity(name, B6Values.Sha256Hex(content), "test");
    }

    private static string MinimalProject(string assemblyName) =>
        $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework>" +
        $"<AssemblyName>{assemblyName}</AssemblyName></PropertyGroup></Project>\n";

    private static void WriteSyntheticNuGetPackage(string root, string packageId, string version)
    {
        var packageDirectory = Path.Combine(root, packageId, version);
        var payloadDirectory = Path.Combine(packageDirectory, "lib", "net8.0");
        Directory.CreateDirectory(payloadDirectory);
        var nuspec = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><package><metadata>" +
                     $"<id>{packageId}</id><version>{version}</version><authors>B6 Test</authors>" +
                     "<description>Disposable offline restore package.</description></metadata></package>";
        var archivePath = Path.Combine(packageDirectory, $"{packageId}.{version}.nupkg");
        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, $"{packageId}.nuspec", nuspec);
            WriteZipEntry(archive, "lib/net8.0/_._", string.Empty);
        }

        var archiveBytes = File.ReadAllBytes(archivePath);
        var contentHash = Convert.ToBase64String(SHA512.HashData(archiveBytes));
        File.WriteAllText(Path.Combine(packageDirectory, $"{packageId}.nuspec"), nuspec);
        File.WriteAllText(archivePath + ".sha512", contentHash);
        File.WriteAllText(Path.Combine(packageDirectory, ".nupkg.metadata"),
            $"{{\"version\":2,\"contentHash\":\"{contentHash}\",\"source\":\"https://api.nuget.org/v3/index.json\"}}");
        File.WriteAllBytes(Path.Combine(payloadDirectory, "_._"), []);
    }

    private static void WriteZipEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        entry.LastWriteTime = SyntheticPackageEntryTimestamp;
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AssertSyntheticNuGetPackageIdentity(
        string root,
        string packageId,
        string version,
        string expectedArchiveSha256)
    {
        var archivePath = Path.Combine(root, packageId, version, $"{packageId}.{version}.nupkg");
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)))
            .ToLowerInvariant().ShouldBe(expectedArchiveSha256);
        using var archive = ZipFile.OpenRead(archivePath);
        archive.Entries.Select(entry => entry.FullName)
            .ShouldBe(new[] { $"{packageId}.nuspec", "lib/net8.0/_._" });
        archive.Entries.ShouldAllBe(entry =>
            entry.LastWriteTime.DateTime == SyntheticPackageEntryTimestamp.DateTime);
    }

    private static void AssertApprovedFixtureDeterminism()
    {
        var roots = Enumerable.Range(0, 3)
            .Select(_ => Path.Combine(Path.GetTempPath(), "b6-approved-fixture-" + Guid.NewGuid().ToString("N")))
            .ToArray();
        try
        {
            foreach (var root in roots)
            {
                WriteSyntheticNuGetPackage(root, "configured.source.package", "1.0.0");
                WriteSyntheticNuGetPackage(root, "synthetic.b", "2.0.0");
                WriteSyntheticNuGetPackage(root, "synthetic.package", "1.0.0");
                AssertSyntheticNuGetPackageIdentity(root, "configured.source.package", "1.0.0",
                    "213c7d72ea629e5c7d76bb4d1bad9b7496453b19dab1762d9be38e861a96a499");
                AssertSyntheticNuGetPackageIdentity(root, "synthetic.b", "2.0.0",
                    "5d4cf66d39eada41c20f070c552fc1652da5668156084d8c543335e1ea0c26c7");
                AssertSyntheticNuGetPackageIdentity(root, "synthetic.package", "1.0.0",
                    "c0415bd663818055158cfd326a3802f9416571320a2a7ad442663858cc01655f");
            }
        }
        finally
        {
            foreach (var root in roots)
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }

    private static (string Id, string Version)[] PackageVersions(string root) =>
        Directory.EnumerateDirectories(root)
            .SelectMany(packageDirectory => Directory.EnumerateDirectories(packageDirectory)
                .Select(versionDirectory => (Path.GetFileName(packageDirectory), Path.GetFileName(versionDirectory))))
            .Order().ToArray();

    private sealed record CurrentRunnerFixture(
        string Root,
        string Repository,
        string ReviewedCommit,
        string EvidenceDirectory,
        string BubblewrapLog,
        string[] Arguments);

    private static void ExerciseGateASimulationVerifierPositive()
    {
        var fixture = CreateGateASimulationVerifierFixture();
        try
        {
            var result = B6SimulationOutputVerifier.Verify(fixture.Output, fixture.ExpectedProvenance);
            result.Result.ShouldBe("PASS");
            result.ProjectionSchemaVersion.ShouldBe(B6Contract.GateASemanticProjectionVersion);
            result.SemanticProjectionSha256.ShouldBe(B6Contract.ExpectedGateASemanticProjectionSha256);
            result.SimulatorOutputDigest.ShouldBe(fixture.Root["simulatorOutputDigest"]!.GetValue<string>());
            result.CurrentFullOutputSha256.ShouldBe(B6Values.Sha256Hex(File.ReadAllBytes(fixture.Output)));
        }
        finally { Directory.Delete(fixture.Directory, recursive: true); }
    }

    private static void ExerciseGateASimulationVerifierNegativeMatrix()
    {
        var fixture = CreateGateASimulationVerifierFixture();
        try
        {
            var mutations = new (string Name, Action<JsonObject> Mutate)[]
            {
                ("semantic-field", root => root["snapshotId"] = "b6-gate-a-fixture-v2"),
                ("placement", root => Placement(root)["canonicalPath"] = "5"),
                ("impact", root => Impact(root)["classification"] = "Same"),
                ("manifest-digest", root => Component(root)["manifestDigest"] = new string('0', 64)),
                ("invalid-digest", root => Placement(root)["sourceEvidenceDigest"] = "invalid"),
                ("unknown-top-level", root => root["unknown"] = true),
                ("unknown-nested", root => Placement(root)["unknown"] = true),
                ("missing-required", root => root.Remove("placementSource")),
                ("wrong-scalar-type", root => root["analysisSnapshotCutoff"] = 1),
                ("wrong-nullability", root => root["snapshotId"] = null),
                ("wrong-component-count", root => root["components"]!.AsArray().Add(Component(root).DeepClone())),
                ("wrong-placement-count", root => Component(root)["placements"]!.AsArray().RemoveAt(6)),
                ("wrong-impact-count", root => Component(root)["impacts"]!.AsArray().RemoveAt(6)),
                ("reordered-arrays", root =>
                {
                    Component(root)["placements"] = Reversed(Component(root)["placements"]!.AsArray());
                    root["placements"] = Reversed(root["placements"]!.AsArray());
                }),
                ("duplicate-array-disagreement", root => root["placements"]!.AsArray()[0]!["canonicalPath"] = "1"),
                ("repository-commit", root => Toolchain(root)["repositoryCommit"] = new string('e', 40)),
                ("evidence-classification", root => Toolchain(root)["evidenceClassification"] = B6Contract.PrecommitEvidence),
                ("runner-sha", root => Toolchain(root)["runner"]!["assemblySha256"] = new string('1', 64)),
                ("core-sha", root => Dependency(root, 0)["assemblySha256"] = new string('2', 64)),
                ("ef-sha", root => Dependency(root, 1)["assemblySha256"] = new string('3', 64)),
                ("runner-informational-version", root => Toolchain(root)["runner"]!["informationalVersion"] = "wrong+" + fixture.ExpectedProvenance.RepositoryCommit),
                ("core-informational-version", root => Dependency(root, 0)["informationalVersion"] = "wrong+" + fixture.ExpectedProvenance.RepositoryCommit),
                ("ef-informational-version", root => Dependency(root, 1)["informationalVersion"] = "wrong+" + fixture.ExpectedProvenance.RepositoryCommit),
                ("dependency-membership", root => Dependency(root, 0)["assemblyName"] = "Unexpected.Core"),
                ("dependency-order", root => Toolchain(root)["firstPartyDependencies"] = Reversed(Toolchain(root)["firstPartyDependencies"]!.AsArray())),
                ("simulator-output-digest", root => root["simulatorOutputDigest"] = new string('0', 64)),
                ("semantic-projection-hash", root => root["placementSource"] = "LegacyCanonicalisationChanged")
            };

            foreach (var mutation in mutations)
            {
                var value = fixture.Root.DeepClone().AsObject();
                mutation.Mutate(value);
                var path = Path.Combine(fixture.Directory, mutation.Name + ".json");
                CanonicalJson.WriteNew(path, value);
                Should.Throw<B6PackageException>(() =>
                        B6SimulationOutputVerifier.Verify(path, fixture.ExpectedProvenance), mutation.Name)
                    .Message.ShouldNotBeNullOrWhiteSpace();
            }

            var wrongExpected = fixture.ExpectedProvenance with
            {
                RepositoryCommit = new string('f', 40),
                Runner = fixture.ExpectedProvenance.Runner with
                    { InformationalVersion = "runner+" + new string('f', 40) },
                FirstPartyDependencies = fixture.ExpectedProvenance.FirstPartyDependencies
                    .Select(value => value with { InformationalVersion = value.AssemblyName + "+" + new string('f', 40) })
                    .ToList()
            };
            Should.Throw<B6PackageException>(() =>
                B6SimulationOutputVerifier.Verify(fixture.Output, wrongExpected));
        }
        finally { Directory.Delete(fixture.Directory, recursive: true); }
    }

    private static GateASimulationFixture CreateGateASimulationVerifierFixture()
    {
        var directory = Path.Combine(Path.GetTempPath(), "b6-gate-a-verifier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var input = Path.Combine(directory, "input.json");
        File.Copy(Path.Combine(TestSourcePackageDirectory(), "fixture", "b2-simulate-input.json"), input);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(input, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var output = Path.Combine(directory, "output.json");
        var previousClassification = Environment.GetEnvironmentVariable(B6Contract.EvidenceClassificationEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(B6Contract.EvidenceClassificationEnvironment, B6Contract.PrecommitEvidence);
            new B6SnapshotPackage().RunSimulator(input, output);
        }
        finally
        {
            Environment.SetEnvironmentVariable(B6Contract.EvidenceClassificationEnvironment, previousClassification);
        }

        var commit = new string('d', 40);
        var expected = new ToolchainIdentity(
            commit,
            B6Contract.OperationalEvidence,
            new FirstPartyAssemblyIdentity("AqualLifeStyle.B6SnapshotRunner", new string('a', 64), "runner+" + commit),
            [
                new FirstPartyAssemblyIdentity("AqualLifeStyle.Core", new string('b', 64), "core+" + commit),
                new FirstPartyAssemblyIdentity("AqualLifeStyle.EntityFrameworkCore", new string('c', 64), "ef+" + commit)
            ]);
        var root = CanonicalJson.Read(output).AsObject();
        root["toolchainIdentity"] = CanonicalJson.ToNode(expected);
        root["simulatorOutputDigest"] = CanonicalJson.Digest(new JsonObject
        {
            ["components"] = root["components"]!.DeepClone(),
            ["toolchainIdentity"] = CanonicalJson.ToNode(expected),
            ["simulationVersion"] = root["simulationVersion"]!.DeepClone(),
            ["comparatorVersion"] = root["comparatorVersion"]!.DeepClone()
        });
        File.Delete(output);
        CanonicalJson.WriteNew(output, root);
        return new GateASimulationFixture(directory, output, root, expected);
    }

    private static JsonObject Component(JsonObject root) => root["components"]!.AsArray()[0]!.AsObject();
    private static JsonObject Placement(JsonObject root) => Component(root)["placements"]!.AsArray()[0]!.AsObject();
    private static JsonObject Impact(JsonObject root) => Component(root)["impacts"]!.AsArray()[0]!.AsObject();
    private static JsonObject Toolchain(JsonObject root) => root["toolchainIdentity"]!.AsObject();
    private static JsonObject Dependency(JsonObject root, int index) =>
        Toolchain(root)["firstPartyDependencies"]!.AsArray()[index]!.AsObject();

    private static JsonArray Reversed(JsonArray value) =>
        new(value.Reverse().Select(item => item?.DeepClone()).ToArray());

    private sealed record GateASimulationFixture(
        string Directory,
        string Output,
        JsonObject Root,
        ToolchainIdentity ExpectedProvenance);

    private static void ExerciseCurrentRunnerSuccess()
    {
        if (OperatingSystem.IsWindows()) return;
        var fixture = CreateCurrentRunnerFixture(3599, "success");
        try
        {
            var result = RunBootstrapCaptured(fixture.Repository, fixture.ReviewedCommit, fixture.Arguments);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Current B6 runner failed: {result.StandardError}\n" +
                    $"Bubblewrap spy: {(File.Exists(fixture.BubblewrapLog) ? File.ReadAllText(fixture.BubblewrapLog) : "missing")}\n" +
                    $"Gate log: {(File.Exists(Path.Combine(fixture.EvidenceDirectory, "gate-q-restore.log")) ? File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "gate-q-restore.log")) : "missing")}");

            result.StandardError.ShouldContain("GATE_Q=PASS");
            result.StandardError.ShouldContain("GATE_A=PASS");
            result.StandardError.ShouldContain("B6_QUALIFICATION=PASS");
            result.StandardError.ShouldNotContain("B6_QUALIFICATION=FAIL");
            File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "trust-freshness.tsv"))
                .ShouldContain("ElapsedDuration\t3599.00 seconds");
            File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv"))
                .ShouldContain("final_transaction_result\tPASS");
            File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv"))
                .ShouldContain("process_exit_code\t0");
            File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv"))
                .ShouldContain("cleanup_reconciliation_result\tPASS");
            Run(fixture.EvidenceDirectory, "sha256sum", "-c", "qualification-SHA256SUMS");
            Run(fixture.EvidenceDirectory, "sha256sum", "-c", "SHA256SUMS");

            var bwrap = File.ReadAllText(fixture.BubblewrapLog);
            bwrap.ShouldContain("--unshare-net");
            bwrap.ShouldContain("--json-status-fd");
            bwrap.ShouldContain("--ro-bind-data");
            bwrap.ShouldContain("/run/aqua/network-witness 8 7 env");
            bwrap.ShouldNotContain("/run/aqua/network-witness 8 7 1000");
            var privateRootMatch = System.Text.RegularExpressions.Regex.Match(bwrap,
                @"--ro-bind (?<root>/tmp/aqua-b6-qualified\.[^ /]+)/source-q ");
            privateRootMatch.Success.ShouldBeTrue();
            var privateRoot = privateRootMatch.Groups["root"].Value;
            bwrap.ShouldNotContain($"--bind {privateRoot} {privateRoot}");
            bwrap.ShouldNotContain(fixture.EvidenceDirectory);
            bwrap.ShouldNotContain($"{privateRoot}/network-witness.sh");
            bwrap.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .ShouldAllBe(invocation => invocation.Contains("--ro-bind-data", StringComparison.Ordinal));
            var witnesses = Directory.EnumerateFiles(fixture.EvidenceDirectory, "network-*.tsv").ToArray();
            witnesses.Length.ShouldBe(12);
            foreach (var witness in witnesses)
            {
                var content = File.ReadAllText(witness);
                content.ShouldContain("uid_map\t65534 0 1\n");
                content.ShouldContain("gid_map\t65534 0 1\n");
            }
        }
        finally { RemoveCurrentRunnerFixture(fixture.Root); }
    }

    private static void ExerciseCurrentRunnerNegativeMatrix()
    {
        if (OperatingSystem.IsWindows()) return;

        var exactBoundary = CreateCurrentRunnerFixture(3600, "success");
        try
        {
            var result = RunBootstrapCaptured(exactBoundary.Repository, exactBoundary.ReviewedCommit,
                exactBoundary.Arguments);
            result.ExitCode.ShouldBe(0, result.StandardError);
            File.ReadAllText(Path.Combine(exactBoundary.EvidenceDirectory, "trust-freshness.tsv"))
                .ShouldContain("ElapsedDuration\t3600.00 seconds");
        }
        finally { RemoveCurrentRunnerFixture(exactBoundary.Root); }

        foreach (var scenario in new[]
                 {
                     "freshness-over", "toolchain-missing", "toolchain-unexpected", "runtime-extra",
                     "package-substitution", "native-verify-failure", "native-verify-missing-success",
                     "mawk-mismatch", "mawk-version-mismatch", "bootstrap-nonroot", "receipt-transaction", "receipt-missing-time",
                     "receipt-malformed-time", "receipt-restart", "receipt-log-missing", "receipt-log-mismatch",
                     "receipt-log-hash-mismatch", "clock-reversal", "clock-discontinuity", "monotonic-reversal",
                     "promoted-tree-tamper", "witness-uid-dynamic", "witness-gid-dynamic",
                     "witness-uid-map-extra", "witness-gid-map-extra", "witness-uid-map-missing",
                     "witness-gid-map-missing", "witness-wrong-inside-uid", "witness-wrong-inside-gid",
                     "witness-wrong-uid-range", "witness-wrong-gid-range", "witness-map-whitespace",
                     "witness-pid", "witness-starttime", "witness-groups",
                     "witness-capabilities", "witness-no-new-privileges", "qualification-evidence-tamper",
                     "simulation-output-mismatch"
                 })
        {
            var fixture = CreateCurrentRunnerFixture(scenario == "freshness-over" ? 3601 : 3599, scenario);
            try
            {
                var result = RunBootstrapCaptured(fixture.Repository, fixture.ReviewedCommit, fixture.Arguments);
                result.ExitCode.ShouldNotBe(0, $"Scenario {scenario} unexpectedly passed.");
                result.StandardError.ShouldContain("B6_QUALIFICATION=FAIL");
                result.StandardError.ShouldNotContain("B6_QUALIFICATION=PASS");
                File.Exists(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv")).ShouldBeTrue();
                Run(fixture.EvidenceDirectory, "sha256sum", "-c", "SHA256SUMS");
                var terminalReceipt = File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv"));
                terminalReceipt.ShouldContain("final_transaction_result\tFAIL");
                terminalReceipt.ShouldNotContain("process_exit_code\t0\n");
                if (scenario == "toolchain-missing")
                    File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "toolchain-inventory-result.tsv"))
                        .ShouldContain("missing\t1");
                if (scenario == "toolchain-unexpected")
                    File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "toolchain-inventory-result.tsv"))
                        .ShouldContain("unexpected\t1");
                if (scenario == "runtime-extra")
                    File.Exists(Path.Combine(fixture.EvidenceDirectory, "toolchain-discovery.tsv")).ShouldBeTrue();
                if (scenario is "freshness-over" or "receipt-transaction" or "receipt-missing-time" or
                    "receipt-malformed-time" or "receipt-restart" or "receipt-log-mismatch" or
                    "receipt-log-missing" or "receipt-log-hash-mismatch" or "clock-reversal" or
                    "clock-discontinuity" or "monotonic-reversal" or "promoted-tree-tamper")
                {
                    var freshness = File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "trust-freshness.tsv"));
                    freshness.ShouldContain("Result\tFAIL");
                }
            }
            finally { RemoveCurrentRunnerFixture(fixture.Root); }
        }

        var fabricated = CreateCurrentRunnerFixture(3599, "success");
        try
        {
            var arguments = fabricated.Arguments.ToList();
            var operationIndex = arguments.IndexOf("runner");
            arguments.Insert(operationIndex, "/tmp/fabricated-ledger.tsv");
            arguments.Insert(operationIndex, "--online-trust-ledger");
            var result = RunBootstrapCaptured(fabricated.Repository, fabricated.ReviewedCommit,
                arguments.ToArray());
            result.ExitCode.ShouldNotBe(0);
            result.StandardError.ShouldContain("unsupported operation --online-trust-ledger");
        }
        finally { RemoveCurrentRunnerFixture(fabricated.Root); }

        var operationFailure = CreateCurrentRunnerFixture(3599, "operation-failure");
        try
        {
            var result = RunBootstrapCaptured(operationFailure.Repository, operationFailure.ReviewedCommit,
                operationFailure.Arguments);
            result.ExitCode.ShouldBe(9);
            result.StandardError.ShouldContain("B6_QUALIFICATION=FAIL");
            result.StandardError.ShouldNotContain("B6_QUALIFICATION=PASS");
            var verdict = Path.Combine(operationFailure.EvidenceDirectory, "final-verdict.tsv");
            File.ReadAllText(verdict).ShouldContain("operation_result\tFAIL");
            File.ReadAllText(verdict).ShouldContain("process_exit_code\t9");
            File.ReadAllText(verdict).ShouldContain("final_transaction_result\tFAIL");
            Run(operationFailure.EvidenceDirectory, "sha256sum", "-c", "SHA256SUMS");
            File.AppendAllText(verdict, "tampered\n");
            RunCaptured(operationFailure.EvidenceDirectory, "sha256sum", new Dictionary<string, string>(),
                "-c", "SHA256SUMS").ExitCode.ShouldNotBe(0);
        }
        finally { RemoveCurrentRunnerFixture(operationFailure.Root); }

        foreach (var scenario in new[]
                 {
                     "transaction-cleanup-failure", "stale-terminal-receipt",
                     "signal-int-before-finalization", "signal-term-before-finalization",
                     "signal-int-cleanup", "signal-term-cleanup",
                     "signal-int-boundary", "signal-term-boundary"
                 })
        {
            var fixture = CreateCurrentRunnerFixture(3599, scenario);
            try
            {
                var result = RunBootstrapCaptured(fixture.Repository, fixture.ReviewedCommit, fixture.Arguments);
                var expectedExit = scenario switch
                {
                    "signal-int-before-finalization" => 130,
                    "signal-term-before-finalization" => 143,
                    "signal-int-cleanup" => 130,
                    "signal-term-cleanup" => 143,
                    "signal-int-boundary" => 130,
                    "signal-term-boundary" => 143,
                    _ => 3
                };
                result.ExitCode.ShouldBe(expectedExit, $"Scenario {scenario}: {result.StandardError}");
                result.StandardError.ShouldContain("B6_QUALIFICATION=FAIL");
                result.StandardError.ShouldNotContain("B6_QUALIFICATION=PASS");
                var receipt = File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv"));
                receipt.ShouldContain("final_transaction_result\tFAIL");
                receipt.ShouldContain($"process_exit_code\t{expectedExit}\n");
                if (scenario == "transaction-cleanup-failure")
                    receipt.ShouldContain("cleanup_reconciliation_result\tFAIL");
                Run(fixture.EvidenceDirectory, "sha256sum", "-c", "SHA256SUMS");
            }
            finally { RemoveCurrentRunnerFixture(fixture.Root); }
        }

        foreach (var scenario in new[]
                 {
                     "terminal-manifest-creation-failure", "terminal-manifest-verification-failure",
                     "terminal-receipt-staging-failure",
                     "terminal-receipt-publish-failure", "atomic-copy-failure",
                     "atomic-permission-failure", "atomic-rename-failure",
                     "stale-pass-and-terminal-manifest-failure"
                 })
        {
            var fixture = CreateCurrentRunnerFixture(3599, scenario);
            try
            {
                var result = RunBootstrapCaptured(fixture.Repository, fixture.ReviewedCommit, fixture.Arguments);
                result.ExitCode.ShouldBe(3, $"Scenario {scenario}: {result.StandardError}");
                AssertNoValidPassReceipt(fixture, result);
            }
            finally { RemoveCurrentRunnerFixture(fixture.Root); }
        }

        foreach (var scenario in new[]
                 {
                     "signal-int-finalization", "signal-term-finalization",
                     "signal-int-after-finalization", "signal-term-after-finalization",
                     "housekeeping-failure"
                 })
        {
            var fixture = CreateCurrentRunnerFixture(3599, scenario);
            try
            {
                var result = RunBootstrapCaptured(fixture.Repository, fixture.ReviewedCommit, fixture.Arguments);
                result.ExitCode.ShouldBe(0, $"Scenario {scenario}: {result.StandardError}");
                result.StandardError.ShouldContain("B6_QUALIFICATION=PASS");
                result.StandardError.ShouldNotContain("B6_QUALIFICATION=FAIL");
                var receipt = File.ReadAllText(Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv"));
                receipt.ShouldContain("final_transaction_result\tPASS");
                receipt.ShouldContain("process_exit_code\t0\n");
                Run(fixture.EvidenceDirectory, "sha256sum", "-c", "SHA256SUMS");
            }
            finally { RemoveCurrentRunnerFixture(fixture.Root); }
        }
    }

    private static void AssertNoValidPassReceipt(CurrentRunnerFixture fixture, CommandResult result)
    {
        result.StandardError.ShouldContain("B6_QUALIFICATION=FAIL");
        result.StandardError.ShouldNotContain("B6_QUALIFICATION=PASS");
        var receipt = Path.Combine(fixture.EvidenceDirectory, "final-verdict.tsv");
        var manifest = Path.Combine(fixture.EvidenceDirectory, "SHA256SUMS");
        if (File.Exists(receipt))
            File.ReadAllText(receipt).ShouldNotContain("final_transaction_result\tPASS");
        if (File.Exists(manifest))
            RunCaptured(fixture.EvidenceDirectory, "sha256sum", new Dictionary<string, string>(),
                "-c", "SHA256SUMS").ExitCode.ShouldNotBe(0);
    }

    private static CurrentRunnerFixture CreateCurrentRunnerFixture(int elapsedSeconds, string scenario)
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var root = Path.Combine(Path.GetTempPath(), "b6-current-runner-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repository");
        var packageDirectory = Path.Combine(repository, "docs", "operations", "aqgreen-v2-b6",
            "snapshot-package");
        var authorityDirectory = Path.Combine(packageDirectory, "authority");
        var acquired = Path.Combine(root, "acquired");
        var feed = Path.Combine(root, "feed");
        var frozen = Path.Combine(root, "frozen-dotnet");
        var evidence = Path.Combine(root, "evidence");
        var bwrapLog = Path.Combine(root, "bwrap.log");
        var monotonicValues = Path.Combine(root, "monotonic-values");
        var monotonicProgram = Path.Combine(root, "monotonic.sh");
        var fakeBwrap = Path.Combine(root, "bwrap");
        var mutationMarker = Path.Combine(root, "mutation-applied");
        Directory.CreateDirectory(repository);
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(authorityDirectory);
        Directory.CreateDirectory(acquired);
        Directory.CreateDirectory(feed);
        Directory.CreateDirectory(frozen);

        var uPackages = new[]
        {
            (Id: "synthetic.b", Version: "2.0.0"),
            (Id: "synthetic.c", Version: "3.0.0"),
            (Id: "synthetic.package", Version: "1.0.0")
        };
        var gPackages = uPackages.Take(2).ToArray();
        var tqPackages = uPackages.Skip(1).Take(1).ToArray();
        foreach (var package in uPackages)
        {
            WriteSyntheticNuGetPackage(acquired, package.Id, package.Version);
            var archive = Path.Combine(acquired, package.Id, package.Version,
                $"{package.Id}.{package.Version}.nupkg");
            File.Copy(archive, Path.Combine(feed, Path.GetFileName(archive)));
        }

        var gManifest = string.Concat(gPackages.Order()
            .Select(package => $"{package.Id}\t{package.Version}\n"));
        var tqManifest = string.Concat(tqPackages.Order()
            .Select(package => $"{package.Id}\t{package.Version}\n"));
        var uManifest = string.Concat(uPackages.Order()
            .Select(package => $"{package.Id}\t{package.Version}\n"));
        var u1Manifest = $"{uPackages[0].Id}\t{uPackages[0].Version}\n";
        var u2Manifest = string.Concat(uPackages.Take(2).Order()
            .Select(package => $"{package.Id}\t{package.Version}\n"));
        File.WriteAllText(Path.Combine(authorityDirectory, "G197.tsv"), gManifest);
        File.WriteAllText(Path.Combine(authorityDirectory, "TQ186.tsv"), tqManifest);
        File.WriteAllText(Path.Combine(authorityDirectory, "U215.tsv"), uManifest);
        File.WriteAllText(Path.Combine(authorityDirectory, "U1.tsv"), u1Manifest);
        File.WriteAllText(Path.Combine(authorityDirectory, "U2.tsv"), u2Manifest);

        var feedManifest = new StringBuilder(
            "id\tversion\tarchive\traw_sha256\traw_sha512_base64\tcontent_hash\tarchive_nuspec_sha256\tinstalled_nuspec_sha256\tsource\n");
        foreach (var package in uPackages.Order())
        {
            var directory = Path.Combine(acquired, package.Id, package.Version);
            var archive = Path.Combine(directory, $"{package.Id}.{package.Version}.nupkg");
            var archiveBytes = File.ReadAllBytes(archive);
            var nuspecHash = B6Values.Sha256Hex(File.ReadAllBytes(Path.Combine(directory, $"{package.Id}.nuspec")));
            feedManifest.Append(package.Id).Append('\t').Append(package.Version).Append('\t')
                .Append(Path.GetFileName(archive)).Append('\t').Append(B6Values.Sha256Hex(archiveBytes)).Append('\t')
                .Append(Convert.ToBase64String(SHA512.HashData(archiveBytes))).Append('\t')
                .Append(File.ReadAllText(archive + ".sha512")).Append('\t').Append(nuspecHash).Append('\t')
                .Append(nuspecHash).Append('\t').Append("https://api.nuget.org/v3/index.json\n");
        }
        var feedManifestPath = Path.Combine(root, "feed-manifest.tsv");
        File.WriteAllText(feedManifestPath, feedManifest.ToString());

        var policy =
            "<configuration><config><add key=\"signatureValidationMode\" value=\"require\" /></config>" +
            "<trustedSigners><repository name=\"nuget.org\" serviceIndex=\"https://api.nuget.org/v3/index.json\">" +
            "<certificate fingerprint=\"0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D\" hashAlgorithm=\"SHA256\" allowUntrustedRoot=\"false\" />" +
            "<certificate fingerprint=\"1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D\" hashAlgorithm=\"SHA256\" allowUntrustedRoot=\"false\" />" +
            "<certificate fingerprint=\"5A2901D6ADA3D18260B9C6DFE2133C95D74B9EEF6AE0E5DC334C8454D1477DF4\" hashAlgorithm=\"SHA256\" allowUntrustedRoot=\"false\" />" +
            "</repository></trustedSigners></configuration>\n";
        var policyPath = Path.Combine(packageDirectory, "qualification-nuget.config");
        File.WriteAllText(policyPath, policy);
        var simulateInput = Path.Combine(packageDirectory, "fixture", "b2-simulate-input.json");
        Directory.CreateDirectory(Path.GetDirectoryName(simulateInput)!);
        File.WriteAllText(simulateInput, "{}\n");

        var lockPaths = new[]
        {
            "AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/packages.lock.json",
            "AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/packages.lock.json",
            "AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner/packages.lock.json",
            "AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner.Tests/packages.lock.json"
        };
        foreach (var lockPath in lockPaths)
        {
            var path = Path.Combine(repository, lockPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{{\"fixture\":\"{lockPath}\"}}\n");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!,
                Path.GetFileNameWithoutExtension(Path.GetFileName(path)) + ".csproj"), "<Project />\n");
        }
        File.WriteAllText(Path.Combine(repository, ".gitignore"), "artifacts/\n");

        var fakeDotnet = Path.Combine(frozen, "dotnet");
        var fakeDotnetText = """
            #!/usr/bin/env bash
            set -euo pipefail
            root="$(dirname -- "$0")"
            evidence='@EVIDENCE@'
            mutation='@MUTATION@'
            mutation_marker='@MUTATION_MARKER@'
            if [[ "${1:-}" == --version ]]; then printf '8.0.130\n'; exit 0; fi
            if [[ "${1:-}" == --list-sdks ]]; then printf '8.0.130 [%s/sdk]\n' "$root"; exit 0; fi
            if [[ "${1:-}" == --list-runtimes ]]; then
              printf 'Microsoft.AspNetCore.App 8.0.30 [%s/shared/Microsoft.AspNetCore.App]\n' "$root"
              printf 'Microsoft.NETCore.App 8.0.30 [%s/shared/Microsoft.NETCore.App]\n' "$root"
              [[ "$mutation" != runtime-extra ]] || printf 'Microsoft.NETCore.App 8.0.31 [%s/shared/Microsoft.NETCore.App]\n' "$root"
              exit 0
            fi
            if [[ "${1:-}" == nuget && "${2:-}" == --version ]]; then printf 'NuGet Command Line\n6.8.2.32767\n'; exit 0; fi
            if [[ "${1:-}" == nuget && "${2:-}" == verify ]]; then
              package="${!#}"; name="$(basename "$package" .nupkg)"
              [[ "$mutation" != native-verify-failure ]] || exit 41
              display_name="$name"; [[ "$name" != synthetic.package.1.0.0 ]] || display_name='Synthetic.Package.1.0.0'
              printf 'Verifying %s\nSignature type: Repository\nService index: https://api.nuget.org/v3/index.json\n  SHA256 hash: 1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D\n' "$display_name"
              [[ "$mutation" == native-verify-missing-success ]] || printf 'Successfully verified package '\''%s'\''.\n' "$display_name"
              exit 0
            fi
            args=("$@")
            value_after() { local wanted="$1"; for ((i=0;i<${#args[@]}-1;i++)); do [[ "${args[$i]}" != "$wanted" ]] || { printf '%s' "${args[$((i+1))]}"; return; }; done; }
            artifact=''; for arg in "${args[@]}"; do [[ "$arg" != -p:ArtifactsPath=* && "$arg" != --artifacts-path=* ]] || artifact="${arg#*=}"; done
            if [[ -z "$artifact" ]]; then artifact="$(value_after --artifacts-path || true)"; fi
            if [[ "${1:-}" == restore ]]; then
              packages="$(value_after --packages)"; source="$(value_after --source)"; mkdir -p "$packages" "$artifact"
              flat_source=no; compgen -G "$source/*.nupkg" >/dev/null && flat_source=yes
              if [[ "$flat_source" == yes ]]; then
                cp -R --no-dereference -- '@ACQUIRED@/.' "$packages/"
              elif [[ "${2:-}" == *B6SnapshotRunner.Tests* ]]; then
                config="$(value_after --configfile)"; manifest="$(dirname "$config")/authority/TQ186.tsv"
                while IFS=$'\t' read -r id version; do mkdir -p "$packages/$id"; cp -R --no-dereference -- "$source/$id/$version" "$packages/$id/$version"; done < "$manifest"
              else
                cp -R --no-dereference -- "$source/." "$packages/"
              fi
              chmod -R u+w "$packages"
              for metadata in "$packages"/*/*/.nupkg.metadata; do
                [[ -f "$metadata" ]] || continue
                sed -i 's|"source":"[^"]*"|"source":"'"$source"'"|' "$metadata"
              done
              for name in one two three; do mkdir -p "$artifact/obj/$name"; printf 'assets-%s\n' "$name" > "$artifact/obj/$name/project.assets.json"; done
              if [[ "$flat_source" == no && "${2:-}" == *B6SnapshotRunner.Tests* && ! -e "$mutation_marker" ]]; then
                : > "$mutation_marker"
                case "$mutation" in
                  receipt-transaction) sed -i 's/^transaction_id\t.*/transaction_id\twrong-transaction/' "$evidence/a-l-transaction-receipt.tsv" ;;
                  receipt-missing-time) sed -i '/^trust_validation_completed_utc\t/d' "$evidence/a-l-transaction-receipt.tsv" ;;
                  receipt-malformed-time) sed -i 's/^trust_validation_completed_utc\t.*/trust_validation_completed_utc\tnot-a-time/' "$evidence/a-l-transaction-receipt.tsv" ;;
                  receipt-restart) sed -i 's/^boot_id\t.*/boot_id\t00000000-0000-0000-0000-000000000000/' "$evidence/a-l-transaction-receipt.tsv" ;;
                  receipt-log-missing) first=''; for file in "$evidence"/online-trust-logs/*.log; do first="$file"; break; done; rm -f "$first" ;;
                  receipt-log-mismatch) first=''; for file in "$evidence"/online-trust-logs/*.log; do first="$file"; break; done; printf tamper >> "$first" ;;
                  receipt-log-hash-mismatch) sed -i '2s/[0-9a-f]\{64\}$/0000000000000000000000000000000000000000000000000000000000000000/' "$evidence/online-trust-logs.tsv" ;;
                  promoted-tree-tamper) first=''; for file in "$source"/*/*/*.nuspec; do first="$file"; break; done; chmod u+w "$first"; printf tamper >> "$first" ;;
                esac
              fi
              exit 0
            fi
            if [[ "${1:-}" == build ]]; then
              runtime="$artifact/bin/AqualLifeStyle.B6SnapshotRunner/release"; mkdir -p "$runtime"
              printf runner > "$runtime/AqualLifeStyle.B6SnapshotRunner.dll"; printf core > "$runtime/AqualLifeStyle.Core.dll"; printf ef > "$runtime/AqualLifeStyle.EntityFrameworkCore.dll"
              commit="$(git -C "$(dirname -- "${2}")" rev-parse HEAD)"
              for assembly in AqualLifeStyle.B6SnapshotRunner AqualLifeStyle.Core AqualLifeStyle.EntityFrameworkCore; do
                generated="$artifact/obj/$assembly/release"; mkdir -p "$generated"
                printf '[assembly: System.Reflection.AssemblyInformationalVersionAttribute("fixture+%s")]\n' "$commit" > "$generated/$assembly.AssemblyInfo.cs"
              done
              printf 'a.cs(1,1): warning CS0628: one\nb.cs(1,1): warning CS0628: two\nc.cs(1,1): warning CA2255: three\n'
              exit 0
            fi
            if [[ "${1:-}" == test ]]; then results="$(value_after --results-directory)"; mkdir -p "$results"; printf '<TestRun />\n' > "$results/gate-q.trx"; exit 0; fi
            if [[ "${1:-}" == *.dll && "${2:-}" == verify-test-run ]]; then printf '{"discovered":61,"executed":61,"passed":61,"failed":0,"skipped":0}\n'; exit 0; fi
            if [[ "${1:-}" == *.dll && "${2:-}" == simulate ]]; then
              output="$(value_after --output)"; result=ok
              [[ "$mutation" != simulation-output-mismatch || "$output" != *simulate-b-output* ]] || result=different
              printf '{"result":"%s"}\n' "$result" > "$output"; exit 0
            fi
            if [[ "${1:-}" == *.dll && "${2:-}" == verify-simulation-output ]]; then
              printf '{"result":"PASS","projectionSchemaVersion":"B6GateASemanticProjectionV1","semanticProjectionSha256":"747ec92ca0d9f50eccf82079bc8f878a97a3b59a270c6e10b7b1cd3b54ce728a"}\n'; exit 0
            fi
            if [[ "${1:-}" == *.dll && "${2:-}" == sensitive-probe ]]; then
              [[ "$mutation" != operation-failure ]] || exit 9
              [[ "$mutation" != qualification-evidence-tamper ]] || printf tamper >> "$evidence/qualification-verdict.tsv"
              [[ "$mutation" != signal-int-before-finalization ]] || kill -INT "$PPID"
              [[ "$mutation" != signal-term-before-finalization ]] || kill -TERM "$PPID"
              printf 'B6_VERIFIED_SENSITIVE_CAPABILITY_VISIBLE\n'; exit 0
            fi
            printf 'unsupported fake dotnet invocation: %s\n' "$*" >&2; exit 97
            """
            .Replace("@EVIDENCE@", evidence, StringComparison.Ordinal)
            .Replace("@MUTATION@", scenario, StringComparison.Ordinal)
            .Replace("@ACQUIRED@", acquired, StringComparison.Ordinal)
            .Replace("@MUTATION_MARKER@", mutationMarker, StringComparison.Ordinal);
        File.WriteAllText(fakeDotnet, fakeDotnetText);
        File.SetUnixFileMode(fakeDotnet, UnixFileMode.UserRead | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var bwrapText = """
            #!/usr/bin/env bash
            set -euo pipefail
            mutation='@MUTATION@'
            printf '%q ' "$@" >> '@LOG@'; printf '\n' >> '@LOG@'
            args=("$@"); command=();
            for ((i=0;i<${#args[@]};i++)); do
              if [[ "${args[$i]}" == /bin/bash && "${args[$((i+1))]:-}" == /run/aqua/network-witness ]]; then
                command=("${args[@]:$((i+4))}"); break
              fi
            done
            [[ "${#command[@]}" -gt 0 ]]
            (
              IFS= read -r setup <&9; [[ "$setup" == SETUP ]]
              (
                child="$BASHPID"; start="$(/usr/bin/mawk '{print $22}' "/proc/$child/stat")"; net="$(readlink "/proc/$child/ns/net")"
                pid='2'; uid_map='65534 0 1'; gid_map='65534 0 1'; groups='65534'; cap='0000000000000000'; no_new_privileges='1'
                [[ "$mutation" != witness-uid-dynamic ]] || uid_map='65534 1000 1'
                [[ "$mutation" != witness-gid-dynamic ]] || gid_map='65534 1000 1'
                [[ "$mutation" != witness-wrong-inside-uid ]] || uid_map='65533 0 1'
                [[ "$mutation" != witness-wrong-inside-gid ]] || gid_map='65533 0 1'
                [[ "$mutation" != witness-wrong-uid-range ]] || uid_map='65534 0 2'
                [[ "$mutation" != witness-wrong-gid-range ]] || gid_map='65534 0 2'
                [[ "$mutation" != witness-map-whitespace ]] || uid_map=' 65534 0 1 '
                [[ "$mutation" != witness-pid ]] || pid='3'
                [[ "$mutation" != witness-starttime ]] || start='1'
                [[ "$mutation" != witness-groups ]] || groups='0 65534'
                [[ "$mutation" != witness-capabilities ]] || cap='0000000000000001'
                [[ "$mutation" != witness-no-new-privileges ]] || no_new_privileges='0'
                printf 'pid\t%s\nppid\t1\nstarttime\t%s\nuid\t65534\ngid\t65534\ngroups\t%s\n' "$pid" "$start" "$groups" >&8
                [[ "$mutation" == witness-uid-map-missing ]] || printf 'uid_map\t%s\n' "$uid_map" >&8
                [[ "$mutation" != witness-uid-map-extra ]] || printf 'uid_map\t0 0 1\n' >&8
                [[ "$mutation" == witness-gid-map-missing ]] || printf 'gid_map\t%s\n' "$gid_map" >&8
                [[ "$mutation" != witness-gid-map-extra ]] || printf 'gid_map\t0 0 1\n' >&8
                printf 'network_namespace\t%s\nCapInh\t0000000000000000\nCapPrm\t0000000000000000\nCapEff\t%s\nCapBnd\t0000000000000000\nCapAmb\t0000000000000000\nNoNewPrivs\t%s\nEND\n' "$net" "$cap" "$no_new_privileges" >&8
                IFS= read -r release <&7; [[ "$release" == RELEASE ]]; exec 8>&- 7<&-; exec "${command[@]}"
              ) &
              child=$!; wait "$child"
            ) &
            init=$!; printf '{"child-pid":%s}\n' "$init" >&3
            set +e; wait "$init"; status=$?; set -e
            printf '{"exit-code":%s}\n' "$status" >&3
            exit "$status"
            """.Replace("@LOG@", bwrapLog, StringComparison.Ordinal)
                .Replace("@MUTATION@", scenario, StringComparison.Ordinal);
        File.WriteAllText(fakeBwrap, bwrapText);
        File.SetUnixFileMode(fakeBwrap, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var finalMonotonic = scenario == "monotonic-reversal" ? 199 : 200 + elapsedSeconds * 100;
        File.WriteAllText(monotonicValues, "100\n150\n200\n" +
            string.Concat(Enumerable.Repeat($"{finalMonotonic}\n", 6)));
        File.WriteAllText(monotonicProgram, """
            #!/usr/bin/env bash
            set -euo pipefail
            file='@VALUES@'; value="$(/usr/bin/mawk 'NR==1 {print; exit}' "$file")"; /usr/bin/mawk 'NR>1' "$file" > "$file.next"; mv "$file.next" "$file"; printf '%s\n' "$value"
            """.Replace("@VALUES@", monotonicValues, StringComparison.Ordinal));
        File.SetUnixFileMode(monotonicProgram, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var toolchainManifest = Path.Combine(root, "toolchain-manifest.tsv");
        var dotnetBytes = File.ReadAllBytes(fakeDotnet);
        File.WriteAllText(toolchainManifest,
            $"type\tmode\tsize\tsha256\tlink_target\tpath\nF\t555\t{dotnetBytes.Length}\t{B6Values.Sha256Hex(dotnetBytes)}\t-\tdotnet\n");
        if (scenario == "toolchain-unexpected")
        {
            File.WriteAllText(Path.Combine(frozen, "unexpected"), "unexpected");
            File.SetUnixFileMode(Path.Combine(frozen, "unexpected"), UnixFileMode.UserRead |
                UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
        if (scenario == "toolchain-missing") File.Delete(fakeDotnet);
        File.SetUnixFileMode(frozen, UnixFileMode.UserRead | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var runnerPath = Path.Combine(packageDirectory, "authoritative-runner.sh");
        var runner = File.ReadAllText(CanonicalB6EntrypointFromTestSource());
        runner = ReplaceExactlyOnce(runner, "expected_g_count='197'", "expected_g_count='2'");
        runner = ReplaceExactlyOnce(runner, "expected_tq_count='186'", "expected_tq_count='1'");
        runner = ReplaceExactlyOnce(runner, "expected_u_count='215'", "expected_u_count='3'");
        runner = ReplaceExactlyOnce(runner, CanonicalGraphSha256, B6Values.Sha256Hex(gManifest));
        runner = ReplaceExactlyOnce(runner, QualificationGraphSha256, B6Values.Sha256Hex(tqManifest));
        runner = ReplaceExactlyOnce(runner, "372c918aa794e976601ee292caa9c56a884784ebcfe04cd53981f76d3e982dd1", B6Values.Sha256Hex(uManifest));
        runner = ReplaceExactlyOnce(runner, "9598c6dd7cab8f0dc62a31be8c73a3c030c5d92d26d120689f06bf1b60b0980a", B6Values.Sha256Hex(u1Manifest));
        runner = ReplaceExactlyOnce(runner, "84ba6476727924aecc6b94207f6de1fdc1135804edbea4d32b7984ede77a6b35", B6Values.Sha256Hex(u2Manifest));
        runner = ReplaceExactlyOnce(runner, "1a56a8bad2fed7875f3326f620ce40af7c0f805d7f16696efa7f1eceaf816df7", B6Values.Sha256Hex(feedManifest.ToString()));
        runner = ReplaceExactlyOnce(runner, "1c609222deecbbc9ef65d311528ad1eb19ff26adf70227752ee95cca73a5910c", B6Values.Sha256Hex(File.ReadAllBytes(toolchainManifest)));
        for (var index = 0; index < lockPaths.Length; index++)
        {
            var oldHash = new[]
            {
                "9f467700282dc48f8bdc172eb8f1830a3167cbc4efaa223a328b1ff00c4acddc",
                "2b1925d618a39d10fa57e3e8455f06c3565de75ccdc3ae4a9021e357c9d07356",
                "401fafbcfa35347666a4de3fcfa92be9c66399663d8f8410eca420879c3d7b7b",
                "e575bab05aa361751f35ce31340880c0286e6133c7f36b7e3e1fb2ff8bbf0a47"
            }[index];
            runner = ReplaceExactlyOnce(runner, oldHash,
                B6Values.Sha256Hex(File.ReadAllBytes(Path.Combine(repository, lockPaths[index]))));
        }
        runner = ReplaceExactlyOnce(runner, "0d131692c7d4035f4ed890d6b32fb127bd6fe423db700b2a0812c2438ff66f79", B6Values.Sha256Hex(policy));
        runner = ReplaceExactlyOnce(runner, "5ab46a2fe268ad525c42ae06a47fb5a105802b37d0733c48fcdc093a14398a5c", B6Values.Sha256Hex(File.ReadAllBytes(simulateInput)));
        runner = ReplaceExactlyOnce(runner, "045a8891a4e84d5acec53b3650e2ea4210bc6d3fc4ddc2d6c8de63662afa56aa", B6Values.Sha256Hex(dotnetBytes));
        runner = ReplaceExactlyOnce(runner, "readonly bwrap='/usr/bin/bwrap'", $"readonly bwrap='{fakeBwrap}'");
        if (scenario != "bootstrap-nonroot")
            runner = ReplaceExactlyOnce(runner,
                "bootstrap_uid=\"$(id -u)\"; bootstrap_gid=\"$(id -g)\"",
                "bootstrap_uid=0; bootstrap_gid=0");
        runner = ReplaceFunction(runner, "monotonic_centiseconds()", $"monotonic_centiseconds() {{ \"{monotonicProgram}\"; }}");
        runner = ReplaceExactlyOnce(runner, "parent_net=\"$(readlink /proc/self/ns/net)\"", "parent_net='net:[fixture-parent]'");
        if (scenario == "transaction-cleanup-failure")
            runner = ReplaceFunction(runner, "transaction_critical_cleanup()",
                "transaction_critical_cleanup() { transaction_cleanup_result='FAIL'; return 1; }");
        if (scenario is "signal-int-cleanup" or "signal-term-cleanup")
        {
            var signal = scenario == "signal-int-cleanup" ? "INT" : "TERM";
            runner = ReplaceFunction(runner, "transaction_critical_cleanup()",
                $"transaction_critical_cleanup() {{ transaction_cleanup_result='PASS'; kill -{signal} \"$$\"; return 0; }}");
        }
        if (scenario is "terminal-manifest-creation-failure" or "stale-pass-and-terminal-manifest-failure")
            runner = ReplaceFunction(runner, "stage_terminal_manifest()",
                "stage_terminal_manifest() { return 1; }");
        if (scenario == "terminal-manifest-verification-failure")
            runner = ReplaceFunction(runner, "verify_terminal_manifest()",
                "verify_terminal_manifest() { return 1; }");
        if (scenario == "terminal-receipt-staging-failure")
            runner = ReplaceFunction(runner, "stage_terminal_receipt()",
                "stage_terminal_receipt() { return 1; }");
        if (scenario == "terminal-receipt-publish-failure")
            runner = ReplaceExactlyOnce(runner,
                "atomic_write() {\n  local destination=\"$1\" source=\"$2\" temporary",
                "atomic_write() {\n  local destination=\"$1\" source=\"$2\" temporary\n" +
                "  [[ \"${destination}\" != \"${evidence_dir}/final-verdict.tsv\" ]] || return 1");
        if (scenario == "atomic-copy-failure")
            runner = ReplaceExactlyOnce(runner,
                "if ! cp -- \"${source}\" \"${temporary}\"; then",
                "if ! false; then");
        if (scenario == "atomic-permission-failure")
            runner = ReplaceExactlyOnce(runner,
                "if ! chmod 600 \"${temporary}\"; then",
                "if ! false; then");
        if (scenario == "atomic-rename-failure")
            runner = ReplaceExactlyOnce(runner,
                "if ! mv -fT -- \"${temporary}\" \"${destination}\"; then",
                "if ! false; then");
        if (scenario is "stale-terminal-receipt" or "stale-pass-and-terminal-manifest-failure")
            runner = ReplaceFunction(runner, "enter_finalization_critical_section()",
                "enter_finalization_critical_section() { printf 'final_transaction_result\\tPASS\\nprocess_exit_code\\t0\\n' > \"${evidence_dir}/final-verdict.tsv\"; trap '' INT TERM; terminal_finalization_active='YES'; }");
        if (scenario is "signal-int-boundary" or "signal-term-boundary")
        {
            var signal = scenario == "signal-int-boundary" ? "INT" : "TERM";
            runner = ReplaceFunction(runner, "enter_finalization_critical_section()",
                $"enter_finalization_critical_section() {{ kill -{signal} \"$$\"; trap '' INT TERM; terminal_finalization_active='YES'; }}");
        }
        if (scenario is "signal-int-finalization" or "signal-term-finalization")
        {
            var signal = scenario == "signal-int-finalization" ? "INT" : "TERM";
            runner = ReplaceFunction(runner, "enter_finalization_critical_section()",
                $"enter_finalization_critical_section() {{ trap '' INT TERM; terminal_finalization_active='YES'; kill -{signal} \"$$\"; }}");
        }
        if (scenario is "signal-int-after-finalization" or "signal-term-after-finalization")
        {
            var signal = scenario == "signal-int-after-finalization" ? "INT" : "TERM";
            runner = ReplaceFunction(runner, "best_effort_housekeeping()",
                $"best_effort_housekeeping() {{ kill -{signal} \"$$\"; return 1; }}");
        }
        if (scenario == "housekeeping-failure")
            runner = ReplaceFunction(runner, "best_effort_housekeeping()",
                "best_effort_housekeeping() { return 1; }");
        if (scenario == "mawk-mismatch")
            runner = ReplaceExactlyOnce(runner, "3850bf893d3f4f8ec462c8b59d6d66fdbbe8ddc0715b3d404d9cc574526b781c", new string('0', 64));
        if (scenario == "mawk-version-mismatch")
            runner = ReplaceExactlyOnce(runner, "expected_mawk_version='mawk 1.3.4 20240123'",
                "expected_mawk_version='mawk 0.0.0 invalid'");
        var controlledFinalTime =
            $"final_package_consumption_at=\"$(date -u -d \"${{trust_validation_completed_at}} + {elapsedSeconds} seconds\" +%Y-%m-%dT%H:%M:%SZ)\"";
        runner = ReplaceExactlyOnce(runner,
            "final_package_consumption_at=\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"", controlledFinalTime);
        if (scenario == "clock-reversal")
            runner = ReplaceExactlyOnce(runner, controlledFinalTime,
                "final_package_consumption_at='1970-01-01T00:00:00Z'");
        if (scenario == "clock-discontinuity")
            runner = ReplaceExactlyOnce(runner, controlledFinalTime,
                $"final_package_consumption_at=\"$(date -u -d \"${{trust_validation_completed_at}} + {elapsedSeconds + 10} seconds\" +%Y-%m-%dT%H:%M:%SZ)\"");
        File.WriteAllText(runnerPath, runner);
        File.SetUnixFileMode(runnerPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        if (scenario == "package-substitution") File.AppendAllText(Directory.EnumerateFiles(feed).First(), "substitution");
        Run(repository, "git", "init");
        Run(repository, "git", "add", ".");
        Run(repository, "git", "-c", "user.name=B6 Test", "-c", "user.email=b6@example.test", "commit", "-m", "current runner fixture");
        var commit = Run(repository, "git", "rev-parse", "HEAD").Trim();
        return new CurrentRunnerFixture(root, repository, commit, evidence, bwrapLog,
        [
            "--frozen-dotnet-root", frozen,
            "--toolchain-manifest", toolchainManifest,
            "--acquisition-feed", feed,
            "--feed-manifest", feedManifestPath,
            "--evidence-dir", evidence,
            "runner", "sensitive-probe"
        ]);
    }

    private static string ReplaceExactlyOnce(string value, string oldValue, string newValue)
    {
        var index = value.IndexOf(oldValue, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"Missing fixture replacement: {oldValue}");
        value.IndexOf(oldValue, index + oldValue.Length, StringComparison.Ordinal).ShouldBe(-1,
            $"Non-unique fixture replacement: {oldValue}");
        return value[..index] + newValue + value[(index + oldValue.Length)..];
    }

    private static string ReplaceFunction(string value, string signature, string replacement)
    {
        var start = value.IndexOf(signature, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var open = value.IndexOf('{', start);
        var close = value.IndexOf("\n}", open, StringComparison.Ordinal);
        close.ShouldBeGreaterThan(open);
        return value[..start] + replacement + value[(close + 2)..];
    }

    private static void RemoveCurrentRunnerFixture(string root)
    {
        if (!Directory.Exists(root)) return;
        Run(root, "chmod", "-R", "u+w", root);
        Directory.Delete(root, recursive: true);
    }

    private static string TestSourceMaterializationDirectory()
    {
        var testSourceMaterializationDirectory = typeof(B6PrimitiveContractTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(metadata => metadata.Key == TestSourceMaterializationMetadataName)?.Value;
        if (string.IsNullOrWhiteSpace(testSourceMaterializationDirectory) ||
            !Path.IsPathFullyQualified(testSourceMaterializationDirectory))
            throw new InvalidOperationException("The B6 test source materialization was not bound at test build time.");

        var expectedTestSourceRepositoryRoot = Path.GetFullPath(testSourceMaterializationDirectory);
        var canonicalB6EntrypointPath = CanonicalB6EntrypointPath(expectedTestSourceRepositoryRoot);
        if (!Directory.Exists(expectedTestSourceRepositoryRoot) || SecureFiles.IsLink(expectedTestSourceRepositoryRoot) ||
            !File.Exists(canonicalB6EntrypointPath) || SecureFiles.IsLink(canonicalB6EntrypointPath))
            throw new InvalidOperationException(
                "The build-time B6 test source materialization does not contain the canonical regular-file entrypoint.");
        return expectedTestSourceRepositoryRoot;
    }

    private static string CanonicalB6EntrypointFromTestSource() =>
        CanonicalB6EntrypointPath(TestSourceMaterializationDirectory());

    private static string TestSourcePackageDirectory() => Path.Combine(TestSourceMaterializationDirectory(),
        "docs", "operations", "aqgreen-v2-b6", "snapshot-package");

    private static string CanonicalB6EntrypointPath(string testSourceMaterializationDirectory) =>
        Path.Combine(testSourceMaterializationDirectory, "docs", "operations", "aqgreen-v2-b6",
            "snapshot-package", "authoritative-runner.sh");

    private static string RunBootstrap(string repository, string reviewedCommit,
        IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var result = RunBootstrapCaptured(repository, reviewedCommit, environment, arguments);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"B6 bootstrap failed with exit code {result.ExitCode}: {result.StandardError}");
        return result.StandardOutput + result.StandardError;
    }

    private static CommandResult RunBootstrapCaptured(string repository, string reviewedCommit,
        params string[] arguments) =>
        RunBootstrapCaptured(repository, reviewedCommit, new Dictionary<string, string>(), arguments);

    private static CommandResult RunBootstrapCaptured(string repository, string reviewedCommit,
        IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        const string bootstrapPath =
            "docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh";
        var shellArguments = new List<string>
        {
            "-c",
            "set -o pipefail; GIT_NO_REPLACE_OBJECTS=1 git -C \"$1\" show \"$2:$3\" | bash -s -- --repository \"$1\" --reviewed-commit \"$2\" \"${@:4}\"",
            "b6-bootstrap", repository, reviewedCommit, bootstrapPath
        };
        shellArguments.AddRange(arguments);
        return RunCaptured(repository, "bash", environment, shellArguments.ToArray());
    }

    private static string WriteTrx(int total, int executed, int passed, int failed, int skipped,
        string? outcome = "Completed", IReadOnlyDictionary<string, int>? counterOverrides = null,
        string extraCounters = "")
    {
        var outcomeAttribute = outcome is null ? string.Empty : $" outcome=\"{outcome}\"";
        return WriteRawTrx($"<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">" +
            $"<ResultSummary{outcomeAttribute}>" +
            CountersXml(total, executed, passed, failed, skipped, counterOverrides, extraCounters) +
            "</ResultSummary></TestRun>");
    }

    private static string CountersXml(int total, int executed, int passed, int failed, int skipped,
        IReadOnlyDictionary<string, int>? overrides = null, string extraCounters = "")
    {
        var counters = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["total"] = total,
            ["executed"] = executed,
            ["passed"] = passed,
            ["failed"] = failed,
            ["error"] = 0,
            ["timeout"] = 0,
            ["aborted"] = 0,
            ["inconclusive"] = 0,
            ["passedButRunAborted"] = 0,
            ["notRunnable"] = 0,
            ["notExecuted"] = skipped,
            ["disconnected"] = 0,
            ["warning"] = 0,
            ["completed"] = 0,
            ["inProgress"] = 0,
            ["pending"] = 0
        };
        if (overrides is not null)
            foreach (var (name, value) in overrides) counters[name] = value;
        return "<Counters " + string.Join(" ", counters.Select(pair => $"{pair.Key}=\"{pair.Value}\"")) +
            extraCounters + " />";
    }

    private static string WriteRawTrx(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "b6-test-results-" + Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(path, content);
        return path;
    }

    private static void AssertTrxRejected(string content)
    {
        var path = WriteRawTrx(content);
        try
        {
            Should.Throw<B6PackageException>(() => B6TestRunResults.ReadAndValidate(path, expectedCount: 1));
        }
        finally { File.Delete(path); }
    }

    private static string Run(string workingDirectory, string file, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo(file)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException($"Unable to start {file}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{file} failed with exit code {process.ExitCode}: {stderr.GetAwaiter().GetResult()}");
        return stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
    }

    private static string RunWithEnvironment(string workingDirectory, string file,
        IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var result = RunCaptured(workingDirectory, file, environment, arguments);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"{file} failed with exit code {result.ExitCode}: {result.StandardOutput}{result.StandardError}");
        return result.StandardOutput + result.StandardError;
    }

    private static CommandResult RunCaptured(string workingDirectory, string file,
        IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo(file)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var (name, value) in environment) start.Environment[name] = value;
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException($"Unable to start {file}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
