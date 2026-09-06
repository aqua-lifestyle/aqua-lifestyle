namespace AqualLifeStyle.B6SnapshotRunner;

public static class B6CommandLine
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> CommandOptions =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
    {
        ["generate-key"] = Options("output"),
        ["extract"] = Options("source-runtime-state", "dsn-env", "output", "source-system-id",
            "repository-commit", "tenant-id", "population-scope", "analysis-cutoff", "authorized-operator-ack"),
        ["sanitize"] = Options("input", "output", "key-file", "snapshot-id", "created-at"),
        ["manifest"] = Options("snapshot", "output"),
        ["verify"] = Options("snapshot", "manifest", "forbidden-values", "root-dispositions", "inventory",
            "runtime-state", "schema", "sql-file"),
        ["verify-inventory"] = Options("snapshot", "base-manifest", "inventory", "runtime-state", "schema", "sql-file"),
        ["runtime-create"] = Options("state", "role"),
        ["runtime-destroy"] = Options("state"),
        ["fixture-migrate"] = Options("runtime-state"),
        ["fixture-load"] = Options("runtime-state", "sql-file"),
        ["restore"] = Options("snapshot", "manifest", "runtime-state", "schema", "forbidden-values"),
        ["verify-db"] = Options("runtime-state", "schema", "manifest"),
        ["inventory"] = Options("snapshot", "base-manifest", "runtime-state", "schema", "sql-file", "output"),
        ["analyse"] = Options("snapshot", "manifest", "root-dispositions", "output", "simulator-input", "forbidden-values"),
        ["finalize-manifest"] = Options("snapshot", "base-manifest", "root-dispositions", "analysis-output",
            "inventory", "runtime-state", "schema", "sql-file", "simulation-output", "impact-output", "output", "forbidden-values"),
        ["impact-report"] = Options("snapshot", "manifest", "root-dispositions", "inventory", "runtime-state", "schema",
            "sql-file", "output", "forbidden-values"),
        ["handoff-receipt"] = Options("snapshot", "manifest", "root-dispositions", "approved-destination",
            "inventory", "runtime-state", "schema", "sql-file", "output", "generated-at", "forbidden-values"),
        ["verify-test-run"] = Options("trx", "expected-count"),
        ["simulate"] = Options("input", "output")
    };

    private static HashSet<string> Options(params string[] values) =>
        values.Append("allowlist").ToHashSet(StringComparer.Ordinal);

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var parsed = Arguments.Parse(args);
            if (!CommandOptions.TryGetValue(parsed.Command, out var allowedOptions))
                throw new B6PackageException($"Unknown B6 command '{parsed.Command}'.");
            parsed.EnsureOnly(allowedOptions);

            var package = new B6SnapshotPackage(parsed.OptionalPath("allowlist"));
            switch (parsed.Command)
            {
                case "generate-key":
                    package.GenerateKey(parsed.RequiredPath("output"));
                    break;
                case "extract":
                    var sourceRuntime = parsed.OptionalPath("source-runtime-state");
                    string sourceDsn;
                    if (sourceRuntime != null)
                    {
                        var ownedSource = await package.Runtime.LoadAsync(sourceRuntime, "SOURCE");
                        sourceDsn = ownedSource.ConnectionString;
                        await ownedSource.Connection.DisposeAsync();
                        if (parsed.Optional("dsn-env") != null)
                            throw new B6PackageException("Extraction source must be either owned runtime state or a DSN environment variable, never both.");
                    }
                    else sourceDsn = parsed.RequiredDsnFromEnvironment();
                    await package.ExtractAsync(new ExtractionRequest(
                        sourceDsn, parsed.RequiredPath("output"),
                        parsed.Required("source-system-id"), parsed.Required("repository-commit"),
                        parsed.RequiredInt("tenant-id"), parsed.Required("population-scope"),
                        B6Values.ParseTimestamp(parsed.Required("analysis-cutoff")),
                        parsed.Flag("authorized-operator-ack")));
                    break;
                case "sanitize":
                    package.Sanitize(parsed.RequiredPath("input"), parsed.RequiredPath("output"),
                        parsed.RequiredPath("key-file"), parsed.Required("snapshot-id"),
                        parsed.OptionalTimestamp("created-at"));
                    break;
                case "manifest":
                    package.CreateBaseManifest(parsed.RequiredPath("snapshot"), parsed.RequiredPath("output"));
                    break;
                case "verify":
                    var verificationRoots = parsed.OptionalPath("root-dispositions");
                    if (verificationRoots != null ||
                        parsed.OptionalPath("inventory") != null || parsed.OptionalPath("runtime-state") != null ||
                        parsed.OptionalPath("sql-file") != null)
                    {
                        await package.VerifyFinalAsync(parsed.RequiredPath("snapshot"), parsed.RequiredPath("manifest"),
                            verificationRoots ?? throw new B6PackageException("Final verification requires --root-dispositions."),
                            parsed.RequiredPath("inventory"), parsed.RequiredPath("runtime-state"),
                            parsed.Optional("schema") ?? "b6_snapshot", parsed.RequiredPath("sql-file"),
                            parsed.OptionalPath("forbidden-values"));
                    }
                    else package.Verify(parsed.RequiredPath("snapshot"), parsed.RequiredPath("manifest"),
                        parsed.OptionalPath("forbidden-values"));
                    break;
                case "verify-inventory":
                    await package.VerifyInventoryAsync(parsed.RequiredPath("snapshot"), parsed.RequiredPath("base-manifest"),
                        parsed.RequiredPath("inventory"), parsed.RequiredPath("runtime-state"),
                        parsed.Optional("schema") ?? "b6_snapshot", parsed.RequiredPath("sql-file"));
                    break;
                case "runtime-create":
                    await package.Runtime.CreateAsync(parsed.RequiredPath("state"), parsed.Optional("role") ?? "ANALYSIS");
                    break;
                case "runtime-destroy":
                    await package.Runtime.DestroyAsync(parsed.RequiredPath("state"));
                    break;
                case "fixture-migrate":
                    await package.Runtime.MigrateFixtureAsync(parsed.RequiredPath("runtime-state"));
                    break;
                case "fixture-load":
                    await package.Runtime.LoadFixtureAsync(parsed.RequiredPath("runtime-state"), parsed.RequiredPath("sql-file"));
                    break;
                case "restore":
                    await package.RestoreAsync(parsed.RequiredPath("snapshot"), parsed.RequiredPath("manifest"),
                        parsed.RequiredPath("runtime-state"), parsed.Optional("schema") ?? "b6_snapshot",
                        parsed.OptionalPath("forbidden-values"));
                    break;
                case "verify-db":
                    await package.VerifyDatabaseAsync(parsed.RequiredPath("runtime-state"),
                        parsed.Optional("schema") ?? "b6_snapshot", parsed.RequiredPath("manifest"));
                    break;
                case "inventory":
                    await package.RunInventoryAsync(parsed.RequiredPath("snapshot"), parsed.RequiredPath("base-manifest"),
                        parsed.RequiredPath("runtime-state"),
                        parsed.Optional("schema") ?? "b6_snapshot", parsed.RequiredPath("sql-file"),
                        parsed.RequiredPath("output"));
                    break;
                case "analyse":
                    package.Analyse(parsed.RequiredPath("snapshot"), parsed.RequiredPath("manifest"),
                        parsed.RequiredPath("root-dispositions"), parsed.RequiredPath("output"),
                        parsed.OptionalPath("simulator-input"), parsed.OptionalPath("forbidden-values"));
                    break;
                case "finalize-manifest":
                    await package.FinalizeManifestAsync(new FinalizationRequest(
                        parsed.RequiredPath("snapshot"), parsed.RequiredPath("base-manifest"),
                        parsed.RequiredPath("root-dispositions"), parsed.RequiredPath("inventory"),
                        parsed.RequiredPath("runtime-state"), parsed.Optional("schema") ?? "b6_snapshot",
                        parsed.RequiredPath("sql-file"),
                        parsed.RequiredPath("analysis-output"),
                        parsed.RequiredPath("simulation-output"), parsed.RequiredPath("impact-output"),
                        parsed.RequiredPath("output"), parsed.OptionalPath("forbidden-values")));
                    break;
                case "impact-report":
                    await package.WriteImpactReportAsync(parsed.RequiredPath("snapshot"), parsed.RequiredPath("manifest"),
                        parsed.RequiredPath("root-dispositions"), parsed.RequiredPath("inventory"),
                        parsed.RequiredPath("runtime-state"), parsed.Optional("schema") ?? "b6_snapshot",
                        parsed.RequiredPath("sql-file"), parsed.RequiredPath("output"),
                        parsed.OptionalPath("forbidden-values"));
                    break;
                case "handoff-receipt":
                    await package.CreateHandoffReceiptAsync(parsed.RequiredPath("snapshot"), parsed.RequiredPath("manifest"),
                        parsed.RequiredPath("root-dispositions"), parsed.RequiredPath("inventory"),
                        parsed.RequiredPath("runtime-state"), parsed.Optional("schema") ?? "b6_snapshot",
                        parsed.RequiredPath("sql-file"), parsed.Required("approved-destination"),
                        parsed.RequiredPath("output"), parsed.OptionalTimestamp("generated-at"),
                        parsed.OptionalPath("forbidden-values"));
                    break;
                case "verify-test-run":
                    var testSummary = B6TestRunResults.ReadAndValidate(parsed.RequiredPath("trx"),
                        parsed.OptionalPositiveInt("expected-count"));
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(testSummary,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        }));
                    break;
                case "simulate":
                    package.RunSimulator(parsed.RequiredPath("input"), parsed.RequiredPath("output"));
                    break;
            }
            return 0;
        }
        catch (B6PackageException exception)
        {
            Console.Error.WriteLine($"B6 snapshot package rejected input: {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"B6 snapshot package failed closed: {exception.Message}");
            return 2;
        }
    }
}

