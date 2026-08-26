using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AQGreenPlacementAllocatorPostgreSqlCollection
        : ICollectionFixture<AQGreenPlacementAllocatorPostgreSqlFixture>
    {
        public const string Name = "AQGreen placement allocator PostgreSQL";
    }

    public sealed class AQGreenPlacementAllocatorPostgreSqlFixture : IAsyncLifetime
    {
        private readonly string _containerName =
            $"aqgreen-placement-allocator-pg-{Guid.NewGuid():N}";
        private readonly string _templateDatabase =
            $"aqgreen_allocator_template_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public AQGreenPlacementAllocatorPostgreSqlFixture()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            await RunDockerAsync(
                $"run -d --name {_containerName} -e POSTGRES_DB=postgres " +
                "-e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle " +
                $"-p {_hostPort}:5432 postgres:16-alpine");

            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(AdminConnectionString);
                    await connection.OpenAsync();
                    break;
                }
                catch when (attempt < 29)
                {
                    await Task.Delay(1000);
                }
            }

            await ExecuteAdminAsync(
                $"CREATE DATABASE \"{_templateDatabase}\" WITH OWNER = aqualifestyle;");
            await using (var context = new AqualLifeStyleDbContext(
                             new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                                 .UseNpgsql(BuildConnectionString(_templateDatabase))
                                 .Options))
            {
                await context.GetService<IMigrator>().MigrateAsync();
            }

            await SeedParticipantsAsync(BuildConnectionString(_templateDatabase));
        }

        public Task DisposeAsync() =>
            RunDockerAsync($"rm -fv {_containerName}", throwOnFailure: false);

        public async Task<DatabaseLease> CreateDatabaseAsync()
        {
            var databaseName = $"aqgreen_allocator_test_{Guid.NewGuid():N}";
            await ExecuteAdminAsync(
                $"CREATE DATABASE \"{databaseName}\" TEMPLATE \"{_templateDatabase}\";");
            return new DatabaseLease(this, databaseName);
        }

        public AqualLifeStyleDbContext CreateDbContext(NpgsqlConnection connection) =>
            new(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(connection)
                    .Options);

        public async Task WaitForAdvisoryWaitersAsync(
            DatabaseLease lease,
            int expectedCount,
            params string[] applicationNames)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var connection = new NpgsqlConnection(lease.ConnectionString());
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    """
                    SELECT COUNT(*)
                    FROM pg_catalog.pg_stat_activity
                    WHERE datname = @databaseName
                      AND application_name = ANY(@applicationNames)
                      AND wait_event_type = 'Lock'
                      AND wait_event = 'advisory';
                    """,
                    connection);
                command.Parameters.AddWithValue("databaseName", lease.DatabaseName);
                command.Parameters.AddWithValue("applicationNames", applicationNames);
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) == expectedCount)
                    return;

                await Task.Delay(50);
            }

            throw new TimeoutException(
                $"Expected {expectedCount} allocator transaction(s) to wait on an advisory lock.");
        }

        public static Guid Participant(int tenantId, int number) =>
            Guid.Parse($"{tenantId:D8}-0000-0000-0000-{number:D12}");

        private async Task DropDatabaseAsync(string databaseName)
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);");
        }

        private async Task ExecuteAdminAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(AdminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task SeedParticipantsAsync(string connectionString)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO "AbpTenants" (
                    "Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES
                    (1, 'allocator-one', 'Allocator One', TRUE, NOW(), FALSE),
                    (2, 'allocator-two', 'Allocator Two', TRUE, NOW(), FALSE);

                INSERT INTO "Areas" (
                    "Id", "TenantId", "Code", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES
                    ('c0000000-0000-0000-0000-000000000001', 1, 'EAST', 'East', TRUE, NOW(), FALSE),
                    ('c0000000-0000-0000-0000-000000000002', 1, 'WEST', 'West', TRUE, NOW(), FALSE),
                    ('c0000000-0000-0000-0000-000000000003', 2, 'OTHER', 'Other', TRUE, NOW(), FALSE);

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id + 3000, 1, 'allocator-user-' || id,
                    'allocator-user-' || id || '@example.test', 'Allocator', id::text,
                    'ALLOCATOR-USER-' || id,
                    'ALLOCATOR-USER-' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(1, 64) id;

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id + 4000, 2, 'allocator-user-two-' || id,
                    'allocator-user-two-' || id || '@example.test', 'Allocator Two', id::text,
                    'ALLOCATOR-USER-TWO-' || id,
                    'ALLOCATOR-USER-TWO-' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(1, 16) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id, 1, 'Allocator Customer ' || id,
                    'allocator-customer-' || id || '@example.test',
                    CASE WHEN id % 2 = 1
                        THEN 'c0000000-0000-0000-0000-000000000001'::uuid
                        ELSE 'c0000000-0000-0000-0000-000000000002'::uuid END,
                    TRUE, NOW(), 'A1-CLB-' || id, id + 3000, FALSE
                FROM generate_series(1, 64) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id + 100, 2, 'Allocator Tenant Two ' || id,
                    'allocator-two-' || id || '@example.test',
                    'c0000000-0000-0000-0000-000000000003'::uuid,
                    TRUE, NOW(), 'A2-CLB-' || id, id + 4000, FALSE
                FROM generate_series(1, 16) id;

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    ('00000001-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
                    1, id, 2, TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    'entry-terms-v1', TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 64) id;

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    ('00000002-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
                    2, id + 100, 2, TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    'entry-terms-v1', TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 16) id;

                INSERT INTO "ProgrammeInvitations" (
                    "Id", "TenantId", "ProgrammeKey", "ProgrammeParticipationId",
                    "Code", "CreationTime", "IsDeleted")
                SELECT
                    "Id", "TenantId", 'AQGREEN', "Id",
                    'B' || "TenantId"::text || lpad("CustomerId"::text, 10, '0'),
                    NOW(), FALSE
                FROM "EntryParticipations";
                """,
                connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task RunDockerAsync(
            string arguments,
            bool throwOnFailure = true)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process.ShouldNotBeNull();
            await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (throwOnFailure && process.ExitCode != 0)
                throw new InvalidOperationException($"Docker command failed: {error}");
        }

        private string BuildConnectionString(string databaseName, string applicationName = null)
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = _hostPort,
                Database = databaseName,
                Username = "aqualifestyle",
                Password = "aqualifestyle",
                Pooling = false
            };
            if (!string.IsNullOrWhiteSpace(applicationName))
                builder.ApplicationName = applicationName;
            return builder.ConnectionString;
        }

        private string AdminConnectionString => BuildConnectionString("postgres");

        public sealed class DatabaseLease : IAsyncDisposable
        {
            private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;

            internal DatabaseLease(
                AQGreenPlacementAllocatorPostgreSqlFixture fixture,
                string databaseName)
            {
                _fixture = fixture;
                DatabaseName = databaseName;
            }

            public string DatabaseName { get; }

            public string ConnectionString(string applicationName = null) =>
                _fixture.BuildConnectionString(DatabaseName, applicationName);

            public async ValueTask DisposeAsync()
            {
                await _fixture.DropDatabaseAsync(DatabaseName);
            }
        }
    }
}
