using System.Text;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.B6SnapshotRunner.Tests;

public sealed class B6PrimitiveContractTests
{
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

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
    public void OperationalProvenance_RejectsExecutingAssemblyBuiltFromIgnoredCompileSource()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "b6-reviewed-tree-test-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(testRoot, "repository");
        var reviewedOutput = Path.Combine(testRoot, "reviewed-output");
        var liveOutput = Path.Combine(testRoot, "live-output");
        Directory.CreateDirectory(repository);
        try
        {
            var backend = Path.Combine(repository, "AqualLifeStyle", "9.4.2", "aspnet-core");
            var runnerProject = Path.Combine(backend, "tools", "AqualLifeStyle.B6SnapshotRunner");
            var coreProject = Path.Combine(backend, "src", "AqualLifeStyle.Core");
            var efProject = Path.Combine(backend, "src", "AqualLifeStyle.EntityFrameworkCore");
            Directory.CreateDirectory(runnerProject);
            Directory.CreateDirectory(coreProject);
            Directory.CreateDirectory(efProject);
            File.WriteAllText(Path.Combine(backend, ".gitignore"), "[Bb]in/\n[Oo]bj/\n[Rr]elease/\n");
            File.WriteAllText(Path.Combine(backend, "NuGet.Config"),
                "<configuration><packageSources><clear /></packageSources></configuration>\n");
            File.WriteAllText(Path.Combine(coreProject, "AqualLifeStyle.Core.csproj"), MinimalProject("AqualLifeStyle.Core"));
            File.WriteAllText(Path.Combine(coreProject, "Marker.cs"), "namespace FixtureCore; public sealed class Marker { }\n");
            File.WriteAllText(Path.Combine(efProject, "AqualLifeStyle.EntityFrameworkCore.csproj"), MinimalProject("AqualLifeStyle.EntityFrameworkCore"));
            File.WriteAllText(Path.Combine(efProject, "Marker.cs"), "namespace FixtureEf; public sealed class Marker { }\n");
            File.WriteAllText(Path.Combine(runnerProject, "AqualLifeStyle.B6SnapshotRunner.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net8.0</TargetFramework>
                    <AssemblyName>AqualLifeStyle.B6SnapshotRunner</AssemblyName>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="../../src/AqualLifeStyle.Core/AqualLifeStyle.Core.csproj" />
                    <ProjectReference Include="../../src/AqualLifeStyle.EntityFrameworkCore/AqualLifeStyle.EntityFrameworkCore.csproj" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(runnerProject, "Program.cs"),
                "System.Console.WriteLine(typeof(FixtureCore.Marker).FullName + typeof(FixtureEf.Marker).FullName);\n");

            Run(repository, "git", "init");
            Run(repository, "git", "add", ".");
            Run(repository, "git", "-c", "user.name=B6 Test", "-c", "user.email=b6@example.test",
                "commit", "-m", "reviewed tree");
            var reviewedCommit = Run(repository, "git", "rev-parse", "HEAD").Trim();

            Run(repository, "dotnet", "build",
                Path.Combine(runnerProject, "AqualLifeStyle.B6SnapshotRunner.csproj"),
                "--configuration", "Release", "--output", reviewedOutput,
                "--disable-build-servers");
            var reviewedIdentity = new ToolchainIdentity(reviewedCommit, B6Contract.OperationalEvidence,
                ReadAssembly(reviewedOutput, "AqualLifeStyle.B6SnapshotRunner"),
                [ReadAssembly(reviewedOutput, "AqualLifeStyle.Core"),
                    ReadAssembly(reviewedOutput, "AqualLifeStyle.EntityFrameworkCore")]);
            reviewedIdentity.RequireMatchesReviewedTreeBuild(repository, reviewedCommit);

            var ignoredSourceDirectory = Path.Combine(runnerProject, "Release");
            Directory.CreateDirectory(ignoredSourceDirectory);
            File.WriteAllText(Path.Combine(ignoredSourceDirectory, "Injected.cs"),
                """
                using System.Runtime.CompilerServices;
                internal static class IgnoredInjection
                {
                    [ModuleInitializer]
                    internal static void Initialize() => System.Console.Error.Write("B6_IGNORED_SOURCE_INJECTED");
                }
                """);

            Run(repository, "git", "status", "--porcelain=v1", "--untracked-files=all").ShouldBeEmpty();
            Run(repository, "dotnet", "build",
                Path.Combine(runnerProject, "AqualLifeStyle.B6SnapshotRunner.csproj"),
                "--configuration", "Release", "--output", liveOutput, "--disable-build-servers");
            Run(repository, "dotnet", Path.Combine(liveOutput, "AqualLifeStyle.B6SnapshotRunner.dll"))
                .ShouldContain("B6_IGNORED_SOURCE_INJECTED");

            var identity = new ToolchainIdentity(reviewedCommit, B6Contract.OperationalEvidence,
                ReadAssembly(liveOutput, "AqualLifeStyle.B6SnapshotRunner"),
                [ReadAssembly(liveOutput, "AqualLifeStyle.Core"),
                    ReadAssembly(liveOutput, "AqualLifeStyle.EntityFrameworkCore")]);

            Should.Throw<B6PackageException>(() =>
                    identity.RequireMatchesReviewedTreeBuild(repository, reviewedCommit))
                .Message.ShouldContain("reviewed Git object tree");
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

    private static FirstPartyAssemblyIdentity ReadAssembly(string root, string name)
    {
        var content = File.ReadAllBytes(Path.Combine(root, name + ".dll"));
        return new FirstPartyAssemblyIdentity(name, B6Values.Sha256Hex(content), "test");
    }

    private static string MinimalProject(string assemblyName) =>
        $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework>" +
        $"<AssemblyName>{assemblyName}</AssemblyName></PropertyGroup></Project>\n";

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
}
