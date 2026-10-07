using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        private string _containerId;

        public AQGreenRecruitmentAttributionMigrationPostgreSqlTests()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            _containerId = await RunDockerAsync(
                $"run -d --name {_containerName} -e POSTGRES_DB=postgres " +
                "-e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle " +
                $"-p {_hostPort}:5432 postgres:16-alpine");

            await WaitForTcpReadinessAsync();
            await using var connection = await OpenReadyAdminConnectionAsync();
            try
            {
                WriteAdminEvent("CREATE_DATABASE_BEGIN", connection);
                await using var command = new NpgsqlCommand(
                    $"CREATE DATABASE \"{_databaseName}\" WITH OWNER = aqualifestyle;",
                    connection);
                // Execute once on the connection whose SELECT 1 succeeded.
                // A lost DDL response is ambiguous and must not be retried.
                await command.ExecuteNonQueryAsync();
                WriteAdminEvent("CREATE_DATABASE_SUCCESS", connection);
            }
            catch (Exception exception)
            {
                await CaptureFailureAsync("CREATE_DATABASE", exception, connection);
                throw;
            }
        }

        private async Task<NpgsqlConnection> OpenReadyAdminConnectionAsync()
        {
            var connection = new NpgsqlConnection(AdminConnectionString);
            var boundary = "OPEN";
            try
            {
                WriteAdminEvent("OPEN_BEGIN", connection);
                await connection.OpenAsync();
                WriteAdminEvent("OPEN_SUCCESS", connection);
                boundary = "SELECT_1";
                await using var command = new NpgsqlCommand("SELECT 1;", connection);
                if (await command.ExecuteScalarAsync() is not int result || result != 1)
                    throw new InvalidOperationException("Admin SELECT 1 returned an unexpected result.");
                WriteAdminEvent("SELECT_1_SUCCESS", connection);
                // Transfer the still-open connection to InitializeAsync for DDL.
                return connection;
            }
            catch (Exception exception)
            {
                // Once TCP readiness passed, do not relabel or retry a connection failure.
                await CaptureFailureAsync(boundary, exception, connection);
                await connection.DisposeAsync();
                throw;
            }
        }

        private async Task WaitForTcpReadinessAsync()
        {
            // Preserve the existing 30-second startup budget, independently of CI limits.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var attempt = 0;
            try
            {
                while (true)
                {
                    budget.Token.ThrowIfCancellationRequested();
                    var info = new ProcessStartInfo("docker")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                    // The official image's initialization server is socket-only.
                    // Explicit TCP can accept only after the final server starts.
                    foreach (var argument in new[] { "exec", _containerName, "pg_isready",
                        "-h", "127.0.0.1", "-p", "5432", "-U", "aqualifestyle",
                        "-d", "postgres", "-t", "1" })
                        info.ArgumentList.Add(argument);
                    using var process = Process.Start(info);
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    try { await process.WaitForExitAsync(budget.Token); }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                        throw;
                    }
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        utc = DateTimeOffset.UtcNow,
                        monotonicTicks = Stopwatch.GetTimestamp(),
                        monotonicFrequency = Stopwatch.Frequency,
                        testClass = nameof(AQGreenRecruitmentAttributionMigrationPostgreSqlTests),
                        container = _containerName,
                        containerId = _containerId,
                        hostPort = _hostPort,
                        boundary = "TCP_READINESS",
                        attempt = ++attempt,
                        exitCode = process.ExitCode,
                        stdout = await output,
                        stderr = await error
                    }));
                    if (process.ExitCode == 0)
                    {
                        await ObserveDockerAsync("inspect", "--format",
                            "Id={{.Id}} Name={{.Name}} State={{json .State}} " +
                            "RestartCount={{.RestartCount}} Ports={{json .NetworkSettings.Ports}}",
                            _containerName);
                        return;
                    }
                    if (process.ExitCode != 1 && process.ExitCode != 2)
                        throw new InvalidOperationException(
                            $"TCP pg_isready failed with exit code {process.ExitCode}.");
                    await Task.Delay(1000, budget.Token);
                }
            }
            catch (Exception exception)
            {
                var failure = exception is OperationCanceledException
                    ? new TimeoutException("PostgreSQL TCP readiness exceeded 30 seconds.", exception)
                    : exception;
                await CaptureFailureAsync("TCP_READINESS", failure, null);
                if (failure != exception)
                    throw failure;
                throw;
            }
        }

        private void WriteAdminEvent(
            string boundary,
            NpgsqlConnection connection,
            Exception exception = null)
        {
            var settings = new NpgsqlConnectionStringBuilder(AdminConnectionString);
            var password = settings.Password;
            settings.Remove("Password");
            var identity = settings.ConnectionString;
            int? backendPid = null;
            try { backendPid = connection?.ProcessID; }
            catch (InvalidOperationException) { }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                utc = DateTimeOffset.UtcNow,
                monotonicTicks = Stopwatch.GetTimestamp(),
                monotonicFrequency = Stopwatch.Frequency,
                testClass = nameof(AQGreenRecruitmentAttributionMigrationPostgreSqlTests),
                container = _containerName,
                containerId = _containerId,
                hostPort = _hostPort,
                boundary,
                pooling = settings.Pooling,
                connectionIdentity = connection == null ? (int?)null : RuntimeHelpers.GetHashCode(connection),
                failureClass = exception == null ? null : boundary switch
                {
                    "TCP_READINESS_FAILURE" => "PRE_READINESS",
                    "OPEN_FAILURE" => "POST_READINESS_OPEN",
                    "SELECT_1_FAILURE" => "POST_OPEN_SELECT",
                    "CREATE_DATABASE_FAILURE" => "POST_VERIFICATION_DDL",
                    _ => "UNCLASSIFIED"
                },
                connectionStringIdentity = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
                redactedConnectionString = identity,
                backendPid,
                exception = exception?.ToString().Replace(password, "[REDACTED]")
            }));
        }

        private async Task CaptureFailureAsync(
            string boundary,
            Exception exception,
            NpgsqlConnection connection)
        {
            WriteAdminEvent(boundary + "_FAILURE", connection, exception);
            // Observe before cleanup; never use this independent session for readiness.
            await ObserveDockerAsync("inspect", "--format",
                "Id={{.Id}} Name={{.Name}} Image={{.Image}} State={{json .State}} " +
                "RestartCount={{.RestartCount}} Ports={{json .NetworkSettings.Ports}}",
                _containerName);
            await ObserveDockerAsync("logs", "--timestamps", _containerName);
            await ObserveDockerAsync("exec", "-e", "PGCONNECT_TIMEOUT=3", _containerName,
                "psql", "-U", "aqualifestyle", "-d", "postgres", "-X", "-A", "-t", "-c",
                "SELECT pg_postmaster_start_time(), pg_backend_pid(), version();");
        }

        private async Task ObserveDockerAsync(params string[] arguments)
        {
            try
            {
                var info = new ProcessStartInfo("docker")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                foreach (var argument in arguments)
                    info.ArgumentList.Add(argument);
                using var process = Process.Start(info);
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
                var password = new NpgsqlConnectionStringBuilder(AdminConnectionString).Password;
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    utc = DateTimeOffset.UtcNow,
                    monotonicTicks = Stopwatch.GetTimestamp(),
                    monotonicFrequency = Stopwatch.Frequency,
                    testClass = nameof(AQGreenRecruitmentAttributionMigrationPostgreSqlTests),
                    container = _containerName,
                    containerId = _containerId,
                    hostPort = _hostPort,
                    observation = arguments[0],
                    exitCode = process.ExitCode,
                    stdout = (await output).Replace(password, "[REDACTED]"),
                    stderr = (await error).Replace(password, "[REDACTED]")
                }));
            }
            catch (Exception observationFailure)
            {
                // Diagnostics must not replace the original fixture failure.
                Console.WriteLine($"PostgreSQL observation unavailable: {observationFailure.GetType().Name}");
            }
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

        private async Task<string> RunDockerAsync(
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
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (throwOnFailure && process.ExitCode != 0)
                throw new InvalidOperationException($"Docker command failed: {error}");
            return output.Trim();
        }

        private string BuildConnectionString(string database) =>
            $"Host=localhost;Port={_hostPort};Database={database};" +
            "Username=aqualifestyle;Password=aqualifestyle";

        private string AdminConnectionString => BuildConnectionString("postgres") + ";Pooling=false";
        private string TestConnectionString => BuildConnectionString(_databaseName);
    }
}
