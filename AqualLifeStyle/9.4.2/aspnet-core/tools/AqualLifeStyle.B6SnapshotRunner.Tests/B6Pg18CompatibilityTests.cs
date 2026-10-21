using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.B6SnapshotRunner.Tests;

public sealed class B6Pg18CompatibilityTests
{
    [Fact]
    public void ReviewedProfile_MatchesCustodianHeadAndAllowlistPin()
    {
        B6Contract.PostgreSqlVersionNumber.ShouldBe(180004);
        B6Contract.ExpectedMigrationHead.ShouldBe("20261005194901_AddIsolatedAQGreenPilotEvidence");
        B6SourceSchema.MigrationIds().Length.ShouldBe(54);
        var node = CanonicalJson.Read(Path.Combine(B6Paths.PackageRoot(), "allowlist.json"), controlled: false);
        CanonicalJson.Digest(node).ShouldBe(B6Contract.ExpectedAllowlistDigest);
    }

    [Theory]
    [InlineData(160015)]
    [InlineData(170001)]
    [InlineData(180006)]
    [InlineData(0)]
    public void UnselectedServerVersion_IsNotCompatibility(int number) =>
        Should.Throw<B6PackageException>(() => B6SourceSchema.RequireVersion(number));

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("product")]
    [InlineData("reordered")]
    public void HeadAlone_DoesNotAuthorizeHistory(string fault)
    {
        var rows = B6SourceSchema.MigrationIds().Select(id => (Id: id, ProductVersion: "8.0.8")).ToList();
        if (fault == "missing") rows.RemoveAt(0);
        if (fault == "extra") rows.Insert(0, ("20260101000000_UNSELECTED", "8.0.8"));
        if (fault == "product") rows[0] = (rows[0].Id, "0.0.0");
        if (fault == "reordered") (rows[0], rows[1]) = (rows[1], rows[0]);
        Should.Throw<B6PackageException>(() => B6SourceSchema.RequireHistory(rows));
    }

    [Fact]
    public async Task Pg184_CurrentEfSchema_ReadOnlyRoundTrip_RejectsUnsupportedSourceAndSnapshot()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This B6 PG18 qualification executes on Linux.");
        Environment.SetEnvironmentVariable(B6Contract.EvidenceClassificationEnvironment, B6Contract.PrecommitEvidence);
        var package = new B6SnapshotPackage();
        var root = Path.Combine(Path.GetTempPath(), "b6-pg18-compat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        SecureFiles.RequireTrustedDirectory(root);
        var state = Path.Combine(root, "source-runtime.json");
        try
        {
            await package.Runtime.CreateAsync(state, "SOURCE");
            await package.Runtime.MigrateFixtureAsync(state);
            await package.Runtime.LoadFixtureAsync(state, Path.Combine(B6Paths.PackageRoot(), "fixture", "synthetic-data.sql"));
            var owned = await package.Runtime.LoadAsync(state, "SOURCE");
            await using var source = owned.Connection;
            (await Scalar(source, "SELECT current_setting('server_version_num')")).ShouldBe("180004");
            (await Scalar(source, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\"")).ShouldBe("54");
            (await Scalar(source, "SELECT MAX(\"MigrationId\") FROM public.\"__EFMigrationsHistory\"")).ShouldBe(B6Contract.ExpectedMigrationHead);
            var allowlist = new B6Allowlist(null);
            await using (var columns = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name=ANY(@tables)", source))
            {
                columns.Parameters.AddWithValue("tables", allowlist.Value.Tables.Select(table => table.SourceTable).ToArray());
                Convert.ToInt32(await columns.ExecuteScalarAsync()).ShouldBe(241);
            }
            var before = await Fingerprint(source, allowlist);
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            // Generated synthetic credentials stay in memory and never in test output.
            await using (var role = new NpgsqlCommand("CREATE ROLE b6_readonly LOGIN PASSWORD '" + password.Replace("'", "''") + "'; " +
                "GRANT USAGE ON SCHEMA public TO b6_readonly; GRANT SELECT ON ALL TABLES IN SCHEMA public TO b6_readonly; " +
                "GRANT EXECUTE ON FUNCTION pg_catalog.pg_control_system() TO b6_readonly; ALTER ROLE b6_readonly SET default_transaction_read_only=on", source))
                await role.ExecuteNonQueryAsync();
            var readerDsn = new NpgsqlConnectionStringBuilder(owned.ConnectionString) { Username = "b6_readonly", Password = password }.ConnectionString;
            var identity = ToolchainIdentity.Capture();
            async Task Extract(string output) => await package.ExtractAsync(new ExtractionRequest(readerDsn, output, "SYNTHETIC-PG18",
                identity.RepositoryCommit, 1, B6Contract.PopulationScope, new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero), true));
            var raw = Path.Combine(root, "raw");
            await Extract(raw);
            var metadata = CanonicalJson.Read<SourceMetadata>(Path.Combine(raw, "source-metadata.json"));
            metadata.TransactionMode.ShouldBe("REPEATABLE READ READ ONLY");
            metadata.SourcePostgreSqlMajorVersion.ShouldBe(18);
            metadata.SourceSchemaVersion.ShouldBe(B6SourceSchema.ExpectedHistoryDigest);
            metadata.EfMigrationHead.ShouldBe(B6Contract.ExpectedMigrationHead);
            var key = Path.Combine(root, "key"); package.GenerateKey(key);
            var snapshots = new List<string>(); var manifests = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                var snapshot = Path.Combine(root, "snapshot-" + i); var manifest = Path.Combine(root, "base-" + i + ".json");
                package.Sanitize(raw, snapshot, key, "B6-PG18-SYNTHETIC", new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero));
                package.CreateBaseManifest(snapshot, manifest);
                package.Verify(snapshot, manifest, Path.Combine(B6Paths.PackageRoot(), "fixture", "forbidden-values.txt"));
                snapshots.Add(snapshot); manifests.Add(manifest);
            }
            File.ReadAllBytes(manifests[0]).ShouldBe(File.ReadAllBytes(manifests[1]));
            foreach (var file in Directory.GetFiles(snapshots[0]))
                File.ReadAllBytes(file).ShouldBe(File.ReadAllBytes(Path.Combine(snapshots[1], Path.GetFileName(file))));
            (await Fingerprint(source, allowlist)).ShouldBe(before);
            // Full-history and required-column drift deny before creating raw output.
            var first = B6SourceSchema.MigrationIds()[0];
            await Exec(source, "DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\"='" + first + "'");
            var bad = Path.Combine(root, "bad-history"); await Should.ThrowAsync<B6PackageException>(() => Extract(bad)); Directory.Exists(bad).ShouldBeFalse();
            await Exec(source, "INSERT INTO public.\"__EFMigrationsHistory\" VALUES ('" + first + "','8.0.8')");
            foreach (var (change, undo) in new[]
            {
                ("ALTER TABLE public.\"EntryParticipations\" ALTER COLUMN \"Status\" TYPE bigint", "ALTER TABLE public.\"EntryParticipations\" ALTER COLUMN \"Status\" TYPE integer USING \"Status\"::integer"),
                ("ALTER TABLE public.\"EntryParticipations\" ALTER COLUMN \"Status\" DROP NOT NULL", "ALTER TABLE public.\"EntryParticipations\" ALTER COLUMN \"Status\" SET NOT NULL"),
                ("ALTER TABLE public.\"EntryParticipations\" RENAME COLUMN \"Status\" TO \"UnselectedStatus\"", "ALTER TABLE public.\"EntryParticipations\" RENAME COLUMN \"UnselectedStatus\" TO \"Status\"")
            })
            {
                await Exec(source, change); bad = Path.Combine(root, "bad-column-" + Guid.NewGuid().ToString("N"));
                await Should.ThrowAsync<B6PackageException>(() => Extract(bad)); Directory.Exists(bad).ShouldBeFalse(); await Exec(source, undo);
            }
            var snapshotMetadata = Path.Combine(snapshots[0], "snapshot-metadata.json"); var original = File.ReadAllBytes(snapshotMetadata);
            foreach (var (field, value) in new (string, JsonNode?)[]
            {
                ("sourcePostgreSqlMajorVersion", JsonValue.Create(16)), ("sourcePostgreSqlVersion", JsonValue.Create("18.6")),
                ("efMigrationHead", JsonValue.Create("20260831230903_AddAQGreenV2CommissionConsumption")),
                ("packageSchemaVersion", JsonValue.Create("AQGreenB6SnapshotPackageV3")), ("sourceSchemaVersion", JsonValue.Create(new string('a',64)))
            })
            {
                var mutated = JsonNode.Parse(original)!.AsObject(); mutated[field] = value;
                File.WriteAllBytes(snapshotMetadata, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(mutated, CanonicalJson.Options));
                Should.Throw<B6PackageException>(() => package.CreateBaseManifest(snapshots[0], Path.Combine(root, "invalid.json")));
                File.WriteAllBytes(snapshotMetadata, original);
            }
        }
        finally
        {
            if (File.Exists(state)) await package.Runtime.DestroyAsync(state);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> Scalar(NpgsqlConnection connection, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); return Convert.ToString(await command.ExecuteScalarAsync())!; }
    private static async Task Exec(NpgsqlConnection connection, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private static async Task<string> Fingerprint(NpgsqlConnection connection, B6Allowlist allowlist)
    {
        var parts = new List<string>();
        foreach (var table in allowlist.Value.Tables)
        {
            var keys = string.Join(',', table.PrimaryKey.Select(B6Values.QuoteIdentifier));
            parts.Add(await Scalar(connection, $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY {keys})::text,'[]') FROM public.{B6Values.QuoteIdentifier(table.SourceTable)} t"));
        }
        return B6Values.Sha256Hex(string.Join('\n', parts));
    }
}
