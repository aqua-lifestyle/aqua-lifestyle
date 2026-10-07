using System.Net;
using System.Text.Json;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// No app startup, migration, seeding, feature gate or Render API dependency.
try
{
    if (args.Length == 1 && args[0] == "--help")
    {
        Console.WriteLine("Customer11Backfill --mode dry-run|apply --manifest FILE --output FILE --source-capture FILE --target-kind local-fixture|verified-recovery-clone --expected-database NAME --expected-user NAME --expected-server-address IP --expected-server-port PORT --source-snapshot-sha256 SHA [--expected-service-id ID --verified-target-receipt FILE --verified-target-reference RECEIPT_SHA256]");
        Console.WriteLine("Connection secret: AQGREEN_CUSTOMER11_BACKFILL_DSN environment variable. Production execution is prohibited. Clone verification and execution require separate owner authority.");
        return 0;
    }
    if (args.Length % 2 != 0) throw new InvalidOperationException("Every option requires one explicit value.");
    var allowed = new HashSet<string> { "--mode", "--manifest", "--output", "--source-capture", "--target-kind", "--expected-database", "--expected-user", "--expected-server-address", "--expected-server-port", "--source-snapshot-sha256", "--verified-target-reference", "--verified-target-receipt", "--expected-service-id" };
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
        if (!allowed.Contains(args[i]) || !parsed.TryAdd(args[i], args[i + 1]))
            throw new InvalidOperationException("Unknown or duplicate operator option.");
    string Required(string key) => parsed.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value : throw new InvalidOperationException($"Explicit {key} is required.");
    var mode = Required("--mode");
    if (mode != "dry-run" && mode != "apply") throw new InvalidOperationException("Mode must be dry-run or apply.");
    var plan = AQGreenCustomer11BackfillPlan.Load(File.ReadAllBytes(Required("--manifest")), File.ReadAllBytes(Required("--output")), File.ReadAllBytes(Required("--source-capture")));
    var dsn = Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_BACKFILL_DSN");
    if (string.IsNullOrWhiteSpace(dsn)) throw new InvalidOperationException("Connection environment variable is required; credentials must not be command arguments.");
    var connection = new NpgsqlConnectionStringBuilder(dsn) { ApplicationName = "AQGreenCustomer11BoundedBackfill", IncludeErrorDetail = false };
    var targetKind = Required("--target-kind");
    var database = Required("--expected-database");
    var user = Required("--expected-user");
    if (database != connection.Database || user != connection.Username)
        throw new InvalidOperationException("Connection database/user differ from operator expectation.");
    var address = Required("--expected-server-address");
    if (!IPAddress.TryParse(address, out _) || !int.TryParse(Required("--expected-server-port"), out var port) || port < 1 || port > 65535)
        throw new InvalidOperationException("Explicit server IP/internal port identity is required.");
    var clone = targetKind == "verified-recovery-clone";
    if (!clone && parsed.Keys.Any(k => k == "--expected-service-id" || k == "--verified-target-receipt" || k == "--verified-target-reference"))
        throw new InvalidOperationException("Clone verification options cannot be used in fixture mode.");
    var target = new AQGreenBackfillTargetIdentity(database, user, address, port, Required("--source-snapshot-sha256"),
        targetKind, clone ? Required("--expected-service-id") : null,
        AQGreenCustomer11BackfillPlan.ServerVersionNumber,
        clone ? File.ReadAllBytes(Required("--verified-target-receipt")) : null,
        clone ? Required("--verified-target-reference") : null);
    AQGreenCustomer11TargetGuard.Verify(connection, target); // same check also enforced inside the writer
    await using var context = new AqualLifeStyleDbContext(new DbContextOptionsBuilder<AqualLifeStyleDbContext>().UseNpgsql(connection.ConnectionString).Options);
    var result = await new AQGreenCustomer11BackfillWriter(context).ExecuteAsync(plan, target, mode == "dry-run");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        result = "PASS", mode, targetKind, database, manifestHashVerified = AQGreenCustomer11BackfillPlan.InputSha256,
        canonicalOutputHashVerified = AQGreenCustomer11BackfillPlan.OutputSha256, scopeId = plan.ScopeId,
        placementSource = AQGreenCustomer11BackfillPlan.PlacementSource, effectiveAtUtc = plan.EffectiveAtUtc,
        proposedOrAppliedDelta = result, currentRuntimeQualification = "NOT_ESTABLISHED"
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (NpgsqlException exception)
{
    // Do not echo server detail, connection string or secrets.
    Console.Error.WriteLine(JsonSerializer.Serialize(new { result = "REJECTED", category = "POSTGRESQL_FAILURE", sqlState = exception.SqlState, delta = (object?)null }));
    return 1;
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { result = "REJECTED", category = "CONTRACT_CONFLICT", reason = exception.Message, delta = (object?)null }));
    return 1;
}
catch
{
    Console.Error.WriteLine("{\"result\":\"REJECTED\",\"category\":\"INPUT_OR_EXECUTION_FAILURE\",\"delta\":null}");
    return 1;
}
