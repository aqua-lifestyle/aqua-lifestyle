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
    public class CommissionRulesVersionMigrationPostgreSqlTests : IAsyncLifetime
    {
        private const string PreviousMigration =
            "20260811150251_SeparateAreaFromTenantBoundary";
        private const string RulesVersionMigration =
            "20260821120000_WidenCommissionRulesVersions";
        private const string ShortRulesVersion = "commission-rules-v1";
        private const string LongRulesVersion =
            "commission-rules-version-longer-than-thirty-two-v1";
        private readonly string _containerName =
            $"commission-rules-version-test-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName =
            $"commission_rules_version_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public CommissionRulesVersionMigrationPostgreSqlTests()
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

            await ExecuteAsync(
                AdminConnectionString,
                $"CREATE DATABASE \"{_databaseName}\" WITH OWNER = aqualifestyle;");
            await MigrateAsync(PreviousMigration);
        }

        public async Task DisposeAsync() =>
            await RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);

        [Fact]
        public async Task Migration_WidensAndSafelyNarrowsOrRefusesWithoutDataLoss()
        {
            await SeedCommissionRowsAsync();

            await MigrateAsync(RulesVersionMigration);
            await AssertColumnLengthsAsync(64);
            await AssertRulesVersionsAsync(ShortRulesVersion);

            await MigrateAsync(PreviousMigration);
            await AssertColumnLengthsAsync(32);
            await AssertRulesVersionsAsync(ShortRulesVersion);

            await MigrateAsync(RulesVersionMigration);
            await SetRulesVersionsAsync(LongRulesVersion);

            var exception = await Should.ThrowAsync<PostgresException>(
                () => MigrateAsync(PreviousMigration));
            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Cannot narrow commission rules versions while values longer than 32 characters exist.");

            await AssertColumnLengthsAsync(64);
            await AssertRulesVersionsAsync(LongRulesVersion);
            (await ScalarAsync(
                    $"SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{RulesVersionMigration}';"))
                .ShouldBe(1);
        }

        private async Task SeedCommissionRowsAsync()
        {
            await ExecuteAsync(
                TestConnectionString,
                $$"""
                INSERT INTO "AbpTenants" (
                    "Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES (1, 'Default', 'Default', TRUE, NOW(), FALSE);

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                VALUES (
                    1, 1, 'commissionmember', 'commission-migration@example.test',
                    'Commission', 'Member', 'COMMISSIONMEMBER',
                    'COMMISSION-MIGRATION@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE);

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "IsActive", "CreationTime",
                    "ClubMemberNumber", "UserId", "IsDeleted")
                VALUES (
                    1, 1, 'Commission Migration Member',
                    'commission-migration@example.test', TRUE, NOW(), 'CLB-COMMISSION-1',
                    1, FALSE);

                INSERT INTO "Memberships" (
                    "Id", "TenantId", "Name", "IsActive", "MembershipType",
                    "MonthlyObligationAmount")
                VALUES (1, 1, 'Onyx', TRUE, 2, 0);

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                VALUES (
                    '10000000-0000-0000-0000-000000000001', 1, 1, 2,
                    TIMESTAMPTZ '2026-08-01 00:00:00+00', 'entry-terms-v1',
                    TIMESTAMPTZ '2026-08-01 00:00:00+00', 1200, 600, 600, 600,
                    600, 7, 'ZAR', NOW(), FALSE);

                INSERT INTO "OnyxParticipations" (
                    "Id", "TenantId", "CustomerId", "OnyxMembershipId",
                    "AdmissionRoute", "Status", "StartedAt", "TermsVersion",
                    "TermsEffectiveFrom", "DirectEntryAmount", "Currency",
                    "CreationTime", "IsDeleted")
                VALUES (
                    '20000000-0000-0000-0000-000000000001', 1, 1, 1, 1, 2,
                    TIMESTAMPTZ '2026-08-01 00:00:00+00', 'onyx-terms-v1',
                    TIMESTAMPTZ '2026-08-01 00:00:00+00', 0, 'ZAR', NOW(), FALSE);

                INSERT INTO "EntryCommissionPeriods" (
                    "Id", "TenantId", "PeriodStart", "PeriodEnd", "TimeZoneId",
                    "CalculatedAt", "RulesVersion", "CreationTime", "IsDeleted")
                VALUES (
                    '30000000-0000-0000-0000-000000000001', 1,
                    TIMESTAMPTZ '2026-08-03 00:00:00+00',
                    TIMESTAMPTZ '2026-08-10 00:00:00+00', 'UTC', NOW(),
                    '{{ShortRulesVersion}}', NOW(), FALSE);

                INSERT INTO "OnyxCommissionPeriods" (
                    "Id", "TenantId", "PeriodStart", "PeriodEnd", "TimeZoneId",
                    "CalculatedAt", "RulesVersion", "CreationTime", "IsDeleted")
                VALUES (
                    '40000000-0000-0000-0000-000000000001', 1,
                    TIMESTAMPTZ '2026-08-03 00:00:00+00',
                    TIMESTAMPTZ '2026-08-10 00:00:00+00', 'UTC', NOW(),
                    '{{ShortRulesVersion}}', NOW(), FALSE);

                INSERT INTO "EntryWeeklyCommissions" (
                    "Id", "TenantId", "EntryParticipationId", "CustomerId",
                    "CommissionPeriodId", "HighestCompletedLevel", "TotalAmount",
                    "Currency", "RulesVersion", "CalculatedAt", "PayoutStatus",
                    "CreationTime", "IsDeleted")
                VALUES (
                    '50000000-0000-0000-0000-000000000001', 1,
                    '10000000-0000-0000-0000-000000000001', 1,
                    '30000000-0000-0000-0000-000000000001', 1, 100, 'ZAR',
                    '{{ShortRulesVersion}}', NOW(), 1, NOW(), FALSE);

                INSERT INTO "OnyxWeeklyCommissions" (
                    "Id", "TenantId", "OnyxParticipationId", "CustomerId",
                    "CommissionPeriodId", "HighestQualifiedNetworkLevel",
                    "HighestCommissionedLevel", "TotalAmount", "Currency",
                    "RulesVersion", "CalculatedAt", "PayoutStatus", "CreationTime",
                    "IsDeleted")
                VALUES (
                    '60000000-0000-0000-0000-000000000001', 1,
                    '20000000-0000-0000-0000-000000000001', 1,
                    '40000000-0000-0000-0000-000000000001', 1, 1, 100, 'ZAR',
                    '{{ShortRulesVersion}}', NOW(), 1, NOW(), FALSE);
                """);
        }

        private async Task SetRulesVersionsAsync(string rulesVersion)
        {
            foreach (var table in CommissionTables)
            {
                await ExecuteAsync(
                    TestConnectionString,
                    $"UPDATE \"{table}\" SET \"RulesVersion\" = '{rulesVersion}';");
            }
        }

        private async Task AssertRulesVersionsAsync(string expected)
        {
            foreach (var table in CommissionTables)
            {
                (await StringScalarAsync(
                        $"SELECT \"RulesVersion\" FROM \"{table}\";"))
                    .ShouldBe(expected);
            }
        }

        private async Task AssertColumnLengthsAsync(long expected)
        {
            foreach (var table in CommissionTables)
            {
                (await ScalarAsync(
                        $"SELECT character_maximum_length FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = 'RulesVersion';"))
                    .ShouldBe(expected);
            }
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

        private async Task<string> StringScalarAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(TestConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToString(await command.ExecuteScalarAsync())!;
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
            {
                throw new InvalidOperationException($"Docker command failed: {error}");
            }
        }

        private static readonly string[] CommissionTables =
        {
            "EntryCommissionPeriods",
            "EntryWeeklyCommissions",
            "OnyxCommissionPeriods",
            "OnyxWeeklyCommissions"
        };

        private string AdminConnectionString =>
            $"Host=localhost;Port={_hostPort};Database=postgres;Username=aqualifestyle;Password=aqualifestyle";

        private string TestConnectionString =>
            $"Host=localhost;Port={_hostPort};Database={_databaseName};Username=aqualifestyle;Password=aqualifestyle";
    }
}
