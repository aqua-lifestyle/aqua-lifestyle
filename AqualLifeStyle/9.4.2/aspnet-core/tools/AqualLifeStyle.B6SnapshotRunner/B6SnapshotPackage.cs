using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Npgsql;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed partial class B6SnapshotPackage
{
    private readonly B6Allowlist _allowlist;
    private readonly ToolchainIdentity _toolchainIdentity;

    public B6SnapshotPackage(string? allowlistPath = null)
    {
        _allowlist = new B6Allowlist(allowlistPath);
        _toolchainIdentity = ToolchainIdentity.Capture();
        Runtime = new OwnedPostgreSqlRuntime(_toolchainIdentity);
    }

    public OwnedPostgreSqlRuntime Runtime { get; }

    public void GenerateKey(string output) =>
        SecureFiles.WriteNew(output, stream => stream.Write(RandomNumberGenerator.GetBytes(32)));

    public async Task ExtractAsync(ExtractionRequest request)
    {
        if (!request.AuthorizedOperatorAcknowledged)
            throw new B6PackageException("Extraction requires --authorized-operator-ack after external authorization.");
        if (!B6Contract.ControlledCode.IsMatch(request.SourceSystemId))
            throw new B6PackageException("Source system identifier must be an approved opaque controlled code.");
        if (!B6Contract.GitSha.IsMatch(request.RepositoryCommit) || request.RepositoryCommit != _toolchainIdentity.RepositoryCommit)
            throw new B6PackageException("Requested repository commit does not equal the executing tool worktree commit.");
        if (request.TenantId <= 0 || request.PopulationScope != B6Contract.PopulationScope)
            throw new B6PackageException("Explicit positive Tenant and full Tenant AQGreen population scope are required.");

        await using var connection = new NpgsqlConnection(request.Dsn);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await ExecuteAsync(connection, transaction, "SET TRANSACTION READ ONLY");
        await ExecuteAsync(connection, transaction, "SET LOCAL statement_timeout = '10min'");
        await ExecuteAsync(connection, transaction, "SET LOCAL lock_timeout = '5s'");
        await ExecuteAsync(connection, transaction, "SET LOCAL search_path = pg_catalog, public");

        SourceObservation observation;
        await using (var command = new NpgsqlCommand(
            "SELECT current_setting('transaction_read_only'), transaction_timestamp(), " +
            "current_setting('server_version'), current_setting('server_version_num')::int, " +
            "current_setting('server_encoding'), (SELECT datcollate FROM pg_database WHERE datname=current_database()), " +
            "txid_current_snapshot()::text, (SELECT system_identifier::text FROM pg_control_system()), " +
            "current_database(), (SELECT oid::text FROM pg_database WHERE datname=current_database())", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new B6PackageException("Source observation evidence is absent.");
            observation = new SourceObservation(
                reader.GetString(0), reader.GetFieldValue<DateTime>(1), reader.GetString(2), reader.GetInt32(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9));
        }
        if (observation.ReadOnly != "on") throw new B6PackageException("Source transaction is not read-only.");
        B6SourceSchema.RequireVersion(observation.VersionNumber);
        if (request.AnalysisCutoff > new DateTimeOffset(DateTime.SpecifyKind(observation.ObservedAt, DateTimeKind.Utc)))
            throw new B6PackageException("AnalysisSnapshotCutoff cannot be later than source observation.");

        await B6SourceSchema.RequireColumnsAsync(connection, transaction, _allowlist);
        var migrations = new List<(string Id, string ProductVersion)>();
        await using (var command = new NpgsqlCommand(
            "SELECT \"MigrationId\", \"ProductVersion\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) migrations.Add((reader.GetString(0), reader.GetString(1)));
        B6SourceSchema.RequireHistory(migrations);

        // No application rows or raw files before version/history/required-column
        // compatibility has been established inside this same read-only snapshot.
        SecureFiles.CreatePrivateDirectory(request.Output);
        foreach (var table in _allowlist.Value.Tables)
        {
            var projections = table.SourceFields.Select(field =>
            {
                var source = $"t.{B6Values.QuoteIdentifier(field.SourceColumns.Single())}";
                var expression = field.Transform == "presence-bool" ? $"({source} IS NOT NULL)" : source;
                return $"{expression} AS {B6Values.QuoteIdentifier(field.Output)}";
            });
            var order = string.Join(", ", table.PrimaryKey.Select(B6Values.QuoteIdentifier));
            var query = ScopedExtractQuery(table.SourceTable, projections, order);
            var rows = new List<IReadOnlyDictionary<string, string>>();
            await using var command = new NpgsqlCommand(query, connection, transaction);
            command.Parameters.AddWithValue("tenant", request.TenantId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var index = 0; index < table.SourceFields.Count; index++)
                    row.Add(table.SourceFields[index].Output, FormatDatabaseValue(reader, index));
                rows.Add(row);
            }
            CsvContract.WriteNew(Path.Combine(request.Output, $"{table.SourceTable}.csv"),
                table.SourceFields.Select(field => field.Output).ToList(), rows);
        }

        var extensions = new List<string>();
        await using (var command = new NpgsqlCommand(
            "SELECT extname || ':' || extversion FROM pg_extension ORDER BY extname", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) extensions.Add(reader.GetString(0));

        var metadata = new SourceMetadata
        {
            PackageSchemaVersion = B6Contract.PackageVersion,
            SourceSystemIdentifier = request.SourceSystemId,
            RepositoryCommit = request.RepositoryCommit,
            ExportToolVersion = B6Contract.PackageVersion,
            AllowlistDigest = _allowlist.Digest,
            AnalysisSnapshotCutoff = B6Values.Timestamp(request.AnalysisCutoff),
            SourceObservedAt = B6Values.Timestamp(observation.ObservedAt),
            SourcePostgreSqlMajorVersion = observation.VersionNumber / 10000,
            SourcePostgreSqlVersion = observation.Version,
            SourceServerEncoding = observation.Encoding,
            SourceDatabaseCollation = observation.Collation,
            SourceExtensions = extensions,
            SourceMvccSnapshot = observation.Mvcc,
            SourceDatabaseIdentityEvidence = B6Values.Sha256Hex($"{observation.SystemId}|{observation.Database}|{observation.DatabaseOid}"),
            TransactionMode = "REPEATABLE READ READ ONLY",
            AuthorizedTenantId = request.TenantId,
            PopulationScope = request.PopulationScope,
            SourceSchemaVersion = B6Values.Sha256Hex(string.Join('\n', migrations.Select(row => $"{row.Id}|{row.ProductVersion}"))),
            EfMigrationHead = migrations.LastOrDefault().Id,
            ExtractedAt = B6Values.Timestamp(DateTimeOffset.UtcNow),
            ToolchainIdentity = _toolchainIdentity
        };
        CanonicalJson.WriteNew(Path.Combine(request.Output, "source-metadata.json"), CanonicalJson.ToNode(metadata));
        await transaction.RollbackAsync();
    }

    public void Sanitize(string input, string output, string keyFile, string snapshotId, DateTimeOffset? createdAt)
    {
        RequireExpectedFiles(input, "source-metadata.json", source: true);
        SecureFiles.CreatePrivateDirectory(output);
        byte[] secret;
        using (var stream = SecureFiles.OpenRead(keyFile, controlled: true))
        using (var memory = new MemoryStream()) { stream.CopyTo(memory); secret = memory.ToArray(); }
        var pseudonymizer = new Pseudonymizer(secret);
        CryptographicOperations.ZeroMemory(secret);
        var raw = LoadRows(input, source: true);
        var namespaceValues = new Dictionary<(string Kind, string Namespace), HashSet<string>>();
        foreach (var table in _allowlist.Value.Tables)
        foreach (var field in table.SourceFields)
        {
            var match = Regex.Match(field.Transform, "^pseudonym-(uuid|int):(.+)$");
            if (!match.Success) continue;
            var key = (match.Groups[1].Value, match.Groups[2].Value);
            if (!namespaceValues.TryGetValue(key, out var values)) namespaceValues[key] = values = new(StringComparer.Ordinal);
            foreach (var row in raw[table.SourceTable]) values.Add(row[field.Output]);
        }
        foreach (var item in namespaceValues) pseudonymizer.Prepare(item.Key.Kind, item.Key.Namespace, item.Value);

        var ordinals = new Dictionary<(string Tenant, string Id), long>();
        foreach (var group in raw["EntryParticipations"].GroupBy(row => row["TenantId"], StringComparer.Ordinal))
        {
            var ordered = group.Select(row => row["Id"]).Select(value =>
                Guid.TryParse(value, out var guid) ? (Text: value, Guid: guid) : throw new B6PackageException("Source ParticipationId is not a UUID."))
                .OrderBy(value => value.Guid).ToList();
            if (ordered.Select(value => value.Guid).Distinct().Count() != ordered.Count)
                throw new B6PackageException("Duplicate source ParticipationId prevents ordinal derivation.");
            for (var index = 0; index < ordered.Count; index++) ordinals[(group.Key, ordered[index].Text)] = index + 1;
        }

        foreach (var table in _allowlist.Value.Tables)
        {
            var transformed = new List<IReadOnlyDictionary<string, string>>();
            foreach (var row in raw[table.SourceTable])
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var field in table.Fields)
                {
                    var rawValue = field.Transform == "legacy-participation-order-ordinal"
                        ? ordinals[(row["TenantId"], row["Id"])].ToString(CultureInfo.InvariantCulture)
                        : row[field.Output];
                    var match = Regex.Match(field.Transform, "^pseudonym-(uuid|int):(.+)$");
                    result[field.Output] = match.Success
                        ? pseudonymizer.Map(match.Groups[1].Value, match.Groups[2].Value, rawValue)
                        : B6Values.CanonicalScalar(rawValue, field.Type, field.Transform);
                }
                transformed.Add(result);
            }
            CsvContract.WriteNew(Path.Combine(output, $"{table.SourceTable}.csv"),
                table.Fields.Select(field => field.Output).ToList(), transformed);
        }

        var source = CanonicalJson.Read<SourceMetadata>(Path.Combine(input, "source-metadata.json"));
        ValidateSourceMetadata(source);
        if (!B6Contract.SnapshotId.IsMatch(snapshotId)) throw new B6PackageException("SnapshotId is not an approved opaque identifier.");
        var metadata = new SnapshotMetadata
        {
            SnapshotId = snapshotId, AnalysisSnapshotCutoff = B6Values.Timestamp(B6Values.ParseTimestamp(source.AnalysisSnapshotCutoff)),
            CreatedAt = B6Values.Timestamp(createdAt ?? DateTimeOffset.UtcNow), SourceObservedAt = B6Values.Timestamp(B6Values.ParseTimestamp(source.SourceObservedAt)),
            SourcePostgreSqlMajorVersion = source.SourcePostgreSqlMajorVersion, SourcePostgreSqlVersion = source.SourcePostgreSqlVersion,
            SourceServerEncoding = source.SourceServerEncoding, SourceDatabaseCollation = source.SourceDatabaseCollation,
            SourceExtensions = source.SourceExtensions, SourceSystemIdentifier = source.SourceSystemIdentifier,
            RepositoryCommit = source.RepositoryCommit, ExportToolVersion = source.ExportToolVersion,
            SourceSchemaVersion = source.SourceSchemaVersion, EfMigrationHead = source.EfMigrationHead,
            SourceMvccSnapshotEvidence = B6Values.Sha256Hex(source.SourceMvccSnapshot),
            SourceDatabaseIdentityEvidence = source.SourceDatabaseIdentityEvidence, TransactionMode = source.TransactionMode,
            AuthorizedTenantSnapshotKey = int.Parse(pseudonymizer.Map("int", "Tenant", source.AuthorizedTenantId.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture),
            PopulationScope = source.PopulationScope, AllowlistVersion = _allowlist.Value.AllowlistVersion,
            AllowlistDigest = _allowlist.Digest, SanitisationSchemeVersion = B6Contract.SanitisationVersion,
            OrderingComparatorVersion = B6Contract.OrderingVersion, PseudonymisationSchemeVersion = B6Contract.PseudonymVersion,
            InventoryRulesVersion = B6Contract.InventoryVersion, AnalysisAdapterVersion = B6Contract.AnalysisVersion,
            SimulatorVersion = B6Contract.SimulatorVersion, PackageSchemaVersion = B6Contract.PackageVersion,
            CanonicalEncodingVersion = B6Contract.EncodingVersion, Classification = B6Contract.Classification,
            ToolchainIdentity = source.ToolchainIdentity
        };
        CanonicalJson.WriteNew(Path.Combine(output, "snapshot-metadata.json"), CanonicalJson.ToNode(metadata));
    }

    private void ValidateSourceMetadata(SourceMetadata metadata)
    {
        if (metadata.PackageSchemaVersion != B6Contract.PackageVersion || metadata.ExportToolVersion != B6Contract.PackageVersion ||
            metadata.AllowlistDigest != _allowlist.Digest || metadata.TransactionMode != "REPEATABLE READ READ ONLY" ||
            metadata.PopulationScope != B6Contract.PopulationScope || metadata.SourcePostgreSqlMajorVersion != B6Contract.PostgreSqlMajorVersion ||
            !B6SourceSchema.VersionTextMatches(metadata.SourcePostgreSqlVersion) ||
            metadata.SourceServerEncoding != "UTF8" || !B6Contract.ControlledCode.IsMatch(metadata.SourceSystemIdentifier) ||
            !B6Contract.GitSha.IsMatch(metadata.RepositoryCommit) || metadata.RepositoryCommit != _toolchainIdentity.RepositoryCommit ||
            metadata.EfMigrationHead != B6Contract.ExpectedMigrationHead || metadata.SourceSchemaVersion != B6SourceSchema.ExpectedHistoryDigest ||
            !B6Contract.Sha256.IsMatch(metadata.SourceDatabaseIdentityEvidence) || metadata.SourceExtensions.Count == 0)
            throw new B6PackageException("Raw source metadata lacks the sealed PostgreSQL/schema/tool/scope evidence contract.");
        B6Values.RequireCutoffNotAfterObservation(metadata.AnalysisSnapshotCutoff, metadata.SourceObservedAt);
        metadata.ToolchainIdentity.RequireEqual(_toolchainIdentity);
    }

    private Dictionary<string, List<Dictionary<string, string>>> LoadRows(string directory, bool source = false)
    {
        var result = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal);
        foreach (var table in _allowlist.Value.Tables)
        {
            var fields = source ? table.SourceFields : table.Fields;
            var rows = CsvContract.Read(Path.Combine(directory, $"{table.SourceTable}.csv"),
                fields.Select(field => field.Output).ToList(), controlled: true);
            var duplicate = rows.GroupBy(row => string.Join('\0', table.PrimaryKey.Select(key => row[key])), StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
                throw new B6PackageException($"Duplicate delivered primary key rejected before materialization for {table.SourceTable}.");
            result.Add(table.SourceTable, rows);
        }
        return result;
    }

    private void RequireExpectedFiles(string directory, string metadataName, bool source = false, params string[] extras)
    {
        SecureFiles.RequireTrustedDirectory(directory);
        var expected = _allowlist.Value.Tables.Select(table => $"{table.SourceTable}.csv").ToHashSet(StringComparer.Ordinal);
        expected.Add(metadataName); expected.UnionWith(extras);
        var actual = Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
        if (!actual.SetEquals(expected)) throw new B6PackageException("Snapshot file set is not exact.");
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            using (SecureFiles.OpenRead(path, controlled: true)) { }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static string FormatDatabaseValue(NpgsqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return B6Contract.Null;
        var value = reader.GetValue(index);
        return value switch
        {
            DateTime date => B6Values.Timestamp(date),
            DateTimeOffset offset => B6Values.Timestamp(offset),
            Guid guid => guid.ToString("D"),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? throw new B6PackageException("Database value cannot be rendered.")
        };
    }

    private static string ScopedExtractQuery(string table, IEnumerable<string> projections, string order)
    {
        const string ctes = "scope AS (SELECT * FROM \"EntryParticipations\" WHERE \"TenantId\"=@tenant), " +
            "scoped_customer_ids AS (SELECT \"CustomerId\" id FROM scope UNION SELECT \"RecruiterCustomerId\" FROM scope WHERE \"RecruiterCustomerId\" IS NOT NULL " +
            "UNION SELECT c.\"PreviousRecruiterCustomerId\" FROM \"EntryRecruiterCorrections\" c JOIN scope p ON p.\"Id\"=c.\"EntryParticipationId\" WHERE c.\"PreviousRecruiterCustomerId\" IS NOT NULL " +
            "UNION SELECT c.\"NewRecruiterCustomerId\" FROM \"EntryRecruiterCorrections\" c JOIN scope p ON p.\"Id\"=c.\"EntryParticipationId\" WHERE c.\"NewRecruiterCustomerId\" IS NOT NULL " +
            "UNION SELECT c.\"CustomerId\" FROM \"EntryWeeklyCommissions\" c JOIN scope p ON p.\"Id\"=c.\"EntryParticipationId\" WHERE c.\"TenantId\"=@tenant " +
            "UNION SELECT l.\"CustomerId\" FROM \"OnyxLoanAgreements\" l JOIN scope p ON p.\"Id\"=l.\"EntryParticipationId\" WHERE l.\"TenantId\"=@tenant " +
            "UNION SELECT g.\"CustomerId\" FROM \"OnyxGraduationDecisions\" g JOIN scope p ON p.\"Id\"=g.\"EntryParticipationId\" WHERE g.\"TenantId\"=@tenant " +
            "UNION SELECT o.\"CustomerId\" FROM \"OnyxParticipations\" o JOIN scope p ON p.\"Id\"=o.\"EntryParticipationId\" WHERE o.\"TenantId\"=@tenant)";
        var condition = table switch
        {
            "AbpTenants" => "t.\"Id\"=@tenant",
            "Areas" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM \"Customers\" c JOIN scoped_customer_ids s ON s.id=c.\"Id\" WHERE c.\"AreaId\"=t.\"Id\" AND c.\"TenantId\"=@tenant)",
            "Customers" => "t.\"TenantId\"=@tenant AND t.\"Id\" IN (SELECT id FROM scoped_customer_ids)",
            "EntryParticipations" => "t.\"TenantId\"=@tenant",
            "EntryRecruiterCorrections" => "EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"EntryParticipationId\")",
            "EntryParticipationApprovalDecisions" => "EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"EntryParticipationId\")",
            "AQGreenRecruitmentAttributions" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"ParticipantId\")",
            "AQGreenRecruitmentAttributionConfirmations" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM \"AQGreenRecruitmentAttributions\" a JOIN scope p ON p.\"Id\"=a.\"ParticipantId\" WHERE a.\"Id\"=t.\"AttributionId\" AND a.\"TenantId\"=@tenant)",
            "AQGreenPlacementTreeScopes" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM \"AQGreenNetworkPlacements\" n JOIN scope p ON p.\"Id\"=n.\"ParticipantId\" WHERE n.\"PlacementTreeScopeId\"=t.\"Id\" AND n.\"TenantId\"=@tenant)",
            "AQGreenNetworkPlacements" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"ParticipantId\")",
            "ProgrammeInvitations" => "t.\"TenantId\"=@tenant AND t.\"ProgrammeKey\"='AQGREEN' AND t.\"IsDeleted\"=FALSE AND EXISTS (SELECT 1 FROM \"AQGreenRecruitmentAttributions\" a JOIN scope p ON p.\"Id\"=a.\"ParticipantId\" WHERE a.\"TenantId\"=@tenant AND a.\"AcquisitionSource\"=1 AND a.\"SourceReferenceId\"=t.\"Id\")",
            "EntryCommissionPeriods" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM \"EntryWeeklyCommissions\" c JOIN scope p ON p.\"Id\"=c.\"EntryParticipationId\" WHERE c.\"CommissionPeriodId\"=t.\"Id\" AND c.\"TenantId\"=@tenant)",
            "EntryWeeklyCommissions" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"EntryParticipationId\")",
            "OnyxLoanAgreements" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"EntryParticipationId\")",
            "OnyxGraduationDecisions" => "t.\"TenantId\"=@tenant AND EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"EntryParticipationId\")",
            "OnyxParticipations" => "t.\"TenantId\"=@tenant AND t.\"EntryParticipationId\" IS NOT NULL AND EXISTS (SELECT 1 FROM scope p WHERE p.\"Id\"=t.\"EntryParticipationId\")",
            "__EFMigrationsHistory" => "TRUE",
            _ => throw new B6PackageException($"No bounded extraction relationship exists for {table}.")
        };
        return $"WITH {ctes} SELECT {string.Join(", ", projections)} FROM {B6Values.QuoteIdentifier(table)} t WHERE {condition} ORDER BY {order}";
    }

    private sealed record SourceObservation(string ReadOnly, DateTime ObservedAt, string Version, int VersionNumber,
        string Encoding, string Collation, string Mvcc, string SystemId, string Database, string DatabaseOid);
}
