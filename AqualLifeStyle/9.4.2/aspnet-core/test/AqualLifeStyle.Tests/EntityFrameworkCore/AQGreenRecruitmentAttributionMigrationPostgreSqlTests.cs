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
    public sealed class AQGreenRecruitmentAttributionMigrationPostgreSqlTests
        : IAsyncLifetime
    {
        private const string PreviousMigration =
            "20260825095740_AddAQGreenPlacementV2Foundation";
        private const string AttributionMigration =
            "20260826011850_AddAQGreenRecruitmentAttributionFoundation";
        private static readonly Guid ParticipantOne =
            Guid.Parse("30000000-0000-0000-0000-000000000001");
        private static readonly Guid ParticipantTwo =
            Guid.Parse("30000000-0000-0000-0000-000000000002");
        private readonly string _containerName =
            $"aqgreen-attribution-migration-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName =
            $"aqgreen_attribution_migration_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public AQGreenRecruitmentAttributionMigrationPostgreSqlTests()
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

            await ExecuteAsync(
                AdminConnectionString,
                $"CREATE DATABASE \"{_databaseName}\" WITH OWNER = aqualifestyle;");
        }

        public Task DisposeAsync() =>
            RunDockerAsync($"rm -fv {_containerName}", throwOnFailure: false);

        [Fact]
        public async Task Migration_UpIsAdditiveAndEmptyDownPreservesV1Facts()
        {
            await MigrateAsync(PreviousMigration);
            await SeedLegacyParticipantsAsync();
            await MigrateAsync(AttributionMigration);

            (await ScalarAsync(
                    "SELECT COUNT(*) FROM information_schema.tables " +
                    "WHERE table_schema = 'public' AND table_name IN " +
                    "('AQGreenRecruitmentAttributions', " +
                    "'AQGreenRecruitmentAttributionConfirmations');"))
                .ShouldBe(2);
            (await ScalarAsync(
                    "SELECT COUNT(*) FROM public.\"AQGreenRecruitmentAttributions\";"))
                .ShouldBe(0);
            (await ScalarAsync(
                    $"SELECT COUNT(*) FROM public.\"EntryParticipations\" " +
                    $"WHERE \"Id\" = '{ParticipantTwo}' AND \"RecruiterCustomerId\" = 1;"))
                .ShouldBe(1);

            await MigrateAsync(PreviousMigration);

            (await ScalarAsync(
                    "SELECT COUNT(*) FROM information_schema.tables " +
                    "WHERE table_schema = 'public' AND table_name LIKE " +
                    "'AQGreenRecruitmentAttribution%';"))
                .ShouldBe(0);
            (await ScalarAsync(
                    "SELECT COUNT(*) FROM pg_constraint " +
                    "WHERE conname = 'AK_EntryParticipations_TenantId_Id';"))
                .ShouldBe(1);
            (await ScalarAsync(
                    $"SELECT COUNT(*) FROM public.\"EntryParticipations\" " +
                    $"WHERE \"Id\" = '{ParticipantTwo}' AND \"RecruiterCustomerId\" = 1;"))
                .ShouldBe(1);
        }

        [Fact]
        public async Task Migration_DownRefusesRecordedAttributionEvidence()
        {
            await MigrateAsync(AttributionMigration);
            await SeedLegacyParticipantsAsync();
            await InsertRootAttributionAsync(TestConnectionString, Guid.NewGuid());

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateAsync(PreviousMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            (await ScalarAsync(
                    $"SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" " +
                    $"WHERE \"MigrationId\" = '{AttributionMigration}';"))
                .ShouldBe(1);
            (await ScalarAsync(
                    "SELECT COUNT(*) FROM public.\"AQGreenRecruitmentAttributions\";"))
                .ShouldBe(1);
        }

        [Fact]
        public async Task Migration_DownWaitsForWriterThenRefusesCommittedEvidence()
        {
            await MigrateAsync(AttributionMigration);
            await SeedLegacyParticipantsAsync();

            await using var writerConnection = new NpgsqlConnection(TestConnectionString);
            await writerConnection.OpenAsync();
            await using var writerTransaction = await writerConnection.BeginTransactionAsync();
            await InsertRootAttributionAsync(
                writerConnection,
                writerTransaction,
                Guid.NewGuid());

            var rollbackTask = MigrateAsync(PreviousMigration);
            await Task.Delay(500);
            rollbackTask.IsCompleted.ShouldBeFalse();

            await writerTransaction.CommitAsync();
            var exception = await Should.ThrowAsync<PostgresException>(() => rollbackTask);

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            (await ScalarAsync(
                    $"SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" " +
                    $"WHERE \"MigrationId\" = '{AttributionMigration}';"))
                .ShouldBe(1);
        }

        private async Task SeedLegacyParticipantsAsync()
        {
            await ExecuteAsync(
                TestConnectionString,
                $$"""
                INSERT INTO public."AbpTenants" (
                    "Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES (1, 'attribution', 'Attribution', TRUE, NOW(), FALSE);

                INSERT INTO public."Areas" (
                    "Id", "TenantId", "Code", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES ('b0000000-0000-0000-0000-000000000001', 1,
                        'ATTR', 'Attribution Area', TRUE, NOW(), FALSE);

                INSERT INTO public."AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                VALUES
                    (101, 1, 'attr-user-1', 'attr-user-1@example.test', 'Attr', 'One',
                     'ATTR-USER-1', 'ATTR-USER-1@EXAMPLE.TEST', 'test-password', 3,
                     TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE),
                    (102, 1, 'attr-user-2', 'attr-user-2@example.test', 'Attr', 'Two',
                     'ATTR-USER-2', 'ATTR-USER-2@EXAMPLE.TEST', 'test-password', 3,
                     TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE);

                INSERT INTO public."Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                VALUES
                    (1, 1, 'Attribution One', 'attr-one@example.test',
                     'b0000000-0000-0000-0000-000000000001', TRUE, NOW(),
                     'ATTR-1', 101, FALSE),
                    (2, 1, 'Attribution Two', 'attr-two@example.test',
                     'b0000000-0000-0000-0000-000000000001', TRUE, NOW(),
                     'ATTR-2', 102, FALSE);

                INSERT INTO public."EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "RecruiterCustomerId", "Status",
                    "StartedAt", "TermsVersion", "TermsEffectiveFrom",
                    "JoiningPaymentAmount", "JoiningInstallmentAmount",
                    "RegistrationPaymentAmount", "ActivationPaymentAmount",
                    "MonthlyCommitmentAmount", "GracePeriodDays", "Currency",
                    "CreationTime", "IsDeleted")
                VALUES
                    ('{{ParticipantOne}}', 1, 1, NULL, 2,
                     TIMESTAMPTZ '2026-08-01 00:00:00+00', 'entry-terms-v1',
                     TIMESTAMPTZ '2026-08-01 00:00:00+00',
                     1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE),
                    ('{{ParticipantTwo}}', 1, 2, 1, 2,
                     TIMESTAMPTZ '2026-08-01 00:00:00+00', 'entry-terms-v1',
                     TIMESTAMPTZ '2026-08-01 00:00:00+00',
                     1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE);
                """);
        }

        private static Task InsertRootAttributionAsync(
            string connectionString,
            Guid attributionId) =>
            ExecuteAsync(
                connectionString,
                RootAttributionSql(attributionId));

        private static Task InsertRootAttributionAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid attributionId) =>
            ExecuteAsync(connection, transaction, RootAttributionSql(attributionId));

        private static string RootAttributionSql(Guid attributionId) =>
            $$"""
            INSERT INTO public."AQGreenRecruitmentAttributions" (
                "Id", "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                "AttributionKind", "AcquisitionSource", "SourceReferenceId", "AttributedAt",
                "AttributedByUserId", "AssignmentReason", "RulesVersion")
            VALUES (
                '{{attributionId}}', 1, '{{ParticipantTwo}}', NULL,
                2, 2, '{{Guid.NewGuid()}}', TIMESTAMPTZ '2026-08-26 08:00:00+00',
                101, 'Authorised prospective root attribution', 'AQGreenRecruitmentAttributionV1');
            """;

        private async Task MigrateAsync(string targetMigration)
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(TestConnectionString)
                    .Options);
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
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

        private static async Task ExecuteAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
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

        private string BuildConnectionString(string database) =>
            $"Host=localhost;Port={_hostPort};Database={database};" +
            "Username=aqualifestyle;Password=aqualifestyle";

        private string AdminConnectionString => BuildConnectionString("postgres");
        private string TestConnectionString => BuildConnectionString(_databaseName);
    }
}
