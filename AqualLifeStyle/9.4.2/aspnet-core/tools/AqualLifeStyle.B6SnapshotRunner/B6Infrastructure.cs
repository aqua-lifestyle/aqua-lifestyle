using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;

namespace AqualLifeStyle.B6SnapshotRunner;

public static class B6Contract
{
    public const string PackageVersion = "AQGreenB6SnapshotPackageV3";
    public const string PseudonymVersion = "HmacSha256NamespacedFatalCollisionV3";
    public const string SanitisationVersion = "AQGreenB6SanitisationV3";
    public const string OrderingVersion = "DotNetGuidCompareToOrdinalV3";
    public const string InventoryVersion = "AQGreenLegacyInventoryV2";
    public const string InventorySemanticVersion = "AQGreenLegacyInventorySemanticsV1";
    public const string SimulatorVersion = "AQGreenLegacyCanonicalisationSimulationV2";
    public const string SimulatorComparatorVersion = "LegacyOrderingTieBreakOrdinalV1";
    public const string EncodingVersion = "B6CanonicalLengthPrefixedUtf8V1";
    public const string AnalysisVersion = "AQGreenB6RepresentativeAnalysisV3";
    public const string RootReviewVersion = "AQGreenB6RootReviewDispositionV1";
    public const string ImpactVersion = "AQGreenB6ImpactAnalysisV3";
    public const string RuntimeVersion = "AQGreenB6OwnedPostgreSqlRuntimeV2";
    public const string ExpectedMigrationHead = "20260831230903_AddAQGreenV2CommissionConsumption";
    public const string PopulationScope = "ALL_TENANT_AQGREEN_PARTICIPATIONS";
    public const string Classification = "PSEUDONYMISED_AND_DATA_MINIMISED";
    public const string ApprovedDestination = "PACKAGE_OWNED_ISOLATED_POSTGRESQL_16";
    public const string WorkerBoundary = "POSTGRES_ONLY_CONTAINER_NO_AQUA_APPLICATION_ATTACHED_BY_PACKAGE";
    public const string ExpectedAllowlistDigest = "cd3361f174bb5b9ab20736c79328110fd4246ce965e51acc59e195f2bc9e514a";
    public const string Null = @"\N";
    public const string EvidenceClassificationEnvironment = "B6_EXECUTION_EVIDENCE_CLASSIFICATION";
    public const string PrecommitEvidence = "PRECOMMIT_SYNTHETIC_VALIDATION";
    public const string OperationalEvidence = "OPERATIONAL_EVIDENCE";

    public static readonly Regex ControlledCode = new("^[A-Za-z0-9._:+/@-]{1,128}$", RegexOptions.CultureInvariant);
    public static readonly Regex MigrationCode = new("^[0-9]{14}_[A-Za-z0-9_]{1,180}$", RegexOptions.CultureInvariant);
    public static readonly Regex ProductVersion = new("^[0-9]+(?:\\.[0-9]+){1,3}(?:[-+][A-Za-z0-9.-]+)?$", RegexOptions.CultureInvariant);
    public static readonly Regex SnapshotId = new("^[A-Za-z0-9._-]{8,128}$", RegexOptions.CultureInvariant);
    public static readonly Regex Sha256 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    public static readonly Regex GitSha = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);
}

