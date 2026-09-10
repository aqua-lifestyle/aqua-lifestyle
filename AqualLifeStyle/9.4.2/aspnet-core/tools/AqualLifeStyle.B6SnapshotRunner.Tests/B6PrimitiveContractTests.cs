using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        var bootstrap = File.ReadAllText(CanonicalB6EntrypointFromTestSource());
        foreach (var contract in lockContracts)
            bootstrap.ShouldContain(contract.Sha256);
        bootstrap.ShouldContain("verify_lock_set");
        bootstrap.ShouldContain("dotnet restore");
        bootstrap.ShouldContain("--locked-mode");
        bootstrap.ShouldNotContain("--force-evaluate");
        bootstrap.ShouldNotContain("--ignore-failed-sources");
        bootstrap.ShouldNotContain("ResolverSupport");
        bootstrap.ShouldContain("dotnet build");
        bootstrap.ShouldContain("--no-restore");
        bootstrap.ShouldContain("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true");
        bootstrap.ShouldContain("network-boundary=REQUIRED_EXTERNAL_WITNESS");
        bootstrap.ShouldContain("Bubblewrap network namespace");
        bootstrap.ToLowerInvariant().ShouldNotContain("python");
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
        if (OperatingSystem.IsWindows()) return;
        var testRoot = Path.Combine(Path.GetTempPath(), "b6-preauthority-test-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(testRoot, "repository");
        var unverifiedMarker = Path.Combine(testRoot, "unverified-marker");
        var verifiedMarker = Path.Combine(testRoot, "verified-marker");
        var buildLeakMarker = Path.Combine(testRoot, "build-leak-marker");
        var checkoutHookMarker = Path.Combine(testRoot, "checkout-hook-marker");
        var checkoutFilterMarker = Path.Combine(testRoot, "checkout-filter-marker");
        var configuredPackageSource = Path.Combine(testRoot, "configured-package-source");
        var configuredPackage = new[] { (Id: "configured.source.package", Version: "1.0.0") };
        const string sentinel = "B6-SYNTHETIC-SENSITIVE-CAPABILITY";
        Directory.CreateDirectory(repository);
        Directory.CreateDirectory(configuredPackageSource);
        try
        {
            WriteSyntheticNuGetPackage(configuredPackageSource, configuredPackage[0].Id,
                configuredPackage[0].Version);
            var runnerProject = CreateSourceLinkFixture(repository, buildLeakMarker,
                packageReferences: configuredPackage, configuredNugetSource: configuredPackageSource,
                lockGenerationSource: configuredPackageSource);
            Run(repository, "git", "init");
            Run(repository, "git", "remote", "add", "origin", "https://github.com/aqua-lifestyle/aqua-lifestyle.git");
            Run(repository, "git", "add", ".");
            Run(repository, "git", "-c", "user.name=B6 Test", "-c", "user.email=b6@example.test",
                "commit", "-m", "reviewed source");
            var reviewedCommit = Run(repository, "git", "rev-parse", "HEAD").Trim();

            var ignoredSourceDirectory = Path.Combine(Path.GetDirectoryName(runnerProject)!, "Release");
            Directory.CreateDirectory(ignoredSourceDirectory);
            File.WriteAllText(Path.Combine(ignoredSourceDirectory, "Injected.cs"),
                """
                using System.Runtime.CompilerServices;
                internal static class IgnoredInjection
                {
                    [ModuleInitializer]
                    internal static void Initialize()
                    {
                        var sentinel = System.Environment.GetEnvironmentVariable("B6_TEST_SENSITIVE_DSN_SENTINEL");
                        var marker = System.Environment.GetEnvironmentVariable("B6_TEST_UNVERIFIED_MARKER");
                        if (sentinel != null && marker != null) System.IO.File.WriteAllText(marker, sentinel);
                    }
                }
                """);

            var injectedSource = Path.Combine(ignoredSourceDirectory, "Injected.cs");
            var injectedRelativeDirectory = Path.GetRelativePath(repository, ignoredSourceDirectory);
            var gitDirectory = Path.GetFullPath(Run(repository, "git", "rev-parse", "--git-common-dir").Trim(), repository);
            var hook = Path.Combine(gitDirectory, "hooks", "post-checkout");
            File.WriteAllText(hook,
                $"#!/usr/bin/env bash\nset -eu\nprintf hook > '{checkoutHookMarker}'\n" +
                $"mkdir -p \"$PWD/{injectedRelativeDirectory}\"\n" +
                $"cp '{injectedSource}' \"$PWD/{injectedRelativeDirectory}/HookInjected.cs\"\n");
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var filter = Path.Combine(testRoot, "checkout-filter.sh");
            File.WriteAllText(filter,
                $"#!/usr/bin/env bash\nset -eu\ncat\nprintf filter > '{checkoutFilterMarker}'\n" +
                $"mkdir -p \"$PWD/{injectedRelativeDirectory}\"\n" +
                $"cp '{injectedSource}' \"$PWD/{injectedRelativeDirectory}/FilterInjected.cs\"\n");
            File.SetUnixFileMode(filter, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(Path.Combine(gitDirectory, "info", "attributes"),
                Path.GetRelativePath(repository, Path.Combine(Path.GetDirectoryName(runnerProject)!, "Program.cs")) +
                " filter=b6-inject\n");
            Run(repository, "git", "config", "filter.b6-inject.smudge", $"bash '{filter}'");
            Run(repository, "git", "config", "filter.b6-inject.clean", "cat");

            var ordinaryCheckout = Path.Combine(testRoot, "ordinary-checkout");
            Run(repository, "git", "worktree", "add", "--detach", ordinaryCheckout, reviewedCommit);
            File.ReadAllText(checkoutHookMarker).ShouldBe("hook");
            File.ReadAllText(checkoutFilterMarker).ShouldBe("filter");
            File.Exists(Path.Combine(ordinaryCheckout, injectedRelativeDirectory, "HookInjected.cs")).ShouldBeTrue();
            File.Exists(Path.Combine(ordinaryCheckout, injectedRelativeDirectory, "FilterInjected.cs")).ShouldBeTrue();
            Run(ordinaryCheckout, "git", "status", "--porcelain=v1", "--untracked-files=all").ShouldBeEmpty();
            Run(repository, "git", "worktree", "remove", "--force", ordinaryCheckout);
            File.Delete(checkoutHookMarker);
            File.Delete(checkoutFilterMarker);

            var ordinaryPackageCache = Path.Combine(testRoot, "ordinary-package-cache");
            Directory.CreateDirectory(ordinaryPackageCache);
            RunWithEnvironment(repository, "dotnet", new Dictionary<string, string>
            {
                ["B6_TEST_SENSITIVE_DSN_SENTINEL"] = sentinel,
                ["NUGET_PACKAGES"] = ordinaryPackageCache,
                ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true"
            }, "build", runnerProject, "--configuration", "Release", "--disable-build-servers");
            File.ReadAllText(buildLeakMarker).Trim().ShouldBe(sentinel);
            File.Delete(buildLeakMarker);
            var ordinaryRunner = Path.Combine(Path.GetDirectoryName(runnerProject)!, "bin", "Release", "net8.0",
                "AqualLifeStyle.B6SnapshotRunner.dll");
            RunWithEnvironment(repository, "dotnet", new Dictionary<string, string>
            {
                ["B6_TEST_SENSITIVE_DSN_SENTINEL"] = sentinel,
                ["B6_TEST_UNVERIFIED_MARKER"] = unverifiedMarker
            }, ordinaryRunner);
            File.ReadAllText(unverifiedMarker).ShouldBe(sentinel);
            File.Delete(unverifiedMarker);

            Directory.CreateDirectory(Path.Combine(repository, "AqualLifeStyle", "9.4.2", "aspnet-core",
                "artifacts", "aqgreen-b6-authoritative"));
            File.WriteAllText(Path.Combine(repository, "AqualLifeStyle", "9.4.2", "aspnet-core",
                "artifacts", "aqgreen-b6-authoritative", "stale-state"), "must not be selected");
            Run(repository, "git", "status", "--porcelain=v1", "--untracked-files=all").ShouldBeEmpty();

            var result = RunBootstrap(repository, reviewedCommit, new Dictionary<string, string>
            {
                ["B6_TEST_SENSITIVE_DSN_SENTINEL"] = sentinel,
                ["B6_TEST_UNVERIFIED_MARKER"] = unverifiedMarker,
                ["B6_TEST_VERIFIED_MARKER"] = verifiedMarker
            }, "--seed-nuget-packages-from", configuredPackageSource, "runner", "sensitive-probe");

            result.ShouldContain("B6 pre-authority provenance: PASS");
            result.ShouldContain("B6_VERIFIED_SENSITIVE_CAPABILITY_VISIBLE");
            result.ShouldContain("/source-a/");
            result.ShouldNotContain(ordinaryRunner);
            result.ShouldContain("Runner SHA-256:");
            result.ShouldContain("Core SHA-256:");
            result.ShouldContain("EntityFrameworkCore SHA-256:");
            result.ShouldContain("B1-R_a=PASS");
            result.ShouldContain("B1-R_b=PASS");
            result.ShouldContain("B1-R_DETERMINISM=PASS");
            result.ShouldContain("B2_a=PASS");
            result.ShouldContain("B2_b=PASS");
            File.Exists(unverifiedMarker).ShouldBeFalse();
            File.Exists(buildLeakMarker).ShouldBeFalse();
            File.Exists(checkoutHookMarker).ShouldBeFalse();
            File.Exists(checkoutFilterMarker).ShouldBeFalse();
            File.ReadAllText(verifiedMarker).ShouldBe(sentinel);
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void OperationalBootstrap_SeedsPrivateNuGetPackagesWithoutMutatingOrTrustingUnsafeInput()
    {
        if (OperatingSystem.IsWindows()) return;
        var testRoot = Path.Combine(Path.GetTempPath(), "b6-package-state-test-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(testRoot, "repository");
        var acquisitionState = Path.Combine(testRoot, "acquired-packages");
        var packageState = Path.Combine(testRoot, "reconciled-packages");
        var destinationMarker = Path.Combine(testRoot, "package-destination");
        var destinationSnapshot = Path.Combine(testRoot, "package-destination-snapshot");
        var sourceMarker = Path.Combine(testRoot, "package-source-destination");
        var sourceSnapshot = Path.Combine(testRoot, "package-source-snapshot");
        var restoreSourcesMarker = Path.Combine(testRoot, "restore-sources");
        var selectedPackages = new[]
        {
            (Id: "synthetic.package", Version: "1.0.0"),
            (Id: "synthetic.b", Version: "2.0.0")
        };
        var residuePackages = new[]
        {
            (Id: "synthetic.b", Version: "1.0.0"),
            (Id: "synthetic.c", Version: "3.0.0")
        };
        Directory.CreateDirectory(repository);
        Directory.CreateDirectory(acquisitionState);
        Directory.CreateDirectory(packageState);
        try
        {
            foreach (var package in selectedPackages.Concat(residuePackages))
                WriteSyntheticNuGetPackage(acquisitionState, package.Id, package.Version);
            foreach (var package in selectedPackages)
                CopyDirectory(Path.Combine(acquisitionState, package.Id, package.Version),
                    Path.Combine(packageState, package.Id, package.Version));

            var runnerProject = CreateSourceLinkFixture(repository, packageStateMarker: destinationMarker,
                packageStateSnapshot: destinationSnapshot, packageSourceMarker: sourceMarker,
                packageSourceSnapshot: sourceSnapshot, restoreSourcesMarker: restoreSourcesMarker,
                packageReferences: selectedPackages, lockGenerationSource: packageState);
            Run(repository, "git", "init");
            Run(repository, "git", "add", ".");
            Run(repository, "git", "-c", "user.name=B6 Test", "-c", "user.email=b6@example.test",
                "commit", "-m", "reviewed source");
            var reviewedCommit = Run(repository, "git", "rev-parse", "HEAD").Trim();
            var seedFiles = Directory.EnumerateFiles(packageState, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(packageState, path), File.ReadAllBytes);
            var seedEntries = Directory.EnumerateFileSystemEntries(packageState, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(packageState, path)).Order().ToArray();

            var result = RunBootstrap(repository, reviewedCommit, new Dictionary<string, string>(),
                "--seed-nuget-packages-from", packageState, "runner", "sensitive-probe");

            PackageVersions(acquisitionState).ShouldBe(selectedPackages.Concat(residuePackages).Order().ToArray());
            PackageVersions(packageState).ShouldBe(selectedPackages.Order().ToArray());
            result.ShouldContain("B6 selected NuGet seed validated: packages=2 archive-manifest-sha256=");
            result.ShouldContain(
                "B6 private local NuGet source derived from selected seed: 2 complete package versions, exact archive bytes, configured sources overridden.");
            result.ShouldContain("B1-R_a=PASS packages=2 warnings=0 errors=0 NU1603=0 lock-mutation=NO assets=3");
            result.ShouldContain("B1-R_b=PASS packages=2 warnings=0 errors=0 NU1603=0 lock-mutation=NO assets=3");
            result.ShouldContain("B1-R_DETERMINISM=PASS");
            result.ShouldContain("B2_a=PASS no-restore=true lock-mutation=NO assets-mutation=NO packages=2");
            result.ShouldContain("B2_b=PASS no-restore=true lock-mutation=NO assets-mutation=NO packages=2");
            result.ShouldContain("B6 selected NuGet seed and private source remained unchanged through B1-R/B2.");
            foreach (var (relativePath, bytes) in seedFiles)
            {
                File.ReadAllBytes(Path.Combine(packageState, relativePath)).ShouldBe(bytes);
                File.ReadAllBytes(Path.Combine(sourceSnapshot, relativePath)).ShouldBe(bytes);
                var restoredPath = Path.Combine(destinationSnapshot, relativePath);
                if (Path.GetFileName(relativePath) == ".nupkg.metadata")
                {
                    using var expectedMetadata = JsonDocument.Parse(bytes);
                    using var restoredMetadata = JsonDocument.Parse(File.ReadAllBytes(restoredPath));
                    restoredMetadata.RootElement.GetProperty("contentHash").GetString().ShouldBe(
                        expectedMetadata.RootElement.GetProperty("contentHash").GetString());
                    restoredMetadata.RootElement.GetProperty("version").GetInt32().ShouldBe(2);
                    restoredMetadata.RootElement.GetProperty("source").GetString().ShouldEndWith("/package-source");
                }
                else
                {
                    File.ReadAllBytes(restoredPath).ShouldBe(bytes);
                }
            }
            Directory.EnumerateFileSystemEntries(packageState, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(packageState, path)).Order().ShouldBe(seedEntries);
            var destination = File.ReadAllText(destinationMarker).Trim().Split(':', 3);
            destination[0].ShouldBe(Run(testRoot, "id", "-u").Trim());
            destination[1].ShouldBe("700");
            var destinationPath = destination[2].TrimEnd(Path.DirectorySeparatorChar);
            destinationPath.ShouldNotBe(packageState);
            destinationPath.ShouldStartWith(Path.Combine(Path.GetTempPath(), "aqua-b6-preauthority."));
            destinationPath.ShouldEndWith("/packages-b");

            var source = File.ReadAllText(sourceMarker).Trim().Split(':', 3);
            source[0].ShouldBe(Run(testRoot, "id", "-u").Trim());
            source[1].ShouldBe("500");
            var sourcePath = source[2].TrimEnd(Path.DirectorySeparatorChar);
            sourcePath.ShouldNotBe(packageState);
            sourcePath.ShouldNotBe(destinationPath);
            sourcePath.ShouldStartWith(Path.Combine(Path.GetTempPath(), "aqua-b6-preauthority."));
            sourcePath.ShouldEndWith("/package-source");
            File.ReadAllText(restoreSourcesMarker).Trim().ShouldBe(sourcePath);
            File.ReadAllText(restoreSourcesMarker).ShouldNotContain("127.0.0.1:1");

            var sourceFiles = Directory.EnumerateFiles(sourceSnapshot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(sourceSnapshot, path)).Order().ToArray();
            sourceFiles.ShouldBe(seedFiles.Keys.Order().ToArray());
            Directory.EnumerateFileSystemEntries(sourceSnapshot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(sourceSnapshot, path)).Order().ShouldBe(seedEntries);
            File.Exists(Path.Combine(sourceSnapshot, "synthetic.package", "1.0.0",
                "synthetic.package.1.0.0.nupkg")).ShouldBeTrue();
            foreach (var package in selectedPackages)
            {
                var relativeArchive = $"{package.Id}/{package.Version}/{package.Id}.{package.Version}.nupkg";
                File.ReadAllBytes(Path.Combine(sourceSnapshot, relativeArchive))
                    .ShouldBe(File.ReadAllBytes(Path.Combine(packageState, relativeArchive)));
            }
            Directory.Exists(Path.Combine(packageState, "synthetic.b", "1.0.0")).ShouldBeFalse();
            Directory.Exists(Path.Combine(packageState, "synthetic.c", "3.0.0")).ShouldBeFalse();
            Directory.Exists(Path.Combine(sourceSnapshot, "synthetic.b", "1.0.0")).ShouldBeFalse();
            Directory.Exists(Path.Combine(sourceSnapshot, "synthetic.c", "3.0.0")).ShouldBeFalse();

            var isolatedPackages = Path.Combine(testRoot, "isolated-restore-packages");
            var isolatedArtifacts = Path.Combine(testRoot, "isolated-restore-artifacts");
            Directory.CreateDirectory(isolatedPackages);
            var offlineRestore = RunCaptured(repository, "dotnet", new Dictionary<string, string>(),
                "restore", runnerProject,
                "--configfile", Path.Combine(repository, "AqualLifeStyle", "9.4.2", "aspnet-core", "NuGet.Config"),
                "--packages", isolatedPackages, "--locked-mode",
                "--disable-build-servers", "--source", sourceSnapshot,
                $"-p:ArtifactsPath={isolatedArtifacts}", "-p:ImportDirectoryBuildProps=false",
                "-p:ImportDirectoryBuildTargets=false");
            if (offlineRestore.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Offline restore failed: {offlineRestore.StandardOutput}{offlineRestore.StandardError}");
            PackageVersions(isolatedPackages).ShouldBe(selectedPackages.Order().ToArray());

            var missing = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", Path.Combine(testRoot, "missing"), "runner", "sensitive-probe");
            missing.ExitCode.ShouldNotBe(0);
            missing.StandardError.ShouldContain("existing absolute directory");

            var relative = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", "reconciled-packages", "runner", "sensitive-probe");
            relative.ExitCode.ShouldNotBe(0);
            relative.StandardError.ShouldContain("existing absolute directory");

            var regularFileSeed = Path.Combine(testRoot, "regular-file-seed");
            File.WriteAllBytes(regularFileSeed, "not-a-directory"u8.ToArray());
            var regularFile = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", regularFileSeed, "runner", "sensitive-probe");
            regularFile.ExitCode.ShouldNotBe(0);
            regularFile.StandardError.ShouldContain("existing absolute directory");

            var emptyState = Path.Combine(testRoot, "empty-packages");
            Directory.CreateDirectory(emptyState);
            var empty = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", emptyState, "runner", "sensitive-probe");
            empty.ExitCode.ShouldNotBe(0);
            empty.StandardError.ShouldContain("contains no package versions");

            var incompleteState = Path.Combine(testRoot, "incomplete-packages");
            CopyDirectory(Path.Combine(packageState, "synthetic.package", "1.0.0"),
                Path.Combine(incompleteState, "synthetic.package", "1.0.0"));
            File.Delete(Path.Combine(incompleteState, "synthetic.package", "1.0.0", "synthetic.package.nuspec"));
            var incomplete = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", incompleteState, "runner", "sensitive-probe");
            incomplete.ExitCode.ShouldNotBe(0);
            incomplete.StandardError.ShouldContain("package directory is incomplete");

            var unsafePackageIdState = Path.Combine(testRoot, "unsafe-package-id");
            CopyDirectory(Path.Combine(packageState, "synthetic.package", "1.0.0"),
                Path.Combine(unsafePackageIdState, "Unsafe Package", "1.0.0"));
            var unsafePackageId = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", unsafePackageIdState, "runner", "sensitive-probe");
            unsafePackageId.ExitCode.ShouldNotBe(0);
            unsafePackageId.StandardError.ShouldContain("unsafe or non-canonical package ID");

            var nonCanonicalVersionState = Path.Combine(testRoot, "non-canonical-version");
            CopyDirectory(Path.Combine(packageState, "synthetic.package", "1.0.0"),
                Path.Combine(nonCanonicalVersionState, "synthetic.package", "1.00.0"));
            var nonCanonicalVersion = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", nonCanonicalVersionState, "runner", "sensitive-probe");
            nonCanonicalVersion.ExitCode.ShouldNotBe(0);
            nonCanonicalVersion.StandardError.ShouldContain("unsafe or non-canonical package version");

            var linkedState = Path.Combine(testRoot, "linked-packages");
            Directory.CreateSymbolicLink(linkedState, packageState);
            var linked = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", linkedState, "runner", "sensitive-probe");
            linked.ExitCode.ShouldNotBe(0);
            linked.StandardError.ShouldContain("existing absolute directory");

            var unsafeState = Path.Combine(testRoot, "unsafe-packages");
            Directory.CreateDirectory(unsafeState);
            File.CreateSymbolicLink(Path.Combine(unsafeState, "package-link"), regularFileSeed);
            var unsafeResult = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", unsafeState, "runner", "sensitive-probe");
            unsafeResult.ExitCode.ShouldNotBe(0);
            unsafeResult.StandardError.ShouldContain("must not contain symbolic links");

            var specialState = Path.Combine(testRoot, "special-packages");
            Directory.CreateDirectory(specialState);
            Run(testRoot, "mkfifo", Path.Combine(specialState, "package-pipe"));
            var specialResult = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", specialState, "runner", "sensitive-probe");
            specialResult.ExitCode.ShouldNotBe(0);
            specialResult.StandardError.ShouldContain("only directories and regular files");

            var nonCanonicalState = Path.Combine(packageState, "..", Path.GetFileName(packageState));
            var nonCanonical = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", nonCanonicalState, "runner", "sensitive-probe");
            nonCanonical.ExitCode.ShouldNotBe(0);
            nonCanonical.StandardError.ShouldContain("must already be canonical");

            var overlapping = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", repository, "runner", "sensitive-probe");
            overlapping.ExitCode.ShouldNotBe(0);
            overlapping.StandardError.ShouldContain("must not overlap the live repository");

            var duplicated = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from", packageState, "--seed-nuget-packages-from", packageState,
                "runner", "sensitive-probe");
            duplicated.ExitCode.ShouldNotBe(0);
            duplicated.StandardError.ShouldContain("may be supplied at most once");

            var missingValue = RunBootstrapCaptured(repository, reviewedCommit,
                "--seed-nuget-packages-from");
            missingValue.ExitCode.ShouldNotBe(0);
            missingValue.StandardError.ShouldContain("requires a directory value and an operation");

            var misplaced = RunBootstrapCaptured(repository, reviewedCommit,
                "runner", "sensitive-probe", "--seed-nuget-packages-from", packageState);
            misplaced.ExitCode.ShouldNotBe(0);
            misplaced.StandardError.ShouldContain("must immediately follow the reviewed commit");

            var runnerTemp = Path.Combine(packageState, "runner-temp");
            Directory.CreateDirectory(runnerTemp);
            var privateOverlap = RunBootstrapCaptured(repository, reviewedCommit,
                new Dictionary<string, string> { ["TMPDIR"] = runnerTemp },
                "--seed-nuget-packages-from", packageState, "runner", "sensitive-probe");
            privateOverlap.ExitCode.ShouldNotBe(0);
            privateOverlap.StandardError.ShouldContain("must not contain the runner temporary root");
            Directory.Delete(runnerTemp);

            var coreLock = Path.Combine(repository, "AqualLifeStyle", "9.4.2", "aspnet-core", "src",
                "AqualLifeStyle.Core", "packages.lock.json");
            Run(repository, "git", "rm", Path.GetRelativePath(repository, coreLock));
            Run(repository, "git", "-c", "user.name=B6 Test", "-c", "user.email=b6@example.test",
                "commit", "-m", "remove required lock");
            var missingLockCommit = Run(repository, "git", "rev-parse", "HEAD").Trim();
            var missingLock = RunBootstrapCaptured(repository, missingLockCommit,
                "--seed-nuget-packages-from", packageState, "runner", "sensitive-probe");
            missingLock.ExitCode.ShouldNotBe(0);
            missingLock.StandardError.ShouldContain("Reviewed B6 build/operation input must be a regular file");
            missingLock.StandardError.ShouldContain("packages.lock.json");
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Run(testRoot, "chmod", "-R", "u+w", testRoot);
                Directory.Delete(testRoot, recursive: true);
            }
        }
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
            $"{{\"version\":2,\"contentHash\":\"{contentHash}\",\"source\":\"B6 synthetic acquisition\"}}");
        File.WriteAllBytes(Path.Combine(payloadDirectory, "_._"), []);
    }

    private static void WriteZipEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
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

    private static string CreateSourceLinkFixture(string repository, string? buildLeakMarker = null,
        string? packageStateMarker = null, string? packageStateSnapshot = null,
        string? packageSourceMarker = null, string? packageSourceSnapshot = null,
        string? restoreSourcesMarker = null,
        IReadOnlyCollection<(string Id, string Version)>? packageReferences = null,
        string? configuredNugetSource = null, string? lockGenerationSource = null)
    {
        var backend = Path.Combine(repository, "AqualLifeStyle", "9.4.2", "aspnet-core");
        var runnerProject = Path.Combine(backend, "tools", "AqualLifeStyle.B6SnapshotRunner");
        var coreProject = Path.Combine(backend, "src", "AqualLifeStyle.Core");
        var efProject = Path.Combine(backend, "src", "AqualLifeStyle.EntityFrameworkCore");
        var package = Path.Combine(repository, "docs", "operations", "aqgreen-v2-b6", "snapshot-package");
        Directory.CreateDirectory(runnerProject);
        Directory.CreateDirectory(coreProject);
        Directory.CreateDirectory(efProject);
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(backend, ".gitignore"), "[Bb]in/\n[Oo]bj/\n[Rr]elease/\nartifacts/\n");
        configuredNugetSource ??= packageReferences is null ? null : "https://127.0.0.1:1/v3/index.json";
        File.WriteAllText(Path.Combine(backend, "NuGet.Config"), configuredNugetSource is null
            ? "<configuration><packageSources><clear /></packageSources></configuration>\n"
            : $"<configuration><packageSources><clear /><add key=\"configured\" " +
              $"value=\"{configuredNugetSource}\" /></packageSources></configuration>\n");
        File.Copy(CanonicalB6EntrypointFromTestSource(), Path.Combine(package, "authoritative-runner.sh"));
        File.Copy(Path.Combine(TestSourcePackageDirectory(), "operator-extract.sh"),
            Path.Combine(package, "operator-extract.sh"));
        File.Copy(Path.Combine(TestSourcePackageDirectory(), "run-analysis.sh"),
            Path.Combine(package, "run-analysis.sh"));
        File.WriteAllText(Path.Combine(coreProject, "AqualLifeStyle.Core.csproj"),
            MinimalProject("AqualLifeStyle.Core"));
        File.WriteAllText(Path.Combine(coreProject, "Marker.cs"),
            "namespace FixtureCore; public sealed class Marker { }\n");
        File.WriteAllText(Path.Combine(efProject, "AqualLifeStyle.EntityFrameworkCore.csproj"),
            MinimalProject("AqualLifeStyle.EntityFrameworkCore"));
        File.WriteAllText(Path.Combine(efProject, "Marker.cs"),
            "namespace FixtureEf; public sealed class Marker { }\n");
        var buildLeakTarget = buildLeakMarker == null
            ? string.Empty
            : $"<Target Name=\"DetectSensitiveBuildEnvironment\" BeforeTargets=\"CoreCompile\" " +
              $"Condition=\"'$(B6_TEST_SENSITIVE_DSN_SENTINEL)' != ''\"><WriteLinesToFile " +
              $"File=\"{buildLeakMarker}\" Lines=\"$(B6_TEST_SENSITIVE_DSN_SENTINEL)\" Overwrite=\"true\" /></Target>";
        var packageStateTarget = packageStateMarker == null
            ? string.Empty
            : $"<Target Name=\"RecordPackageState\" BeforeTargets=\"CoreCompile\"><Exec " +
              $"Command=\"stat --format='%u:%a:%n' &quot;$(NuGetPackageRoot)&quot; " +
              $"&gt; &quot;{packageStateMarker}&quot;\" />" +
              $"<MakeDir Directories=\"{packageStateSnapshot}\" />" +
              $"<Exec Command=\"cp -R --no-dereference -- &quot;$(NuGetPackageRoot)/.&quot; " +
              $"&quot;{packageStateSnapshot}/&quot;\" /></Target>";
        var restoreSourcesTarget = restoreSourcesMarker == null
            ? string.Empty
            : $"<Target Name=\"RecordRestoreSources\" BeforeTargets=\"_GenerateRestoreGraphProjectEntry\" " +
              $"Condition=\"!Exists('{restoreSourcesMarker}')\"><WriteLinesToFile " +
              $"File=\"{restoreSourcesMarker}\" Lines=\"$(RestoreSources)\" Overwrite=\"true\" /></Target>";
        var packageSourceTarget = packageSourceMarker == null
            ? string.Empty
            : $"<Target Name=\"RecordPackageSource\" BeforeTargets=\"_GenerateRestoreGraphProjectEntry\" " +
              $"Condition=\"!Exists('{packageSourceMarker}')\">" +
              $"<Exec Command=\"stat --format='%u:%a:%n' &quot;$(RestoreSources)&quot; " +
              $"&gt; &quot;{packageSourceMarker}&quot;\" />" +
              $"<MakeDir Directories=\"{packageSourceSnapshot}\" />" +
              $"<Exec Command=\"cp -R --no-dereference -- &quot;$(RestoreSources)/.&quot; " +
              $"&quot;{packageSourceSnapshot}/&quot;\" /></Target>";
        var packageReferenceItems = packageReferences is null
            ? string.Empty
            : "<ItemGroup>" + string.Concat(packageReferences.Select(package =>
                $"<PackageReference Include=\"{package.Id}\" Version=\"{package.Version}\" />")) + "</ItemGroup>";
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
            """ + packageReferenceItems + buildLeakTarget + packageStateTarget + restoreSourcesTarget +
            packageSourceTarget + "\n</Project>\n");
        File.WriteAllText(Path.Combine(runnerProject, "Program.cs"),
            """
            var sentinel = System.Environment.GetEnvironmentVariable("B6_TEST_SENSITIVE_DSN_SENTINEL");
            var verifiedMarker = System.Environment.GetEnvironmentVariable("B6_TEST_VERIFIED_MARKER");
            if (sentinel != null && verifiedMarker != null) System.IO.File.WriteAllText(verifiedMarker, sentinel);
            System.Console.WriteLine(sentinel == "B6-SYNTHETIC-SENSITIVE-CAPABILITY"
                ? "B6_VERIFIED_SENSITIVE_CAPABILITY_VISIBLE"
                : "B6_SENSITIVE_CAPABILITY_ABSENT");
            System.Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.Console.WriteLine(typeof(FixtureCore.Marker).FullName + typeof(FixtureEf.Marker).FullName);
            """);

        if (packageReferences is not null)
        {
            lockGenerationSource.ShouldNotBeNullOrWhiteSpace();
            var lockCache = Path.Combine(Path.GetTempPath(), "b6-lock-generation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(lockCache);
            try
            {
                RunWithEnvironment(repository, "dotnet", new Dictionary<string, string>
                {
                    ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                    ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true",
                    ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                    ["DOTNET_NOLOGO"] = "1",
                    ["MSBUILDDISABLENODEREUSE"] = "1"
                }, "restore", Path.Combine(runnerProject, "AqualLifeStyle.B6SnapshotRunner.csproj"),
                    "--use-lock-file", "--force-evaluate", "--source", lockGenerationSource!,
                    "--packages", lockCache, "--disable-build-servers");
            }
            finally
            {
                if (Directory.Exists(lockCache)) Directory.Delete(lockCache, recursive: true);
            }
            foreach (var generatedMarker in new[] { packageSourceMarker, restoreSourcesMarker })
                if (generatedMarker is not null && File.Exists(generatedMarker)) File.Delete(generatedMarker);
            if (packageSourceSnapshot is not null && Directory.Exists(packageSourceSnapshot))
                Directory.Delete(packageSourceSnapshot, recursive: true);

            var coreLock = Path.Combine(coreProject, "packages.lock.json");
            var efLock = Path.Combine(efProject, "packages.lock.json");
            var runnerLock = Path.Combine(runnerProject, "packages.lock.json");
            File.Exists(coreLock).ShouldBeTrue();
            File.Exists(efLock).ShouldBeTrue();
            File.Exists(runnerLock).ShouldBeTrue();
            var canonicalFixtureGraph = string.Concat(packageReferences
                .Select(package => $"{package.Id.ToLowerInvariant()}\t{package.Version}\n")
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            var bootstrapPath = Path.Combine(package, "authoritative-runner.sh");
            var bootstrap = File.ReadAllText(bootstrapPath)
                .Replace("expected_selected_package_count='197'",
                    $"expected_selected_package_count='{packageReferences.Count}'", StringComparison.Ordinal)
                .Replace("a32d7081f7c687c9ece1511122dd772ac17e7cfc682943e5706ac0caef6fc4a0",
                    B6Values.Sha256Hex(canonicalFixtureGraph), StringComparison.Ordinal)
                .Replace("9f467700282dc48f8bdc172eb8f1830a3167cbc4efaa223a328b1ff00c4acddc",
                    B6Values.Sha256Hex(File.ReadAllBytes(coreLock)), StringComparison.Ordinal)
                .Replace("2b1925d618a39d10fa57e3e8455f06c3565de75ccdc3ae4a9021e357c9d07356",
                    B6Values.Sha256Hex(File.ReadAllBytes(efLock)), StringComparison.Ordinal)
                .Replace("401fafbcfa35347666a4de3fcfa92be9c66399663d8f8410eca420879c3d7b7b",
                    B6Values.Sha256Hex(File.ReadAllBytes(runnerLock)), StringComparison.Ordinal);
            File.WriteAllText(bootstrapPath, bootstrap);
        }
        return Path.Combine(runnerProject, "AqualLifeStyle.B6SnapshotRunner.csproj");
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
