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
    public class InternalAccountInvitationMigrationPostgreSqlTests : IAsyncLifetime
    {
        private const string PreviousMigration = "20260801092352_AddAQGreenSchedulesAndOnyxGraduation";
        private const string InvitationMigration = "20260804040549_AddInternalAccountInvitations";
        private readonly string _containerName = $"invitation-migration-test-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName = $"invitation_test_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public InternalAccountInvitationMigrationPostgreSqlTests()
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
            await SeedAdministratorRolesAsync();
        }

        public async Task DisposeAsync()
        {
            await RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);
        }

        [Fact]
        public async Task UpAndDownCreateConstraintsBackfillRolesAndRollbackSafely()
        {
            await MigrateAsync(InvitationMigration);

            (await ScalarAsync("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'InternalAccountInvitations';"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'InternalAccountInvitations' AND column_name = 'Version' AND data_type = 'integer' AND is_nullable = 'NO';"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'InternalAccountInvitations' AND indexdef LIKE 'CREATE UNIQUE INDEX%PublicCodeHash%';"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'InternalAccountInvitations' AND indexdef LIKE '%\"TenantId\"%\"UserId\"%\"CreationTime\" DESC%';"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'InternalAccountInvitations' AND indexdef LIKE 'CREATE UNIQUE INDEX%TenantId%UserId%' AND indexdef LIKE '%WHERE (\"Status\" = 0)%';"))
                .ShouldBe(1);
            (await ScalarAsync(
                """
                SELECT COUNT(*)
                FROM pg_constraint AS con
                JOIN pg_class AS source ON source.oid = con.conrelid
                JOIN pg_class AS target ON target.oid = con.confrelid
                WHERE source.relname = 'InternalAccountInvitations'
                  AND con.contype = 'f'
                  AND target.relname IN ('AbpUsers', 'InternalAccountInvitations');
                """))
                .ShouldBe(2);

            (await ScalarAsync("SELECT COUNT(*) FROM \"AbpPermissions\" WHERE \"Name\" = 'Aqua.Admin.Users.Invite' AND \"RoleId\" IN (9001, 9002) AND \"IsGranted\" = TRUE;"))
                .ShouldBe(2);
            (await ScalarAsync("SELECT COUNT(*) FROM \"AbpPermissions\" WHERE \"Name\" = 'Aqua.Admin.Users.Invite' AND \"RoleId\" = 9003 AND \"IsGranted\" = FALSE;"))
                .ShouldBe(1);
            (await ScalarAsync("SELECT COUNT(*) FROM \"AbpPermissions\" WHERE \"Name\" = 'Aqua.Admin.Users.Invite' AND \"RoleId\" = 9004;"))
                .ShouldBe(0);

            await MigrateAsync(PreviousMigration);

            (await ScalarAsync("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'InternalAccountInvitations';"))
                .ShouldBe(0);
            (await ScalarAsync("SELECT COUNT(*) FROM \"AbpPermissions\" WHERE \"Name\" = 'Aqua.Admin.Users.Invite' AND \"RoleId\" IN (9001, 9002) AND \"IsGranted\" = TRUE;"))
                .ShouldBe(2);
        }

        private async Task SeedAdministratorRolesAsync()
        {
            await ExecuteAsync(TestConnectionString,
                """
                INSERT INTO "AbpRoles"
                    ("Id", "CreationTime", "IsDeleted", "TenantId", "Name", "DisplayName", "IsStatic", "IsDefault", "NormalizedName")
                VALUES
                    (9001, NOW(), FALSE, NULL, 'Admin', 'Host Admin', TRUE, FALSE, 'ADMIN'),
                    (9002, NOW(), FALSE, 1, 'Admin', 'Tenant Admin', TRUE, FALSE, 'ADMIN'),
                    (9003, NOW(), FALSE, 2, 'SystemAdmin', 'System Admin', FALSE, FALSE, 'SYSTEMADMIN'),
                    (9004, NOW(), FALSE, 1, 'Member', 'Member', FALSE, FALSE, 'MEMBER');

                INSERT INTO "AbpPermissions"
                    ("TenantId", "Name", "IsGranted", "Discriminator", "RoleId", "UserId", "CreationTime", "CreatorUserId")
                VALUES
                    (2, 'Aqua.Admin.Users.Invite', FALSE, 'RolePermissionSetting', 9003, NULL, NOW(), NULL);
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