public static class B6Values
{
    public static DateTimeOffset ParseTimestamp(string value)
    {
        if (value == B6Contract.Null) throw new B6PackageException("A required timestamp is null.");
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed) ||
            !Regex.IsMatch(value, "(?:Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant))
            throw new B6PackageException($"Invalid offset-bearing timestamp: {value}.");
        return parsed.ToUniversalTime();
    }

    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);

    public static string Timestamp(DateTime value) =>
        Timestamp(value.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(value)
            : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    public static void RequireCutoffNotAfterObservation(string cutoff, string observedAt)
    {
        if (ParseTimestamp(cutoff) > ParseTimestamp(observedAt))
            throw new B6PackageException("AnalysisSnapshotCutoff cannot be later than source observation.");
    }

    public static string Sha256Hex(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    public static string Sha256Hex(string value) => Sha256Hex(Encoding.UTF8.GetBytes(value));

    public static string CanonicalScalar(string value, string type, string transform)
    {
        var nullable = type.EndsWith("?", StringComparison.Ordinal);
        var baseType = type.TrimEnd('?');
        if (value == B6Contract.Null)
        {
            if (!nullable) throw new B6PackageException($"Required {baseType} value is null.");
            return value;
        }
        switch (baseType)
        {
            case "int":
            case "long":
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    throw new B6PackageException($"Invalid integer {value}.");
                if (transform == "legacy-participation-order-ordinal" && integer <= 0)
                    throw new B6PackageException("Ordering ordinal must be positive.");
                value = integer.ToString(CultureInfo.InvariantCulture);
                break;
            case "bool":
                value = value.ToLowerInvariant() switch
                {
                    "t" or "true" or "1" => "true",
                    "f" or "false" or "0" => "false",
                    _ => throw new B6PackageException($"Invalid boolean {value}.")
                };
                break;
            case "uuid":
                if (!Guid.TryParse(value, out var guid)) throw new B6PackageException($"Invalid UUID {value}.");
                value = guid.ToString("D");
                break;
            case "timestamp":
                value = Timestamp(ParseTimestamp(value));
                break;
            case "string":
                break;
            default:
                throw new B6PackageException($"Unknown allowlist field type {type}.");
        }
        if (transform == "controlled-code" && !B6Contract.ControlledCode.IsMatch(value))
            throw new B6PackageException($"Uncontrolled version/code value {value}.");
        if (transform == "programme-code" && !Regex.IsMatch(value, "^[A-Z0-9_-]{1,32}$"))
            throw new B6PackageException($"Invalid programme code {value}.");
        if (transform == "timezone-code" && !Regex.IsMatch(value, "^[A-Za-z0-9_+./-]{1,64}$"))
            throw new B6PackageException($"Invalid time-zone code {value}.");
        if (transform == "canonical-path" && !Regex.IsMatch(value, "^[1-5]*$"))
            throw new B6PackageException($"Invalid canonical path {value}.");
        if (transform == "migration-code" && !B6Contract.MigrationCode.IsMatch(value))
            throw new B6PackageException($"Invalid EF migration identity {value}.");
        if (transform == "product-version" && !B6Contract.ProductVersion.IsMatch(value))
            throw new B6PackageException($"Invalid EF product version {value}.");
        return value;
    }

    public static string SqlType(string type) => type.TrimEnd('?') switch
    {
        "int" => "integer", "long" => "bigint", "bool" => "boolean", "uuid" => "uuid",
        "timestamp" => "timestamp with time zone", "string" => "text",
        _ => throw new B6PackageException($"Unsupported restored SQL type {type}.")
    };

    public static string QuoteIdentifier(string value)
    {
        if (!Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new B6PackageException($"Unsafe packaged identifier {value}.");
        return $"\"{value}\"";
    }
}

public static class CanonicalJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    public static JsonNode Read(string path, bool controlled = true)
    {
        using var stream = SecureFiles.OpenRead(path, controlled);
        return JsonNode.Parse(stream) ?? throw new B6PackageException($"JSON document is empty: {path}.");
    }

    public static T Read<T>(string path, bool controlled = true) where T : class
    {
        using var stream = SecureFiles.OpenRead(path, controlled);
        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new B6PackageException($"JSON document is empty: {path}.");
    }

    public static JsonNode ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, Options)!;

    public static byte[] Bytes(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            WriteSorted(writer, node);
        return stream.ToArray();
    }

    public static string Digest(JsonNode node) => B6Values.Sha256Hex(Bytes(node));

    public static void WriteNew(string path, JsonNode node) =>
        SecureFiles.WriteNew(path, stream =>
        {
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            WriteSorted(writer, node);
            writer.Flush();
            stream.WriteByte((byte)'\n');
        });

    private static void WriteSorted(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var item in obj.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(item.Key);
                    WriteSorted(writer, item.Value);
                }
                writer.WriteEndObject();
                return;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array) WriteSorted(writer, item);
                writer.WriteEndArray();
                return;
            case JsonValue value:
                value.WriteTo(writer);
                return;
            default:
                throw new B6PackageException("Unsupported JSON node.");
        }
    }
}

public static class B6Paths
{
    public static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
            current = current.Parent;
        }
        throw new B6PackageException("B6 tool is not executing inside its repository worktree.");
    }

    public static string PackageRoot() => Path.Combine(RepositoryRoot(),
        "docs", "operations", "aqgreen-v2-b6", "snapshot-package");

    public static string InventorySql() => Path.Combine(RepositoryRoot(),
        "docs", "operations", "aqgreen-v2-b6", "legacy-inventory.sql");
}