public sealed class Arguments
{
    private readonly Dictionary<string, string?> _values;
    private Arguments(string command, Dictionary<string, string?> values)
    {
        Command = command;
        _values = values;
    }
    public string Command { get; }

    public void EnsureOnly(IReadOnlySet<string> allowed)
    {
        var unknown = _values.Keys.Where(name => !allowed.Contains(name)).Order(StringComparer.Ordinal).ToList();
        if (unknown.Count != 0)
            throw new B6PackageException($"Command '{Command}' does not accept option(s): {string.Join(", ", unknown.Select(name => $"--{name}"))}.");
    }

    public static Arguments Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0) throw new B6PackageException("A B6 command is required.");
        var commandIndex = -1;
        for (var index = 0; index < args.Count; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                commandIndex = index;
                break;
            }
            if (index + 1 >= args.Count)
                throw new B6PackageException($"Option {args[index]} requires a value before the command.");
            index++;
        }
        if (commandIndex < 0) throw new B6PackageException("A B6 command is required.");
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index++)
        {
            if (index == commandIndex) continue;
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
                throw new B6PackageException($"Unexpected positional argument '{token}'.");
            var name = token[2..];
            if (!values.TryAdd(name, null))
                throw new B6PackageException($"Option --{name} was supplied more than once.");
            if (index + 1 < args.Count && index + 1 != commandIndex &&
                !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                values[name] = args[++index];
        }
        return new Arguments(args[commandIndex], values);
    }

    public string Required(string name) =>
        _values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new B6PackageException($"--{name} is required.");
    public string? Optional(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public bool Flag(string name) => _values.TryGetValue(name, out var value) && value is null;
    public int RequiredInt(string name) => int.TryParse(Required(name), out var value)
        ? value : throw new B6PackageException($"--{name} must be an integer.");
    public int? OptionalPositiveInt(string name)
    {
        var text = Optional(name);
        if (text == null) return null;
        return int.TryParse(text, out var value) && value > 0
            ? value : throw new B6PackageException($"--{name} must be a positive integer.");
    }
    public string RequiredPath(string name) => Path.GetFullPath(Required(name));
    public string? OptionalPath(string name) => Optional(name) is { } value ? Path.GetFullPath(value) : null;
    public DateTimeOffset? OptionalTimestamp(string name) => Optional(name) is { } value ? B6Values.ParseTimestamp(value) : null;

    public string RequiredDsnFromEnvironment()
    {
        var name = Required("dsn-env");
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^B6_[A-Z0-9_]+$"))
            throw new B6PackageException("DSN environment-variable name must use the B6_ prefix.");
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } dsn
            ? dsn : throw new B6PackageException($"Required DSN environment variable {name} is absent.");
    }
}

public sealed class B6PackageException : Exception
{
    public B6PackageException(string message) : base(message) { }
    public B6PackageException(string message, Exception inner) : base(message, inner) { }
}
