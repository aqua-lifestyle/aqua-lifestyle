using System.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed partial class B6SnapshotPackage
{
    private const string SnapshotAuthorityTable = "__AquaB6SnapshotAuthority";

    public async Task RunInventoryAsync(string snapshot, string baseManifestPath, string runtimeState,
        string schema, string sqlPath, string output)
    {
        ValidateSchemaName(schema);
        var baseManifest = Verify(snapshot, baseManifestPath);
        if (baseManifest["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT")
            throw new B6PackageException("Inventory requires a verified BASE_SNAPSHOT manifest.");
        CanonicalJson.WriteNew(output,
            await BuildInventoryValueAsync(baseManifest, runtimeState, schema, sqlPath));
    }

    public async Task<JsonObject> VerifyInventoryAsync(string snapshot, string baseManifestPath,
        string inventoryPath, string runtimeState, string schema, string sqlPath)
    {
        ValidateSchemaName(schema);
        var baseManifest = Verify(snapshot, baseManifestPath);
        return await VerifyInventoryAsync(baseManifest, inventoryPath, runtimeState, schema, sqlPath);
    }

    private async Task<JsonObject> VerifyInventoryAsync(JsonObject baseManifest, string inventoryPath,
        string runtimeState, string schema, string sqlPath)
    {
        if (baseManifest["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT")
            throw new B6PackageException("Inventory verification requires a verified BASE_SNAPSHOT manifest.");
        var supplied = CanonicalJson.Read(inventoryPath).AsObject();
        var expected = await BuildInventoryValueAsync(baseManifest, runtimeState, schema, sqlPath);
        if (!JsonNode.DeepEquals(supplied, expected))
            throw new B6PackageException("Inventory does not equal an authoritative execution against the verified restored snapshot.");
        return supplied;
    }

    private async Task<JsonObject> BuildInventoryValueAsync(JsonObject baseManifest, string runtimeState,
        string schema, string sqlPath)
    {
        ValidateSchemaName(schema);
        string sql;
        using (var stream = SecureFiles.OpenRead(sqlPath, controlled: false))
        using (var reader = new StreamReader(stream)) sql = await reader.ReadToEndAsync();
        var queryDigest = B6Values.Sha256Hex(sql);
        var authoritativeQueryDigest = B6Values.Sha256Hex(await File.ReadAllTextAsync(B6Paths.InventorySql()));
        if (queryDigest != authoritativeQueryDigest)
            throw new B6PackageException("Inventory SQL is not the repository-sealed authoritative query definition.");
        var firstStatement = sql.IndexOf("-- AQGreen V2 B6 read-only inventory.", StringComparison.Ordinal);
        if (firstStatement < 0 || !sql.TrimEnd().EndsWith("ROLLBACK;", StringComparison.Ordinal))
            throw new B6PackageException("Inventory SQL does not match the controlled read-only contract.");
        sql = sql[firstStatement..].Replace(":'b6_cutoff_utc'", "@cutoff", StringComparison.Ordinal);
        sql = sql.Replace("BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;", "", StringComparison.Ordinal)
            .Replace("ROLLBACK;", "", StringComparison.Ordinal);
        var cutoff = B6Values.ParseTimestamp(baseManifest["analysisSnapshotCutoff"]!.GetValue<string>());
        var owned = await Runtime.LoadAsync(runtimeState, "ANALYSIS");
        await using var connection = owned.Connection;
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await ExecuteAsync(connection, transaction, "SET TRANSACTION READ ONLY");
        await ExecuteAsync(connection, transaction, $"SET LOCAL search_path={B6Values.QuoteIdentifier(schema)}");
        await VerifyDatabaseAsync(connection, transaction, schema, baseManifest);
        var sets = new JsonArray();
        await using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 600 };
        command.Parameters.AddWithValue("cutoff", cutoff);
        await using var data = await command.ExecuteReaderAsync();
        do
        {
            if (data.FieldCount == 0) continue;
            var columns = Enumerable.Range(0, data.FieldCount).Select(data.GetName).ToList();
            var resultRows = new JsonArray();
            while (await data.ReadAsync())
            {
                var row = new JsonArray();
                for (var index = 0; index < data.FieldCount; index++)
                    row.Add(data.IsDBNull(index) ? null : FormatDatabaseValue(data, index));
                resultRows.Add(row);
            }
            sets.Add(new JsonObject
            {
                ["columns"] = new JsonArray(columns.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                ["rows"] = resultRows
            });
        } while (await data.NextResultAsync());
        await data.DisposeAsync();
        await transaction.RollbackAsync();
        var semantic = sets.DeepClone().AsArray();
        if (semantic.Count > 0 && semantic[0]?["columns"] is JsonArray firstColumns && semantic[0]?["rows"] is JsonArray firstRows)
        {
            var snapshotIndex = firstColumns.Select((node, index) => (node, index))
                .FirstOrDefault(item => item.node?.GetValue<string>() == "DatabaseSnapshot").index;
            if (snapshotIndex >= 0)
                foreach (var row in firstRows) if (row is JsonArray values && snapshotIndex < values.Count) values[snapshotIndex] = "SEALED_PER_EXECUTION";
        }
        var inventoryResultDigest = CanonicalJson.Digest(semantic);
        var inventorySemanticDigest = B6Digests.Tuples([new[]
        {
            B6Contract.InventoryVersion,
            B6Contract.InventorySemanticVersion,
            queryDigest
        }]);
        var result = new JsonObject
        {
            ["snapshotId"] = baseManifest["snapshotId"]!.DeepClone(),
            ["baseManifestDigest"] = baseManifest["manifestHash"]!.DeepClone(),
            ["sourceSnapshotDigest"] = baseManifest["overallSnapshotDigest"]!.DeepClone(),
            ["inventoryRulesVersion"] = B6Contract.InventoryVersion,
            ["inventorySemanticVersion"] = B6Contract.InventorySemanticVersion,
            ["inventorySemanticDigest"] = inventorySemanticDigest,
            ["inventoryQueryDigest"] = queryDigest,
            ["analysisSnapshotCutoff"] = B6Values.Timestamp(cutoff),
            ["toolchainIdentity"] = CanonicalJson.ToNode(_toolchainIdentity),
            ["resultSets"] = semantic,
            ["inventoryResultDigest"] = inventoryResultDigest
        };
        result["inventoryDigest"] = CanonicalJson.Digest(result);
        return result;
    }

    public async Task RestoreAsync(string snapshot, string manifest, string runtimeState,
        string schema, string? forbiddenValues)
    {
        ValidateSchemaName(schema);
        var verified = Verify(snapshot, manifest, forbiddenValues);
        if (verified["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT")
            throw new B6PackageException("Restore requires a verified BASE_SNAPSHOT manifest.");
        var rows = LoadRows(snapshot);
        var metadata = CanonicalJson.Read<SnapshotMetadata>(Path.Combine(snapshot, "snapshot-metadata.json"));
        var owned = await Runtime.LoadAsync(runtimeState, "ANALYSIS");
        await using var connection = owned.Connection;
        if (metadata.SourceDatabaseIdentityEvidence == owned.State.DatabaseIdentityEvidence)
            throw new B6PackageException("Source and destination database identity are equal; destructive restore refused.");
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await ExecuteAsync(connection, transaction, $"DROP SCHEMA IF EXISTS {B6Values.QuoteIdentifier(schema)} CASCADE");
            await ExecuteAsync(connection, transaction, $"CREATE SCHEMA {B6Values.QuoteIdentifier(schema)}");
            foreach (var table in _allowlist.Value.Tables)
            {
                var columns = table.Fields.Select(field =>
                    $"{B6Values.QuoteIdentifier(field.Output)} {B6Values.SqlType(field.Type)}" +
                    (field.Type.EndsWith("?", StringComparison.Ordinal) ? "" : " NOT NULL"));
                var primary = string.Join(", ", table.PrimaryKey.Select(B6Values.QuoteIdentifier));
                await ExecuteAsync(connection, transaction,
                    $"CREATE TABLE {B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(table.SourceTable)} " +
                    $"({string.Join(", ", columns)}, PRIMARY KEY ({primary}))");
                var projection = string.Join(", ", table.Fields.Select(field => B6Values.QuoteIdentifier(field.Output)));
                await using var importer = await connection.BeginBinaryImportAsync(
                    $"COPY {B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(table.SourceTable)} ({projection}) FROM STDIN (FORMAT BINARY)");
                foreach (var row in rows[table.SourceTable])
                {
                    await importer.StartRowAsync();
                    foreach (var field in table.Fields) await WriteValueAsync(importer, row[field.Output], field.Type);
                }
                await importer.CompleteAsync();
            }
            await ExecuteAsync(connection, transaction,
                $"CREATE TABLE {B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(SnapshotAuthorityTable)} " +
                "(\"SnapshotId\" text PRIMARY KEY, \"SourceSnapshotDigest\" text NOT NULL, \"BaseManifestDigest\" text NOT NULL)");
            await using (var authority = new NpgsqlCommand(
                $"INSERT INTO {B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(SnapshotAuthorityTable)} " +
                "(\"SnapshotId\",\"SourceSnapshotDigest\",\"BaseManifestDigest\") VALUES (@snapshot,@source,@base)",
                connection, transaction))
            {
                authority.Parameters.AddWithValue("snapshot", verified["snapshotId"]!.GetValue<string>());
                authority.Parameters.AddWithValue("source", verified["overallSnapshotDigest"]!.GetValue<string>());
                authority.Parameters.AddWithValue("base", verified["manifestHash"]!.GetValue<string>());
                await authority.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task VerifyDatabaseAsync(string runtimeState, string schema, string manifestPath)
    {
        ValidateSchemaName(schema);
        var manifest = CanonicalJson.Read(manifestPath).AsObject();
        VerifySelfHash(manifest);
        RequireBaseManifestDatabaseAuthority(manifest);
        var owned = await Runtime.LoadAsync(runtimeState, "ANALYSIS");
        await using var connection = owned.Connection;
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await ExecuteAsync(connection, transaction, "SET TRANSACTION READ ONLY");
        await VerifyDatabaseAsync(connection, transaction, schema, manifest);
        await transaction.RollbackAsync();
    }

    private async Task VerifyDatabaseAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, JsonObject manifest)
    {
        RequireBaseManifestDatabaseAuthority(manifest);
        var manifestTables = manifest["tables"]!.AsArray().ToDictionary(
            item => item!["table"]!.GetValue<string>(), item => item!.AsObject(), StringComparer.Ordinal);
        var actualTables = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema=@schema AND table_type='BASE TABLE'", connection, transaction))
        {
            command.Parameters.AddWithValue("schema", schema);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) actualTables.Add(reader.GetString(0));
        }
        var expectedTables = _allowlist.Value.Tables.Select(table => table.SourceTable).Append(SnapshotAuthorityTable)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualTables.SetEquals(expectedTables)) throw new B6PackageException("Restored table set mismatch.");
        await using (var authority = new NpgsqlCommand(
            $"SELECT \"SnapshotId\",\"SourceSnapshotDigest\",\"BaseManifestDigest\" FROM " +
            $"{B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(SnapshotAuthorityTable)}", connection, transaction))
        await using (var reader = await authority.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync() ||
                reader.GetString(0) != manifest["snapshotId"]!.GetValue<string>() ||
                reader.GetString(1) != manifest["overallSnapshotDigest"]!.GetValue<string>() ||
                reader.GetString(2) != manifest["manifestHash"]!.GetValue<string>() ||
                await reader.ReadAsync())
                throw new B6PackageException("Restored schema is not bound to the verified snapshot/base manifest authority.");
        }
        var restored = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal);
        foreach (var table in _allowlist.Value.Tables)
        {
            var actualColumns = new List<(string Name, string Type, bool Nullable)>();
            await using (var command = new NpgsqlCommand(
                "SELECT column_name,data_type,is_nullable='YES' FROM information_schema.columns " +
                "WHERE table_schema=@schema AND table_name=@table ORDER BY ordinal_position", connection, transaction))
            {
                command.Parameters.AddWithValue("schema", schema); command.Parameters.AddWithValue("table", table.SourceTable);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) actualColumns.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
            }
            var expectedColumns = table.Fields.Select(field => (field.Output, B6Values.SqlType(field.Type), field.Type.EndsWith("?", StringComparison.Ordinal))).ToList();
            if (!actualColumns.SequenceEqual(expectedColumns))
                throw new B6PackageException($"Restored PostgreSQL type/nullability contract mismatch for {table.SourceTable}.");
            var primary = new List<string>();
            await using (var command = new NpgsqlCommand(
                "SELECT a.attname FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace " +
                "JOIN unnest(c.conkey) WITH ORDINALITY k(attnum,ord) ON true JOIN pg_attribute a ON a.attrelid=r.oid AND a.attnum=k.attnum " +
                "WHERE c.contype='p' AND n.nspname=@schema AND r.relname=@table ORDER BY k.ord", connection, transaction))
            {
                command.Parameters.AddWithValue("schema", schema); command.Parameters.AddWithValue("table", table.SourceTable);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) primary.Add(reader.GetString(0));
            }
            if (!primary.SequenceEqual(table.PrimaryKey))
                throw new B6PackageException($"Restored primary-key/uniqueness contract mismatch for {table.SourceTable}.");
            await using (var unique = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_index i JOIN pg_class r ON r.oid=i.indrelid JOIN pg_namespace n ON n.oid=r.relnamespace " +
                "WHERE n.nspname=@schema AND r.relname=@table AND i.indisprimary AND i.indisunique", connection, transaction))
            {
                unique.Parameters.AddWithValue("schema", schema); unique.Parameters.AddWithValue("table", table.SourceTable);
                if (Convert.ToInt64(await unique.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 1)
                    throw new B6PackageException($"Restored primary key is not backed by exact PostgreSQL uniqueness for {table.SourceTable}.");
            }
            var rowCount = Convert.ToInt64(manifestTables[table.SourceTable]["rowCount"]!.GetValue<int>(), CultureInfo.InvariantCulture);
            await using (var count = new NpgsqlCommand(
                $"SELECT COUNT(*) FROM {B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(table.SourceTable)}", connection, transaction))
                if (Convert.ToInt64(await count.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != rowCount)
                    throw new B6PackageException($"Restored row count mismatch for {table.SourceTable}.");
            var order = string.Join(", ", table.PrimaryKey.Select(B6Values.QuoteIdentifier));
            var projection = string.Join(", ", table.Fields.Select(field => B6Values.QuoteIdentifier(field.Output)));
            var tableRows = new List<Dictionary<string, string>>();
            await using (var command = new NpgsqlCommand(
                $"SELECT {projection} FROM {B6Values.QuoteIdentifier(schema)}.{B6Values.QuoteIdentifier(table.SourceTable)} ORDER BY {order}", connection, transaction))
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (var index = 0; index < table.Fields.Count; index++)
                        row[table.Fields[index].Output] = B6Values.CanonicalScalar(FormatDatabaseValue(reader, index), table.Fields[index].Type, table.Fields[index].Transform);
                    tableRows.Add(row);
                }
            restored[table.SourceTable] = tableRows;
            var digest = B6Values.Sha256Hex(B6Digests.CanonicalTableBytes(table, tableRows));
            if (digest != manifestTables[table.SourceTable]["canonicalDigest"]!.GetValue<string>())
                throw new B6PackageException($"Restored canonical digest mismatch for {table.SourceTable}.");
            var schemaDigest = B6Digests.Tuples(table.Fields.Select(field => new[]
                { field.Output, B6Values.SqlType(field.Type), field.Type.EndsWith("?", StringComparison.Ordinal) ? "NULL" : "NOT_NULL" })
                .Append(new[] { "PRIMARY_KEY", string.Join(",", table.PrimaryKey), "UNIQUE" }));
            if (schemaDigest != manifestTables[table.SourceTable]["schemaContractDigest"]!.GetValue<string>())
                throw new B6PackageException($"Restored schema evidence digest mismatch for {table.SourceTable}.");
        }
        ValidateTypes(restored); ValidateRelationships(restored);
        if (RelationshipDigest(restored) != manifest["relationshipDigest"]!.GetValue<string>() ||
            RecruiterCorrectionDigest(restored) != manifest["recruiterCorrectionDigest"]!.GetValue<string>())
            throw new B6PackageException("Restored relationship/recruiter digest mismatch.");
    }

    private static void ValidateSchemaName(string schema)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^b6_[a-z0-9_]{1,40}$"))
            throw new B6PackageException("Analysis schema must use the b6_ prefix.");
    }

    private static void RequireBaseManifestDatabaseAuthority(JsonObject manifest)
    {
        if (manifest["manifestStage"]?.GetValue<string>() != "BASE_SNAPSHOT" ||
            !B6Contract.SnapshotId.IsMatch(manifest["snapshotId"]?.GetValue<string>() ?? "") ||
            !B6Contract.Sha256.IsMatch(manifest["overallSnapshotDigest"]?.GetValue<string>() ?? "") ||
            !B6Contract.Sha256.IsMatch(manifest["manifestHash"]?.GetValue<string>() ?? ""))
            throw new B6PackageException("Database verification requires complete BASE_SNAPSHOT authority.");
        B6Values.RequireCutoffNotAfterObservation(
            manifest["analysisSnapshotCutoff"]?.GetValue<string>() ?? "",
            manifest["sourceObservedAt"]?.GetValue<string>() ?? "");
    }

    private static async Task WriteValueAsync(NpgsqlBinaryImporter importer, string value, string type)
    {
        if (value == B6Contract.Null) { await importer.WriteNullAsync(); return; }
        switch (type.TrimEnd('?'))
        {
            case "int": await importer.WriteAsync(int.Parse(value, CultureInfo.InvariantCulture), NpgsqlDbType.Integer); break;
            case "long": await importer.WriteAsync(long.Parse(value, CultureInfo.InvariantCulture), NpgsqlDbType.Bigint); break;
            case "bool": await importer.WriteAsync(bool.Parse(value), NpgsqlDbType.Boolean); break;
            case "uuid": await importer.WriteAsync(Guid.Parse(value), NpgsqlDbType.Uuid); break;
            case "timestamp": await importer.WriteAsync(B6Values.ParseTimestamp(value), NpgsqlDbType.TimestampTz); break;
            case "string": await importer.WriteAsync(value, NpgsqlDbType.Text); break;
            default: throw new B6PackageException($"Unsupported restore value type {type}.");
        }
    }
}