public static class SecureFiles
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void CreatePrivateDirectory(string path)
    {
        path = Path.GetFullPath(path);
        if (File.Exists(path) || Directory.Exists(path) || IsLink(path))
            throw new B6PackageException($"Output directory must not already exist: {path}.");
        RequireSafeAncestors(Path.GetDirectoryName(path)!);
        RequireTrustedDirectory(Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, OwnerDirectory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, OwnerDirectory);
        RequireTrustedDirectory(path);
    }

    public static void RequireTrustedDirectory(string path)
    {
        path = Path.GetFullPath(path);
        RequireSafeAncestors(path);
        if (!Directory.Exists(path) || IsLink(path))
            throw new B6PackageException($"Path must be a real directory: {path}.");
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & ~OwnerDirectory) != 0)
            throw new B6PackageException($"Controlled directory must have mode 0700 or stricter: {path}.");
    }

    public static FileStream OpenRead(string path, bool controlled)
    {
        path = Path.GetFullPath(path);
        RequireSafeAncestors(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || Directory.Exists(path) || IsLink(path))
            throw new B6PackageException($"Path must be a regular non-symlink file: {path}.");
        if (controlled)
        {
            RequireTrustedDirectory(Path.GetDirectoryName(path)!);
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & ~OwnerFile) != 0)
                throw new B6PackageException($"Controlled file must have mode 0600 or stricter: {path}.");
        }
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.SequentialScan);
    }

    public static void WriteNew(string path, Action<FileStream> writer)
    {
        path = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(path)!;
        RequireTrustedDirectory(parent);
        if (File.Exists(path) || Directory.Exists(path) || IsLink(path))
            throw new B6PackageException($"Output file already exists or is a link: {path}.");
        var temporary = Path.Combine(parent, $".b6-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, OwnerFile);
                writer(stream);
                stream.Flush(true);
            }
            File.Move(temporary, path, false);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, OwnerFile);
        }
        catch
        {
            if (File.Exists(temporary) && !IsLink(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public static void DeleteControlledFile(string path)
    {
        using (OpenRead(path, controlled: true)) { }
        File.Delete(path);
    }

    public static void RequireSafeAncestors(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current != null)
        {
            if (IsLink(current.FullName))
                throw new B6PackageException($"Symlink/reparse-point ancestor is prohibited: {current.FullName}.");
            current = current.Parent;
        }
    }

    public static bool IsLink(string path)
    {
        try
        {
            var info = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
            if (info.LinkTarget != null) return true;
            if (!File.Exists(path) && !Directory.Exists(path)) return false;
            return (info.Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}

public static class CsvContract
{
    public static List<Dictionary<string, string>> Read(string path, IReadOnlyList<string> expectedHeader, bool controlled = true)
    {
        using var stream = SecureFiles.OpenRead(path, controlled);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 4096, leaveOpen: false);
        var records = Parse(reader.ReadToEnd());
        if (records.Count == 0 || !records[0].SequenceEqual(expectedHeader, StringComparer.Ordinal))
            throw new B6PackageException($"Unexpected columns in {Path.GetFileName(path)}.");
        var rows = new List<Dictionary<string, string>>();
        for (var index = 1; index < records.Count; index++)
        {
            if (records[index].Count != expectedHeader.Count)
                throw new B6PackageException($"Malformed CSV row {index + 1} in {Path.GetFileName(path)}.");
            rows.Add(expectedHeader.Select((name, column) => (name, value: records[index][column]))
                .ToDictionary(item => item.name, item => item.value, StringComparer.Ordinal));
        }
        return rows;
    }

    public static void WriteNew(string path, IReadOnlyList<string> header,
        IEnumerable<IReadOnlyDictionary<string, string>> rows) =>
        SecureFiles.WriteNew(path, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
            WriteRecord(writer, header);
            foreach (var row in rows) WriteRecord(writer, header.Select(name => row[name]));
            writer.Flush();
        });

    private static void WriteRecord(TextWriter writer, IEnumerable<string> values)
    {
        writer.WriteLine(string.Join(',', values.Select(Escape)));
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static List<List<string>> Parse(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var value = text[index];
            if (quoted)
            {
                if (value == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"'); index++;
                }
                else if (value == '"') quoted = false;
                else field.Append(value);
                continue;
            }
            if (value == '"' && field.Length == 0) quoted = true;
            else if (value == ',') { record.Add(field.ToString()); field.Clear(); }
            else if (value == '\n')
            {
                if (field.Length > 0 && field[^1] == '\r') field.Length--;
                record.Add(field.ToString()); field.Clear(); records.Add(record); record = new List<string>();
            }
            else field.Append(value);
        }
        if (quoted) throw new B6PackageException("Unterminated quoted CSV field.");
        if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
        if (records.Count > 0 && records[^1].Count == 1 && records[^1][0].Length == 0) records.RemoveAt(records.Count - 1);
        return records;
    }
}

public sealed record FirstPartyAssemblyIdentity(
    string AssemblyName,
    string AssemblySha256,
    string? InformationalVersion);

public sealed record ToolchainIdentity(
    string RepositoryCommit,
    string EvidenceClassification,
    FirstPartyAssemblyIdentity Runner,
    List<FirstPartyAssemblyIdentity> FirstPartyDependencies)
{
    private static readonly string[] RequiredDependencies =
    [
        "AqualLifeStyle.Core",
        "AqualLifeStyle.EntityFrameworkCore"
    ];

    public static ToolchainIdentity Capture()
    {
        var runnerAssembly = Assembly.GetExecutingAssembly();
        var runner = CaptureAssembly(runnerAssembly);
        if (runner.AssemblyName != "AqualLifeStyle.B6SnapshotRunner")
            throw new B6PackageException("The B6 runner assembly identity is unexpected.");
        var dependencies = new[]
            {
                typeof(AQGreenLegacyCanonicalisationSimulator).Assembly,
                typeof(AqualLifeStyleDbContext).Assembly
            }
            .Select(CaptureAssembly)
            .OrderBy(value => value.AssemblyName, StringComparer.Ordinal)
            .ToList();
        var root = FindRepositoryRoot(runnerAssembly.Location);
        var commit = Run("git", ["-C", root, "rev-parse", "HEAD"]).Trim();
        if (!B6Contract.GitSha.IsMatch(commit)) throw new B6PackageException("Unable to bind the B6 tool to a repository commit.");
        var classification = Environment.GetEnvironmentVariable(B6Contract.EvidenceClassificationEnvironment);
        if (classification is not (B6Contract.PrecommitEvidence or B6Contract.OperationalEvidence))
            throw new B6PackageException($"{B6Contract.EvidenceClassificationEnvironment} must explicitly select " +
                $"{B6Contract.PrecommitEvidence} or {B6Contract.OperationalEvidence}.");
        if (classification == B6Contract.OperationalEvidence)
        {
            var status = Run("git", ["-C", root, "status", "--porcelain=v1", "--untracked-files=all"]);
            if (!string.IsNullOrWhiteSpace(status))
                throw new B6PackageException("Operational evidence requires an exact clean reviewed Git worktree with no untracked material.");
        }
        var result = new ToolchainIdentity(commit, classification, runner, dependencies);
        result.RequireComplete();
        if (classification == B6Contract.OperationalEvidence)
            result.RequireMatchesReviewedTreeBuild(root, commit);
        return result;
    }

    public void RequireComplete()
    {
        if (!B6Contract.GitSha.IsMatch(RepositoryCommit) ||
            EvidenceClassification is not (B6Contract.PrecommitEvidence or B6Contract.OperationalEvidence) ||
            Runner.AssemblyName != "AqualLifeStyle.B6SnapshotRunner" ||
            !B6Contract.Sha256.IsMatch(Runner.AssemblySha256))
            throw new B6PackageException("B6 toolchain runner/repository evidence is incomplete.");
        if (!FirstPartyDependencies.Select(value => value.AssemblyName)
                .SequenceEqual(FirstPartyDependencies.Select(value => value.AssemblyName).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new B6PackageException("First-party assembly identity is not in canonical Ordinal order.");
        if (FirstPartyDependencies.Select(value => value.AssemblyName).Distinct(StringComparer.Ordinal).Count() != FirstPartyDependencies.Count ||
            !FirstPartyDependencies.Select(value => value.AssemblyName).SequenceEqual(RequiredDependencies, StringComparer.Ordinal) ||
            FirstPartyDependencies.Any(value => !B6Contract.Sha256.IsMatch(value.AssemblySha256)))
            throw new B6PackageException("B6 toolchain is missing a required first-party assembly or contains an unexpected dependency.");
    }

    public void RequireEqual(ToolchainIdentity current)
    {
        RequireComplete();
        current.RequireComplete();
        if (RepositoryCommit != current.RepositoryCommit || EvidenceClassification != current.EvidenceClassification ||
            Runner != current.Runner || !FirstPartyDependencies.SequenceEqual(current.FirstPartyDependencies))
            throw new B6PackageException("Snapshot first-party executable toolchain/repository identity is stale or unbound.");
    }

    public void RequireMatchesBuiltArtifacts(string outputDirectory)
    {
        RequireComplete();
        var expected = new[] { Runner }.Concat(FirstPartyDependencies)
            .ToDictionary(value => value.AssemblyName, StringComparer.Ordinal);
        foreach (var item in expected)
        {
            var path = Path.Combine(outputDirectory, item.Key + ".dll");
            if (!File.Exists(path) || B6Values.Sha256Hex(File.ReadAllBytes(path)) != item.Value.AssemblySha256)
                throw new B6PackageException($"Executing first-party assembly {item.Key} does not match the fresh reviewed-source build from the reviewed Git object tree.");
        }
    }

    internal void RequireMatchesReviewedTreeBuild(string root, string reviewedCommit)
    {
        if (!B6Contract.GitSha.IsMatch(reviewedCommit))
            throw new B6PackageException("Reviewed Git commit identity is invalid.");
        var privateRoot = Directory.CreateTempSubdirectory("aqua-b6-toolchain-").FullName;
        var source = Path.Combine(privateRoot, "source");
        var output = Path.Combine(privateRoot, "output");
        var packages = Path.Combine(privateRoot, "packages");
        var worktreeCreated = false;
        try
        {
            SetPrivateDirectoryMode(privateRoot);
            CreatePrivateDirectory(output);
            CreatePrivateDirectory(packages);
            Run("git", ["-C", root, "worktree", "add", "--detach", source, reviewedCommit]);
            worktreeCreated = true;
            SetPrivateDirectoryMode(source);
            var materializedCommit = Run("git", ["-C", source, "rev-parse", "HEAD"]).Trim();
            var materializedStatus = Run("git", ["-C", source, "status", "--porcelain=v1", "--untracked-files=all"]);
            if (materializedCommit != reviewedCommit || !string.IsNullOrWhiteSpace(materializedStatus))
                throw new B6PackageException("Private reviewed Git object tree materialization is not exact and clean.");
            var project = Path.Combine(source, "AqualLifeStyle", "9.4.2", "aspnet-core", "tools",
                "AqualLifeStyle.B6SnapshotRunner", "AqualLifeStyle.B6SnapshotRunner.csproj");
            if (!File.Exists(project) || SecureFiles.IsLink(project))
                throw new B6PackageException("Reviewed Git object tree does not contain the B6 runner project as a regular file.");
            var nugetConfig = Path.Combine(source, "AqualLifeStyle", "9.4.2", "aspnet-core", "NuGet.Config");
            var restoreArguments = new List<string>
            {
                "restore", project, "--packages", packages,
                "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"
            };
            if (File.Exists(nugetConfig))
            {
                if (SecureFiles.IsLink(nugetConfig))
                    throw new B6PackageException("Reviewed NuGet configuration must not be a symlink.");
                restoreArguments.Add("--configfile");
                restoreArguments.Add(nugetConfig);
            }
            restoreArguments.Add("--disable-build-servers");
            Run("dotnet", restoreArguments);
            Run("dotnet", ["build", project, "--configuration", "Release", "--no-restore",
                "--no-incremental", "--disable-build-servers", "--output", output,
                "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false",
                $"-p:PathMap={source}={Path.GetFullPath(root)}"]);
            RequireMatchesBuiltArtifacts(output);
        }
        finally
        {
            if (worktreeCreated)
                Run("git", ["-C", root, "worktree", "remove", "--force", source]);
            if (Directory.Exists(privateRoot) && !SecureFiles.IsLink(privateRoot))
                Directory.Delete(privateRoot, recursive: true);
        }
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        SetPrivateDirectoryMode(path);
    }

    private static void SetPrivateDirectoryMode(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static FirstPartyAssemblyIdentity CaptureAssembly(Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly.Location) || !File.Exists(assembly.Location))
            throw new B6PackageException("A required first-party assembly has no verifiable file location.");
        return new FirstPartyAssemblyIdentity(
            assembly.GetName().Name ?? throw new B6PackageException("A required first-party assembly has no name."),
            B6Values.Sha256Hex(File.ReadAllBytes(assembly.Location)),
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
    }

    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? current = new(Path.GetDirectoryName(start)!);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            current = current.Parent;
        }
        throw new B6PackageException("The B6 executable is not bound to a Git worktree.");
    }

    public static string Run(string file, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment != null) foreach (var item in environment) start.Environment[item.Key] = item.Value;
        using var process = Process.Start(start) ?? throw new B6PackageException($"Unable to start {file}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var standardError = stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new B6PackageException($"Controlled {file} operation failed: {SanitizeDiagnostic(standardError)}");
        return stdout.GetAwaiter().GetResult();
    }

    private static string SanitizeDiagnostic(string value) =>
        string.IsNullOrWhiteSpace(value) ? "no diagnostic" : value.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
}
