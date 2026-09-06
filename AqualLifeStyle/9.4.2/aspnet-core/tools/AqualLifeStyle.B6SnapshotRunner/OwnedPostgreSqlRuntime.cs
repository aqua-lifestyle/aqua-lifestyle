using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed class OwnedPostgreSqlRuntime
{
    private readonly ToolchainIdentity _toolchainIdentity;
    public OwnedPostgreSqlRuntime(ToolchainIdentity toolchainIdentity) => _toolchainIdentity = toolchainIdentity;

    public async Task CreateAsync(string statePath, string role)
    {
        role = role.ToUpperInvariant();
        if (role is not ("SOURCE" or "ANALYSIS"))
            throw new B6PackageException("Owned PostgreSQL runtime role must be SOURCE or ANALYSIS.");
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var name = $"aqua-b6-{role.ToLowerInvariant()}-{suffix}";
        var database = $"b6_{role.ToLowerInvariant()}_{suffix}";
        var username = $"b6_{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var environment = new Dictionary<string, string>
        {
            ["POSTGRES_USER"] = username, ["POSTGRES_DB"] = database, ["POSTGRES_PASSWORD"] = password
        };
        var containerId = ToolchainIdentity.Run("docker",
        [
            "run", "--rm", "-d", "--name", name,
            "--label", $"aqua.b6.runtime-version={B6Contract.RuntimeVersion}",
            "--label", $"aqua.b6.ownership-nonce={nonce}",
            "--label", $"aqua.b6.runtime-role={role}",
            "--env", "POSTGRES_USER", "--env", "POSTGRES_DB", "--env", "POSTGRES_PASSWORD",
            "-p", "127.0.0.1::5432", "postgres:16-alpine"
        ], environment).Trim();
        try
        {
            var portText = ToolchainIdentity.Run("docker", ["port", containerId, "5432/tcp"]).Trim();
            if (!int.TryParse(portText[(portText.LastIndexOf(':') + 1)..], out var port))
                throw new B6PackageException("Owned runtime port binding is invalid.");
            var builder = Builder("127.0.0.1", port, database, username, password);
            NpgsqlConnection? connection = null;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                try
                {
                    connection = new NpgsqlConnection(builder.ConnectionString);
                    await connection.OpenAsync();
                    break;
                }
                catch when (attempt < 59)
                {
                    if (connection != null) await connection.DisposeAsync();
                    await Task.Delay(500);
                }
            }
            if (connection == null || connection.State != System.Data.ConnectionState.Open)
                throw new B6PackageException("Owned PostgreSQL 16 runtime did not become ready.");
            await using (connection)
            {
                await using (var marker = new NpgsqlCommand(
                    "CREATE TABLE public.\"__AquaB6RuntimeOwnership\" (\"Nonce\" text PRIMARY KEY, \"RuntimeVersion\" text NOT NULL, \"RuntimeRole\" text NOT NULL); " +
                    "INSERT INTO public.\"__AquaB6RuntimeOwnership\" VALUES (@nonce,@version,@role)", connection))
                {
                    marker.Parameters.AddWithValue("nonce", nonce);
                    marker.Parameters.AddWithValue("version", B6Contract.RuntimeVersion);
                    marker.Parameters.AddWithValue("role", role);
                    await marker.ExecuteNonQueryAsync();
                }
                var evidence = await ReadIdentityAsync(connection);
                if (evidence.Major != 16) throw new B6PackageException("Owned runtime is not PostgreSQL major version 16.");
                var state = new RuntimeState
                {
                    RuntimeVersion = B6Contract.RuntimeVersion, RuntimeRole = role,
                    ContainerId = containerId, ContainerName = name, OwnershipNonce = nonce,
                    Host = "127.0.0.1", Port = port, Database = evidence.Database,
                    Username = username, Password = password,
                    DatabaseIdentityEvidence = evidence.Digest, PostgreSqlVersion = evidence.Version,
                    PostgreSqlMajorVersion = evidence.Major, WorkerBoundary = B6Contract.WorkerBoundary,
                    ToolchainIdentity = _toolchainIdentity
                };
                CanonicalJson.WriteNew(statePath, CanonicalJson.ToNode(state));
            }
        }
        catch
        {
            try { ToolchainIdentity.Run("docker", ["rm", "-f", containerId]); } catch { }
            throw;
        }
    }

    public async Task DestroyAsync(string statePath)
    {
        var owned = await LoadAsync(statePath, requiredRole: null);
        await owned.Connection.DisposeAsync();
        ToolchainIdentity.Run("docker", ["rm", "-f", owned.State.ContainerId]);
        SecureFiles.DeleteControlledFile(statePath);
    }

    public async Task MigrateFixtureAsync(string statePath)
    {
        var owned = await LoadAsync(statePath, "SOURCE");
        await using (owned.Connection)
        {
            var options = new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseNpgsql(owned.ConnectionString).Options;
            await using var context = new AqualLifeStyleDbContext(options);
            await context.GetService<IMigrator>().MigrateAsync();
        }
    }

    public async Task LoadFixtureAsync(string statePath, string sqlPath)
    {
        var owned = await LoadAsync(statePath, "SOURCE");
        await using (owned.Connection)
        using (var stream = SecureFiles.OpenRead(sqlPath, controlled: false))
        using (var reader = new StreamReader(stream))
        {
            var sql = await reader.ReadToEndAsync();
            await using var transaction = await owned.Connection.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(sql, owned.Connection, transaction) { CommandTimeout = 600 };
                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public async Task<OwnedRuntime> LoadAsync(string statePath, string? requiredRole)
    {
        var state = CanonicalJson.Read<RuntimeState>(statePath);
        if (state.RuntimeVersion != B6Contract.RuntimeVersion || state.Host != "127.0.0.1" ||
            state.WorkerBoundary != B6Contract.WorkerBoundary || state.PostgreSqlMajorVersion != 16 ||
            state.RuntimeRole is not ("SOURCE" or "ANALYSIS") ||
            requiredRole != null && state.RuntimeRole != requiredRole)
            throw new B6PackageException("Runtime state is not the required package-owned PostgreSQL 16 identity.");
        state.ToolchainIdentity.RequireEqual(_toolchainIdentity);

        var inspect = JsonNode.Parse(ToolchainIdentity.Run("docker", ["inspect", state.ContainerId]))?.AsArray();
        if (inspect == null || inspect.Count != 1) throw new B6PackageException("Owned runtime container identity is ambiguous.");
        var detail = inspect[0]!.AsObject();
        var labels = detail["Config"]?["Labels"]?.AsObject();
        var bindings = detail["HostConfig"]?["PortBindings"]?["5432/tcp"]?.AsArray();
        if (detail["Name"]?.GetValue<string>() != "/" + state.ContainerName ||
            detail["Config"]?["Image"]?.GetValue<string>() != "postgres:16-alpine" ||
            labels?["aqua.b6.runtime-version"]?.GetValue<string>() != B6Contract.RuntimeVersion ||
            labels?["aqua.b6.ownership-nonce"]?.GetValue<string>() != state.OwnershipNonce ||
            labels?["aqua.b6.runtime-role"]?.GetValue<string>() != state.RuntimeRole ||
            detail["State"]?["Running"]?.GetValue<bool>() != true || bindings == null || bindings.Count == 0 ||
            bindings.Any(binding => binding?["HostIp"]?.GetValue<string>() != "127.0.0.1"))
            throw new B6PackageException("Destination is not the exact running loopback-only package-owned runtime.");

        var connectionString = Builder(state.Host, state.Port, state.Database, state.Username, state.Password).ConnectionString;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        try
        {
            var evidence = await ReadIdentityAsync(connection);
            if (evidence.Digest != state.DatabaseIdentityEvidence || evidence.Major != 16)
                throw new B6PackageException("Owned runtime database identity/version changed.");
            await using var marker = new NpgsqlCommand(
                "SELECT COUNT(*) FROM public.\"__AquaB6RuntimeOwnership\" WHERE \"Nonce\"=@nonce AND \"RuntimeVersion\"=@version AND \"RuntimeRole\"=@role", connection);
            marker.Parameters.AddWithValue("nonce", state.OwnershipNonce);
            marker.Parameters.AddWithValue("version", B6Contract.RuntimeVersion);
            marker.Parameters.AddWithValue("role", state.RuntimeRole);
            if (Convert.ToInt64(await marker.ExecuteScalarAsync()) != 1)
                throw new B6PackageException("Database-side unpredictable ownership marker is absent.");
            return new OwnedRuntime(state, connection, connectionString);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static NpgsqlConnectionStringBuilder Builder(string host, int port, string database, string username, string password) => new()
    {
        Host = host, Port = port, Database = database, Username = username, Password = password,
        IncludeErrorDetail = false, Pooling = false, Timeout = 10, CommandTimeout = 600
    };

    private static async Task<(string Digest, string Database, string Version, int Major)> ReadIdentityAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT (SELECT system_identifier::text FROM pg_control_system()), current_database(), " +
            "(SELECT oid::text FROM pg_database WHERE datname=current_database()), current_setting('server_version'), " +
            "current_setting('server_version_num')::int / 10000", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new B6PackageException("Unable to read runtime identity.");
        var system = reader.GetString(0); var database = reader.GetString(1); var oid = reader.GetString(2);
        return (B6Values.Sha256Hex($"{system}|{database}|{oid}"), database, reader.GetString(3), reader.GetInt32(4));
    }
}

public sealed record OwnedRuntime(RuntimeState State, NpgsqlConnection Connection, string ConnectionString);
