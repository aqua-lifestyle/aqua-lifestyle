using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AqualLifeStyle.B6SnapshotRunner;

// The custodian's schema-only readback established 180004, 54 migrations through
// AddIsolatedAQGreenPilotEvidence, and the required public column shapes. This
// profile selects those existing semantics; it never applies a source migration.
internal static class B6SourceSchema
{
    internal sealed record Column(string Table, string Name, string Udt, bool Nullable);
    private static AqualLifeStyleDbContext Model() => new(
        new DbContextOptionsBuilder<AqualLifeStyleDbContext>().UseNpgsql().Options);

    internal static string[] MigrationIds()
    {
        using var context = Model();
        var ids = context.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal).ToArray();
        if (ids.Length != 54 || ids.Last() != B6Contract.ExpectedMigrationHead)
            throw new B6PackageException("The executing EF migration inventory is not the reviewed B6 PG18 schema profile.");
        return ids;
    }

    internal static string ExpectedHistoryDigest => B6Values.Sha256Hex(
        string.Join('\n', MigrationIds().Select(id => $"{id}|8.0.8")));

    internal static void RequireHistory(IEnumerable<(string Id, string ProductVersion)> history)
    {
        var rows = history.ToArray();
        if (!rows.Select(row => row.Id).SequenceEqual(MigrationIds()) || rows.Any(row => row.ProductVersion != "8.0.8"))
            throw new B6PackageException("Source migration history is missing, extra, reordered or outside the reviewed B6 schema profile.");
    }

    internal static void RequireVersion(int number)
    {
        if (number != B6Contract.PostgreSqlVersionNumber)
            throw new B6PackageException("The selected B6 source/runtime profile requires PostgreSQL 18.4 (180004).");
    }

    internal static bool VersionTextMatches(string value) => value == "18.4" || value?.StartsWith("18.4 ", StringComparison.Ordinal) == true;

    internal static IReadOnlyList<Column> Columns(B6Allowlist allowlist)
    {
        using var context = Model();
        var tables = context.Model.GetRelationalModel().Tables;
        var result = new List<Column>();
        foreach (var required in allowlist.Value.Tables)
        foreach (var field in required.SourceFields)
        {
            var name = field.SourceColumns.Single();
            if (required.SourceTable == "__EFMigrationsHistory")
            { result.Add(new(required.SourceTable, name, "varchar", false)); continue; }
            var table = tables.Single(value => value.Name == required.SourceTable && value.Schema is null or "public");
            var column = table.Columns.Single(value => value.Name == name);
            var type = column.StoreType;
            var udt = type switch
            {
                "integer" => "int4", "bigint" => "int8", "boolean" => "bool", "uuid" => "uuid",
                "text" => "text", "timestamp with time zone" => "timestamptz", "numeric" => "numeric",
                _ when type.StartsWith("character varying", StringComparison.Ordinal) => "varchar",
                _ => throw new B6PackageException("An allowlisted source column has an unsupported executing-model type.")
            };
            result.Add(new(required.SourceTable, name, udt, column.IsNullable));
        }
        return result;
    }

    internal static async Task RequireColumnsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, B6Allowlist allowlist)
    {
        // Zero-row SELECTs retain normal ACCESS SHARE locks until rollback: the
        // catalog check and subsequent reads cannot straddle a concurrent DDL.
        foreach (var table in allowlist.Value.Tables)
        {
            await using var locked = new NpgsqlCommand($"SELECT 1 FROM public.{B6Values.QuoteIdentifier(table.SourceTable)} LIMIT 0", connection, transaction);
            await locked.ExecuteNonQueryAsync();
        }
        var actual = new Dictionary<(string Table, string Name), (string Udt, bool Nullable)>();
        await using var command = new NpgsqlCommand(
            "SELECT c.table_name,c.column_name,c.udt_name,c.is_nullable FROM information_schema.columns c " +
            "WHERE c.table_schema='public' AND c.table_name=ANY(@tables) AND EXISTS (" +
            "SELECT 1 FROM information_schema.tables t WHERE t.table_schema='public' AND t.table_name=c.table_name AND t.table_type='BASE TABLE')", connection, transaction);
        command.Parameters.AddWithValue("tables", allowlist.Value.Tables.Select(table => table.SourceTable).ToArray());
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            if (!actual.TryAdd((reader.GetString(0), reader.GetString(1)), (reader.GetString(2), reader.GetString(3) == "YES")))
                throw new B6PackageException("Source column identity is ambiguous.");
        foreach (var expected in Columns(allowlist))
            if (!actual.TryGetValue((expected.Table, expected.Name), out var column) || column != (expected.Udt, expected.Nullable))
                throw new B6PackageException($"Source column shape is unsupported: {expected.Table}.{expected.Name}.");
        // Additional columns are not exported. Missing or incompatible required
        // columns never acquire a compatibility/default value.
    }
}
