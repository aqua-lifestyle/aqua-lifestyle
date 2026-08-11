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
    public class AreaBoundaryMigrationPostgreSqlTests : IAsyncLifetime
    {
        private const string PreviousMigration = "20260809201814_AddCommissionTermsVersions";
        private const string AreaMigration = "20260811150251_SeparateAreaFromTenantBoundary";
        private readonly string _containerName = $"area-boundary-test-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName = $"area_boundary_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public AreaBoundaryMigrationPostgreSqlTests()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            await RunDockerAsync(
                $"run -d --name {_containerName} -e POSTGRES_DB=postgres -e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle -p {_hostPort}:5432 postgres:16-alpine");
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

            await ExecuteAsync(AdminConnectionString,
                $"CREATE DATABASE \"{_databaseName}\" WITH OWNER = aqualifestyle;");
            await MigrateAsync(PreviousMigration);
        }

        public async Task DisposeAsync() =>
            await RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);

        [Fact]
        public async Task ProductionShape_BackfillsJohannesburgWithoutChangingTenantTopology()
        {
            await SeedProductionShapeAsync();
            await MigrateAsync(AreaMigration);

            (await ScalarAsync("SELECT COUNT(*) FROM \"Areas\" WHERE \"TenantId\" = 1 AND \"Code\" = 'JHB' AND \"Name\" = 'Johannesburg' AND \"IsActive\";"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM \"Customers\" WHERE \"TenantId\" = 1 AND \"AreaId\" IS NOT NULL;"))
                .ShouldBe(10);
            (await ScalarAsync("SELECT COUNT(*) FROM \"CustomerAreaAssignments\" WHERE \"TenantId\" = 1 AND \"EffectiveTo\" IS NULL AND \"IsMigrationBaseline\";"))
                .ShouldBe(10);
            (await ScalarAsync("SELECT COUNT(*) FROM \"EntryParticipations\" p JOIN \"Customers\" c ON c.\"Id\" = p.\"CustomerId\" AND c.\"TenantId\" = p.\"TenantId\" WHERE p.\"TenantId\" = 1 AND c.\"AreaId\" IS NOT NULL;"))
                .ShouldBe(5);
            (await ScalarAsync("SELECT COUNT(*) FROM \"AreaAdminAssignments\" WHERE \"TenantId\" = 1 AND \"RevokedAt\" IS NULL;"))
                .ShouldBe(3);
            (await ScalarAsync("SELECT COUNT(*) FROM \"AbpTenants\";"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM \"AbpTenants\" WHERE \"Id\" = 1 AND \"TenancyName\" = 'Default';"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM \"OnyxParticipations\";"))
                .ShouldBe(0);

            var duplicateCode = await Should.ThrowAsync<PostgresException>(() =>
                ExecuteAsync(TestConnectionString,
                    "INSERT INTO \"Areas\" (\"Id\", \"TenantId\", \"Code\", \"Name\", \"IsActive\", \"CreationTime\", \"IsDeleted\") VALUES ('b0000000-0000-0000-0000-000000000001', 1, 'JHB', 'Duplicate Johannesburg', TRUE, NOW(), FALSE);"));
            duplicateCode.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);

            await ExecuteAsync(TestConnectionString,
                """
                INSERT INTO "AbpTenants" ("Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES (2, 'Second', 'Second', TRUE, NOW(), FALSE);
                INSERT INTO "Areas" ("Id", "TenantId", "Code", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES ('b0000000-0000-0000-0000-000000000002', 2, 'JHB', 'Johannesburg', TRUE, NOW(), FALSE);
                """);
            var crossTenant = await Should.ThrowAsync<PostgresException>(() =>
                ExecuteAsync(TestConnectionString,
                    "UPDATE \"Customers\" SET \"AreaId\" = 'b0000000-0000-0000-0000-000000000002' WHERE \"Id\" = 1;"));
            crossTenant.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        }

        [Fact]
        public async Task EmptyDatabase_AppliesAndRollsBackWithoutInventingAnArea()
        {
            await MigrateAsync(AreaMigration);
            (await ScalarAsync("SELECT COUNT(*) FROM \"Areas\";")).ShouldBe(0);

            await MigrateAsync(PreviousMigration);
            (await ScalarAsync("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Areas';"))
                .ShouldBe(0);
        }

        private async Task SeedProductionShapeAsync()
        {
            await ExecuteAsync(TestConnectionString,
                """
                INSERT INTO "AbpTenants" ("Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES (1, 'Default', 'Default', TRUE, NOW(), FALSE);

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id, 1, 'member' || id, 'member' || id || '@example.test',
                    'Member', id::text, 'MEMBER' || id,
                    'MEMBER' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(101, 110) id;

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id, 1, 'administrator' || id, 'administrator' || id || '@example.test',
                    'Area', 'Administrator', 'ADMINISTRATOR' || id,
                    'ADMINISTRATOR' || id || '@EXAMPLE.TEST', 'test-password', 4,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(201, 203) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "IsActive", "CreationTime",
                    "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id - 100, 1, 'Customer ' || id,
                    'customer' || id || '@example.test', TRUE, NOW(),
                    'CLB-AREA-' || id, id, FALSE
                FROM generate_series(101, 110) id;

                INSERT INTO "AbpRoles" (
                    "Id", "TenantId", "Name", "DisplayName", "NormalizedName",
                    "IsStatic", "IsDefault", "CreationTime", "IsDeleted")
                VALUES
                    (9001, 1, 'SystemAdmin', 'System Admin', 'SYSTEMADMIN', FALSE, FALSE, NOW(), FALSE),
                    (9002, 1, 'Admin', 'Admin', 'ADMIN', TRUE, FALSE, NOW(), FALSE);

                INSERT INTO "AbpUserRoles" ("TenantId", "UserId", "RoleId", "CreationTime")
                VALUES (1, 201, 9001, NOW()), (1, 202, 9001, NOW()), (1, 203, 9002, NOW());

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    md5('area-participation:' || id::text)::uuid, 1, id, 2,
                    TIMESTAMPTZ '2026-08-01 00:00:00+00', 'entry-2026-07',
                    TIMESTAMPTZ '2026-07-26 00:00:00+00', 1200.00, 600.00,
                    600.00, 600.00, 600.00, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 5) id;
                """);
        }

        private async Task MigrateAsync(string migration)
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(TestConnectionString)
                    .Options);
            await context.GetService<IMigrator>().MigrateAsync(migration);
        }

        private async Task<long> ScalarAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(TestConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task RunDockerAsync(string arguments, bool throwOnFailure = true)
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

        private string AdminConnectionString =>
            $"Host=localhost;Port={_hostPort};Database=postgres;Username=aqualifestyle;Password=aqualifestyle";

        private string TestConnectionString =>
            $"Host=localhost;Port={_hostPort};Database={_databaseName};Username=aqualifestyle;Password=aqualifestyle";
    }
}
