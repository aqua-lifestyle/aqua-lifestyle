using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class AQGreenMigrationRollbackPostgreSqlTests : IAsyncLifetime
    {
        private const string PostgresImage = "postgres:16-alpine";
        private const string MonthlyCheckoutMigration =
            "20260809114317_AddAQGreenMonthlyObligationCheckouts";
        private const string PreviousMonthlyCheckoutMigration =
            "20260809081746_AddAreaActivationStateHistory";
        private const string TermsVersionsMigration =
            "20260809201814_AddCommissionTermsVersions";
        private const string BeforeFuneralCoverMigration =
            "20260809042322_EnforceSingleProgrammeParticipationDecision";
        private const string FuneralCoverMigration =
            "20260809043240_AddAQGreenFuneralCoverEntitlements";
        private readonly string _containerName = $"aqgreen-migration-test-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName = $"aqgreen_test_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public AQGreenMigrationRollbackPostgreSqlTests()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            await StartPostgreSqlContainerAsync();
            await CreateTestDatabaseAsync();
            await MigrateToLatestAsync();
        }

        public async Task DisposeAsync()
        {
            await StopPostgreSqlContainerAsync();
        }

        private async Task StartPostgreSqlContainerAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run -d --name {_containerName} -e POSTGRES_DB=postgres -e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle -p {_hostPort}:5432 {PostgresImage}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Docker process for PostgreSQL.");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Docker run failed: {error}");
            }

            TraceLine($"Started PostgreSQL container: {output.Trim()} on port {_hostPort}");

            var maxAttempts = 30;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using var connection = new Npgsql.NpgsqlConnection(BuildAdminConnectionString());
                    await connection.OpenAsync();
                    TraceLine("PostgreSQL is ready.");
                    return;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }

            throw new InvalidOperationException("PostgreSQL container did not become ready in time.");
        }

        private async Task ResetDatabaseAsync()
        {
            await using var adminConnection = new Npgsql.NpgsqlConnection(BuildAdminConnectionString());
            await adminConnection.OpenAsync();

            await using var terminateCommand = adminConnection.CreateCommand();
            terminateCommand.CommandText = $"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{_databaseName}'
                  AND pid <> pg_backend_pid();
                """;
            await terminateCommand.ExecuteNonQueryAsync();

            await using var dropCommand = adminConnection.CreateCommand();
            dropCommand.CommandText = $"""
                DROP DATABASE IF EXISTS "{_databaseName}";
                """;
            await dropCommand.ExecuteNonQueryAsync();

            await using var createCommand = adminConnection.CreateCommand();
            createCommand.CommandText = $"""
                CREATE DATABASE "{_databaseName}" WITH OWNER = aqualifestyle;
                """;
            await createCommand.ExecuteNonQueryAsync();

            Npgsql.NpgsqlConnection.ClearAllPools();

            TraceLine($"Reset test database: {_databaseName}");
        }

        private async Task CreateTestDatabaseAsync()
        {
            await using var connection = new Npgsql.NpgsqlConnection(BuildAdminConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{_databaseName}\" WITH OWNER = aqualifestyle;";
            await command.ExecuteNonQueryAsync();
            TraceLine($"Created test database: {_databaseName}");
        }

        private async Task SeedMinimalUserAsync()
        {
            var connectionString = BuildTestConnectionString();
            await using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var userCommand = connection.CreateCommand();
            userCommand.CommandText = """
                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password",
                    "IsEmailConfirmed", "IsActive", "CreationTime",
                    "CreatorUserId", "LastModificationTime", "LastModifierUserId",
                    "IsDeleted", "DeleterUserId", "DeletionTime",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled", "SecurityStamp", "ConcurrencyStamp"
                )
                VALUES (
                    1, NULL, 'admin', 'admin@example.test', 'Admin', 'User',
                    'ADMIN', 'ADMIN@EXAMPLE.TEST', 'AQAAAAIAAYagAAAAEIyc0dGWfvhRQXjBOiIQ6L8yZeE5W1e5vTXvjC/zvGkqsYH/F0L32b2sK0oN5sN9w==',
                    TRUE, TRUE, NOW(),
                    NULL, NULL, NULL,
                    FALSE, NULL, NULL,
                    0, FALSE, FALSE,
                    FALSE, NULL, NULL
                )
                ON CONFLICT ("Id") DO NOTHING;
                """;
            await userCommand.ExecuteNonQueryAsync();

            await using var customerCommand = connection.CreateCommand();
            customerCommand.CommandText = $"""
                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "IsActive", "CreationTime", "ClubMemberNumber",
                    "CreatorUserId", "UserId", "LastModificationTime", "LastModifierUserId",
                    "IsDeleted", "DeleterUserId", "DeletionTime"
                )
                VALUES (
                    1, 1, 'AQGreen Migration Test Member', 'aqgreen-migration-test@example.test', TRUE, NOW(), 'CLB-TEST-001',
                    NULL, 1, NULL, NULL,
                    FALSE, NULL, NULL
                )
                ON CONFLICT ("Id") DO NOTHING;
                """;
            await customerCommand.ExecuteNonQueryAsync();

            TraceLine("Seeded minimal user and customer.");
        }

        private async Task SeedAdditionalCustomerAsync(int customerId)
        {
            await ExecuteAsync($$"""
                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password",
                    "IsEmailConfirmed", "IsActive", "CreationTime",
                    "CreatorUserId", "LastModificationTime", "LastModifierUserId",
                    "IsDeleted", "DeleterUserId", "DeletionTime",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled", "SecurityStamp", "ConcurrencyStamp"
                )
                VALUES (
                    {{customerId}}, 1, 'cover{{customerId}}', 'cover{{customerId}}@example.test',
                    'Cover', 'Member {{customerId}}', 'COVER{{customerId}}',
                    'COVER{{customerId}}@EXAMPLE.TEST',
                    'AQAAAAIAAYagAAAAEIyc0dGWfvhRQXjBOiIQ6L8yZeE5W1e5vTXvjC/zvGkqsYH/F0L32b2sK0oN5sN9w==',
                    TRUE, TRUE, NOW(), NULL, NULL, NULL, FALSE, NULL, NULL,
                    0, FALSE, FALSE, FALSE, NULL, NULL
                );

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "IsActive", "CreationTime",
                    "ClubMemberNumber", "CreatorUserId", "UserId", "LastModificationTime",
                    "LastModifierUserId", "IsDeleted", "DeleterUserId", "DeletionTime"
                )
                VALUES (
                    {{customerId}}, 1, 'AQGreen Migration Member {{customerId}}',
                    'cover{{customerId}}@example.test', TRUE, NOW(), 'CLB-TEST-{{customerId}}',
                    NULL, {{customerId}}, NULL, NULL, FALSE, NULL, NULL
                );
                """);
        }

        private async Task StopPostgreSqlContainerAsync()
        {
            var stopInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"rm -f {_containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(stopInfo);
            if (process == null)
            {
                return;
            }

            await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            TraceLine($"Stopped PostgreSQL container: {_containerName}");
        }

        private string BuildAdminConnectionString() =>
            $"Host=localhost;Port={_hostPort};Database=postgres;Username=aqualifestyle;Password=aqualifestyle";

        private string BuildTestConnectionString() =>
            $"Host=localhost;Port={_hostPort};Database={_databaseName};Username=aqualifestyle;Password=aqualifestyle";

        private AqualLifeStyleDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
            optionsBuilder.UseNpgsql(BuildTestConnectionString());

            return new AqualLifeStyleDbContext(optionsBuilder.Options);
        }

        private async Task MigrateToLatestAsync()
        {
            await using var context = CreateDbContext();
            var migrator = context.GetService<IMigrator>();
            migrator.ShouldNotBeNull();
            await migrator.MigrateAsync();
            TraceLine("Migrated to latest.");
            await SeedMinimalUserAsync();
        }

        private static async Task<long> CountAsync(string connectionString, string sql)
        {
            await using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task ExecuteAsync(string sql)
        {
            await using var connection = new Npgsql.NpgsqlConnection(
                BuildTestConnectionString());
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<(Guid ParticipationId, DateTime ConfirmedAt)>
            SeedModernAQGreenParticipationAsync(
                bool completeJoiningPayment,
                int? requestedCustomerId = null)
        {
            var customerId = requestedCustomerId ?? await GetTenantOneCustomerIdAsync();
            var startedAt = new DateTime(
                2026,
                8,
                1,
                9,
                0,
                0,
                DateTimeKind.Utc);
            var confirmedAt = startedAt.AddMinutes(5);

            await using var context = CreateDbContext();
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "2026-08-single-1200",
                    new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                    1200m,
                    600m,
                    7),
                startedAt);
            context.EntryParticipations.Add(participation);

            if (completeJoiningPayment)
            {
                var payment = MemberPayment.CreatePending(
                    1,
                    customerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m,
                    "Test",
                    $"historical-cover-{Guid.NewGuid():N}",
                    startedAt,
                    "ZAR");
                payment.Confirm(confirmedAt);
                participation.ApplyConfirmedJoiningPayment(payment);
                context.MemberPayments.Add(payment);
            }

            await context.SaveChangesAsync();
            return (participation.Id, confirmedAt);
        }

        private async Task<(Guid ParticipationId, DateTime FirstConfirmedAt,
            DateTime SecondConfirmedAt)> SeedInstallmentParticipationAsync(
                bool includeSecondPayment,
                int? requestedCustomerId = null)
        {
            var customerId = requestedCustomerId ?? await GetTenantOneCustomerIdAsync();
            var startedAt = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);
            var firstConfirmedAt = startedAt.AddMinutes(5);
            var secondConfirmedAt = startedAt.AddMinutes(10);

            await using var context = CreateDbContext();
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                    "2026-08-flexible-1200",
                    new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                    1200m,
                    600m,
                    600m,
                    7),
                startedAt);
            participation.SelectJoiningPaymentSchedule(
                AQGreenJoiningPaymentSchedule.TwoInstallments);

            var firstPayment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.AQGreenJoining,
                600m,
                "Test",
                $"historical-cover-first-{Guid.NewGuid():N}",
                startedAt,
                "ZAR");
            firstPayment.Confirm(firstConfirmedAt);
            participation.ApplyConfirmedJoiningPayment(
                firstPayment,
                AQGreenJoiningPaymentStage.FirstInstallment);
            context.MemberPayments.Add(firstPayment);
            context.EntryParticipations.Add(participation);

            if (includeSecondPayment)
            {
                var secondPayment = MemberPayment.CreatePending(
                    1,
                    customerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    600m,
                    "Test",
                    $"historical-cover-second-{Guid.NewGuid():N}",
                    startedAt.AddMinutes(6),
                    "ZAR");
                secondPayment.Confirm(secondConfirmedAt);
                participation.ApplyConfirmedJoiningPayment(
                    secondPayment,
                    AQGreenJoiningPaymentStage.SecondInstallment);
                context.MemberPayments.Add(secondPayment);
            }

            await context.SaveChangesAsync();
            return (participation.Id, firstConfirmedAt, secondConfirmedAt);
        }

        private async Task<Guid> SeedLegacyParticipationThroughJoiningMigrationAsync()
        {
            const string previousMigration =
                "20260726145201_AddDirectOnyxCheckoutIntents";
            await ResetDatabaseAsync();
            await MigrateToAsync(previousMigration);
            await SeedMinimalUserAsync();

            var participationId = Guid.NewGuid();
            await ExecuteAsync($$"""
                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom",
                    "RegistrationPaymentAmount", "ActivationPaymentAmount",
                    "MonthlyCommitmentAmount", "GracePeriodDays", "Currency",
                    "CreationTime", "IsDeleted")
                VALUES (
                    '{{participationId}}', 1, 1, 0,
                    TIMESTAMPTZ '2026-07-24 12:27:22.947458+00',
                    '2026-07', TIMESTAMPTZ '2026-07-01 00:00:00+00',
                    600.00, 600.00, 600.00, 7, 'ZAR',
                    NOW(), FALSE);
                """);
            await MigrateToAsync("20260726162000_AddAQGreenSingleJoiningPayment");
            await MigrateToAsync(BeforeFuneralCoverMigration);
            return participationId;
        }

        private async Task ApplyLegacyJoiningPaymentAsync(
            Guid participationId,
            DateTime confirmedAt)
        {
            var paymentId = Guid.NewGuid();
            await ExecuteAsync($$"""
                INSERT INTO "MemberPayments" (
                    "Id", "TenantId", "CustomerId", "Purpose", "Amount", "Currency",
                    "Provider", "ExternalReference", "Status", "InitiatedAt", "ConfirmedAt",
                    "CreationTime", "IsDeleted")
                VALUES (
                    '{{paymentId}}', 1, 1, 7, 1200.00, 'ZAR', 'Test',
                    'legacy-joining-{{paymentId:N}}', 1,
                    TIMESTAMPTZ '2026-08-04 19:01:42.097429+00',
                    TIMESTAMPTZ '{{confirmedAt.ToString("yyyy-MM-dd HH:mm:ss")}}+00',
                    NOW(), FALSE);

                UPDATE "EntryParticipations"
                SET "JoiningPaymentId" = '{{paymentId}}',
                    "Status" = 2,
                    "ActivatedAt" = TIMESTAMPTZ '{{confirmedAt.ToString("yyyy-MM-dd HH:mm:ss")}}+00'
                WHERE "Id" = '{{participationId}}';
                """);
        }

        private async Task<Guid> SeedLegacyShapedParticipationAsync()
        {
            var participationId = Guid.NewGuid();
            await ExecuteAsync($$"""
                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom",
                    "RegistrationPaymentAmount", "ActivationPaymentAmount",
                    "MonthlyCommitmentAmount", "GracePeriodDays", "Currency",
                    "JoiningPaymentAmount", "JoiningInstallmentAmount",
                    "CreationTime", "IsDeleted")
                VALUES (
                    '{{participationId}}', 1, 1, 0,
                    TIMESTAMPTZ '2026-07-24 09:00:00+00',
                    '2026-07-single-1200', TIMESTAMPTZ '2026-07-26 00:00:00+00',
                    600.00, 600.00, 600.00, 7, 'ZAR',
                    1200.00, 0.00,
                    NOW(), FALSE);
                """);
            return participationId;
        }

        private async Task MigrateToAsync(string targetMigration)
        {
            await using var context = CreateDbContext();
            var migrator = context.GetService<IMigrator>();
            migrator.ShouldNotBeNull();
            await migrator.MigrateAsync(targetMigration);
            TraceLine($"Migrated to {targetMigration}.");
        }

        private async Task<int> GetTenantOneCustomerIdAsync()
        {
            await using var context = CreateDbContext();
            return await context.Customers
                .Where(c => c.TenantId == 1)
                .Select(c => c.Id)
                .FirstAsync();
        }

        [Fact]
        public async Task FuneralCoverMigration_BackfillsHistoricalFullPaymentAtConfirmationTime()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            var completion = await SeedModernAQGreenParticipationAsync(
                completeJoiningPayment: true);

            await MigrateToAsync(FuneralCoverMigration);

            await using var context = CreateDbContext();
            var entitlement = await context.AQGreenFuneralCoverEntitlements.SingleAsync();
            entitlement.EntryParticipationId.ShouldBe(completion.ParticipationId);
            entitlement.IncludedAt.ShouldBe(completion.ConfirmedAt);
            entitlement.FuneralCoverAmount.ShouldBe(30000m);
        }

        [Fact]
        public async Task FuneralCoverMigration_BackfillsTwoInstallmentsAtLaterConfirmationTime()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            var completion = await SeedInstallmentParticipationAsync(
                includeSecondPayment: true);

            await MigrateToAsync(FuneralCoverMigration);

            await using var context = CreateDbContext();
            var entitlement = await context.AQGreenFuneralCoverEntitlements.SingleAsync();
            entitlement.EntryParticipationId.ShouldBe(completion.ParticipationId);
            entitlement.IncludedAt.ShouldBe(completion.SecondConfirmedAt);
        }

        [Fact]
        public async Task FuneralCoverMigration_LeavesFirstInstallmentOnlyUntouched()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            await SeedInstallmentParticipationAsync(includeSecondPayment: false);

            await MigrateToAsync(FuneralCoverMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(0);
        }

        [Fact]
        public async Task FuneralCoverMigration_LeavesPendingFullPaymentUntouched()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            var seeded = await SeedModernAQGreenParticipationAsync(
                completeJoiningPayment: false);

            await using (var context = CreateDbContext())
            {
                var payment = MemberPayment.CreatePending(
                    1,
                    await GetTenantOneCustomerIdAsync(),
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m,
                    "Test",
                    $"historical-pending-{Guid.NewGuid():N}",
                    new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc),
                    "ZAR");
                context.MemberPayments.Add(payment);
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.Id == seeded.ParticipationId);
                context.Entry(participation).Property("JoiningPaymentId").CurrentValue = payment.Id;
                await context.SaveChangesAsync();
            }

            await MigrateToAsync(FuneralCoverMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(0);
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForContradictoryCompletionFacts()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            await SeedModernAQGreenParticipationAsync(completeJoiningPayment: true);
            await ExecuteAsync(
                "UPDATE \"MemberPayments\" SET \"Amount\" = 1199.00 WHERE \"Purpose\" = 7");

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForWrongPaymentCustomer()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            await SeedAdditionalCustomerAsync(2);
            await SeedModernAQGreenParticipationAsync(completeJoiningPayment: true);
            await ExecuteAsync(
                "UPDATE \"MemberPayments\" SET \"CustomerId\" = 2 WHERE \"Purpose\" = 7");

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForCrossTenantCustomerRelationship()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            await SeedModernAQGreenParticipationAsync(completeJoiningPayment: true);
            await ExecuteAsync(
                "UPDATE \"Customers\" SET \"TenantId\" = 2 WHERE \"Id\" = 1");

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForDuplicateInstallmentReference()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            var completion = await SeedInstallmentParticipationAsync(
                includeSecondPayment: true);
            await ExecuteAsync($$"""
                UPDATE "EntryParticipations"
                SET "ActivationPaymentId" = "RegistrationPaymentId"
                WHERE "Id" = '{{completion.ParticipationId}}';
                """);

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FuneralCoverMigration_DoesNotDuplicateExistingEntitlement_AndDownDropsData()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(FuneralCoverMigration);
            await SeedMinimalUserAsync();
            var completion = await SeedModernAQGreenParticipationAsync(
                completeJoiningPayment: true);

            await using (var context = CreateDbContext())
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.Id == completion.ParticipationId);
                var entitlement = AQGreenFuneralCoverEntitlement
                    .GrantForJoiningCompletion(
                        participation,
                        AQGreenFuneralCoverTerms.Create(
                            "2026-08-funeral-30000",
                            new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                            30000m),
                        completion.ConfirmedAt);
                context.AQGreenFuneralCoverEntitlements.Add(entitlement);
                await context.SaveChangesAsync();
            }

            await MigrateToAsync(FuneralCoverMigration);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(1);

            // This proves the existing Down behaviour is destructive after use.
            await MigrateToAsync(BeforeFuneralCoverMigration);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AQGreenFuneralCoverEntitlements'"))
                .ShouldBe(0);
        }

        [Fact]
        public async Task FuneralCoverMigration_BackfillsEachQualifyingParticipantOnce()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            await SeedAdditionalCustomerAsync(2);
            await SeedAdditionalCustomerAsync(3);
            await SeedModernAQGreenParticipationAsync(true, 1);
            await SeedModernAQGreenParticipationAsync(true, 2);
            await SeedInstallmentParticipationAsync(true, 3);

            await MigrateToAsync(FuneralCoverMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(3);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(DISTINCT \"EntryParticipationId\") FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(3);
        }

        [Fact]
        public async Task FuneralCoverMigration_DoesNotInventModernHistoryForLegacyParticipation()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();

            await using (var context = CreateDbContext())
            {
                context.EntryParticipations.Add(
                    EntryParticipation.StartIndependently(
                        1,
                        await GetTenantOneCustomerIdAsync(),
                        EntryProgrammeTerms.Create(
                            "legacy-split-lifecycle",
                            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                            600m,
                            600m,
                            600m,
                            7),
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
                await context.SaveChangesAsync();
            }

            await MigrateToAsync(FuneralCoverMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(0);
        }

        [Fact]
        public async Task FuneralCoverMigration_RecognisesProvenLegacyChronology_Unpaid()
        {
            await ResetDatabaseAsync();
            var participationId = await SeedLegacyParticipationThroughJoiningMigrationAsync();

            await MigrateToAsync(FuneralCoverMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AQGreenFuneralCoverEntitlements\""))
                .ShouldBe(0);
            (await CountAsync(
                BuildTestConnectionString(),
                $$"""
                SELECT COUNT(*)
                FROM "AQGreenMigrationBackup"
                WHERE "ParticipationId" = '{{participationId}}'
                  AND "OldTermsVersion" = '2026-07'
                  AND "OldTermsEffectiveFrom" = TIMESTAMPTZ '2026-07-01 00:00:00+00'
                """))
                .ShouldBe(1);
        }

        [Fact]
        public async Task FuneralCoverMigration_RecognisesProvenLegacyChronology_AndBackfillsAtConfirmedPayment()
        {
            await ResetDatabaseAsync();
            var participationId = await SeedLegacyParticipationThroughJoiningMigrationAsync();
            var confirmedAt = new DateTime(
                2026, 8, 4, 19, 1, 43, DateTimeKind.Utc);
            await ApplyLegacyJoiningPaymentAsync(participationId, confirmedAt);

            await MigrateToAsync(FuneralCoverMigration);

            await using var context = CreateDbContext();
            var entitlement = await context.AQGreenFuneralCoverEntitlements.SingleAsync();
            entitlement.EntryParticipationId.ShouldBe(participationId);
            entitlement.IncludedAt.ShouldBe(confirmedAt);
            entitlement.FuneralCoverAmount.ShouldBe(30000m);
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForModernStartWithoutLegacyProvenance()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            await SeedModernAQGreenParticipationAsync(completeJoiningPayment: true);
            await ExecuteAsync($$"""
                UPDATE "EntryParticipations"
                SET "StartedAt" = TIMESTAMPTZ '2026-07-24 09:00:00+00'
                WHERE "JoiningPaymentId" IS NOT NULL;
                """);

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForLegacyBackupWithoutOldTermsEffectiveDate()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            var participationId = await SeedLegacyShapedParticipationAsync();
            await ExecuteAsync($$"""
                INSERT INTO "AQGreenMigrationBackup" (
                    "ParticipationId", "OldTermsVersion", "OldTermsEffectiveFrom")
                VALUES (
                    '{{participationId}}', '2026-07', NULL);
                """);

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FuneralCoverMigration_FailsClosedForLegacyChronologyContradiction()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(BeforeFuneralCoverMigration);
            await SeedMinimalUserAsync();
            var participationId = await SeedLegacyShapedParticipationAsync();
            await ExecuteAsync($$"""
                INSERT INTO "AQGreenMigrationBackup" (
                    "ParticipationId", "OldTermsVersion", "OldTermsEffectiveFrom")
                VALUES (
                    '{{participationId}}', '2026-07',
                    TIMESTAMPTZ '2026-07-25 00:00:00+00');
                """);

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(FuneralCoverMigration));

            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "Contradictory historical AQGreen joining-payment data");
        }

        [Fact]
        public async Task FlexibleJoiningMigration_BackfillsUnpaidRows_AndProtectsInstallmentHistory()
        {
            const string previousMigration =
                "20260809043240_AddAQGreenFuneralCoverEntitlements";
            await ResetDatabaseAsync();
            await MigrateToAsync(previousMigration);
            await SeedMinimalUserAsync();
            var customerId = await GetTenantOneCustomerIdAsync();
            Guid participationId;

            await using (var context = CreateDbContext())
            {
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customerId,
                    EntryProgrammeTerms.CreateSingleJoiningPayment(
                        "2026-08-single-1200",
                        new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                        1200m,
                        600m,
                        7),
                    DateTime.UtcNow);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                participationId = participation.Id;
            }

            await MigrateToLatestAsync();

            await using (var context = CreateDbContext())
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.Id == participationId);
                participation.TermsVersion.ShouldBe("2026-08-flexible-1200");
                participation.JoiningInstallmentAmount.ShouldBe(600m);
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                await context.SaveChangesAsync();
            }

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                MigrateToAsync(previousMigration));
            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            exception.MessageText.ShouldContain(
                "two-instalment history exists");
        }

        [Fact]
        public async Task ProgrammeDecisionLock_SerializesCompetingPostgreSqlTransactions()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            var participationId = Guid.NewGuid();

            await using var firstContext = CreateDbContext();
            await using var secondContext = CreateDbContext();
            await using var firstTransaction =
                await firstContext.Database.BeginTransactionAsync();
            await using var secondTransaction =
                await secondContext.Database.BeginTransactionAsync();

            var firstProvider =
                Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            firstProvider.GetDbContext().Returns(firstContext);
            var secondProvider =
                Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            secondProvider.GetDbContext().Returns(secondContext);
            var firstLock = new HostedPaymentCheckoutLock(firstProvider);
            var secondLock = new HostedPaymentCheckoutLock(secondProvider);

            await firstLock.AcquireProgrammeParticipationDecisionAsync(
                participationId);
            var competingAcquisition =
                secondLock.AcquireProgrammeParticipationDecisionAsync(
                    participationId);

            await Task.Delay(250);
            competingAcquisition.IsCompleted.ShouldBeFalse();

            await firstTransaction.CommitAsync();
            await competingAcquisition.WaitAsync(TimeSpan.FromSeconds(5));
            await secondTransaction.CommitAsync();
        }

        [Fact]
        public async Task Database_AllowsDirectOnyxRetryOnlyAfterTerminalFailure()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            var customerId = await GetTenantOneCustomerIdAsync();
            var createdAt = DateTime.UtcNow;

            await using var context = CreateDbContext();
            var membership = Membership.Create(
                1,
                "Onyx retry index test",
                "Onyx retry index test plan",
                MembershipType.Onyx);
            context.Memberships.Add(membership);
            await context.SaveChangesAsync();

            var failedAttempt = DirectOnyxCheckoutIntent.Create(
                1,
                customerId,
                null,
                null,
                membership.Id,
                OnyxPlanTerms.Create("retry-index-2026-08", createdAt, 6120m),
                createdAt);
            failedAttempt.RecordCheckout(
                "ch_onyx_retry_failed",
                "https://payments.example.test/ch_onyx_retry_failed",
                createdAt.AddSeconds(1));
            failedAttempt.RecordProviderFailure(
                createdAt.AddSeconds(2),
                "Signed provider failure");
            context.DirectOnyxCheckoutIntents.Add(failedAttempt);
            await context.SaveChangesAsync();

            var retry = DirectOnyxCheckoutIntent.Create(
                1,
                customerId,
                null,
                null,
                membership.Id,
                OnyxPlanTerms.Create("retry-index-2026-08", createdAt, 6120m),
                createdAt.AddSeconds(3));
            context.DirectOnyxCheckoutIntents.Add(retry);
            await context.SaveChangesAsync();

            var competingAttempt = DirectOnyxCheckoutIntent.Create(
                1,
                customerId,
                null,
                null,
                membership.Id,
                OnyxPlanTerms.Create("retry-index-2026-08", createdAt, 6120m),
                createdAt.AddSeconds(4));
            context.DirectOnyxCheckoutIntents.Add(competingAttempt);

            var exception = await Should.ThrowAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
            exception.InnerException.ShouldBeOfType<PostgresException>()
                .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        }

        [Fact]
        public async Task Database_RejectsJoiningCheckoutTotalAboveParticipationObligation()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            var customerId = await GetTenantOneCustomerIdAsync();
            var startedAt = DateTime.UtcNow;

            await using var context = CreateDbContext();
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                    "cap-test-2026-08",
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    1200m,
                    600m,
                    600m,
                    7),
                startedAt);
            participation.SelectJoiningPaymentSchedule(
                AQGreenJoiningPaymentSchedule.Full);
            context.EntryParticipations.Add(participation);
            await context.SaveChangesAsync();

            var firstCheckout = AQGreenJoiningCheckout.Create(
                1,
                participation.Id,
                customerId,
                AQGreenJoiningPaymentSchedule.Full,
                AQGreenJoiningPaymentStage.Full,
                1200m,
                "ZAR",
                startedAt);
            firstCheckout.RecordCheckout(
                "ch_cap_full",
                "https://payments.example.test/ch_cap_full",
                startedAt.AddSeconds(1));
            var firstPayment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "Yoco",
                "pay_cap_full",
                startedAt,
                "ZAR");
            firstPayment.Confirm(startedAt.AddSeconds(2));
            context.MemberPayments.Add(firstPayment);
            context.AQGreenJoiningCheckouts.Add(firstCheckout);
            await context.SaveChangesAsync();
            firstCheckout.Complete(firstPayment.Id, startedAt.AddSeconds(2));
            await context.SaveChangesAsync();

            var excessCheckout = AQGreenJoiningCheckout.Create(
                1,
                participation.Id,
                customerId,
                AQGreenJoiningPaymentSchedule.TwoInstallments,
                AQGreenJoiningPaymentStage.FirstInstallment,
                600m,
                "ZAR",
                startedAt.AddSeconds(3));
            excessCheckout.RecordCheckout(
                "ch_cap_excess",
                "https://payments.example.test/ch_cap_excess",
                startedAt.AddSeconds(4));
            var excessPayment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.AQGreenJoining,
                600m,
                "Yoco",
                "pay_cap_excess",
                startedAt.AddSeconds(3),
                "ZAR");
            excessPayment.Confirm(startedAt.AddSeconds(5));
            context.MemberPayments.Add(excessPayment);
            context.AQGreenJoiningCheckouts.Add(excessCheckout);
            await context.SaveChangesAsync();
            excessCheckout.Complete(excessPayment.Id, startedAt.AddSeconds(5));

            var exception = await Should.ThrowAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
            var providerException = exception.InnerException
                .ShouldBeOfType<PostgresException>();

            providerException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            providerException.MessageText.ShouldContain(
                "exceeds the participation joining obligation");
        }

        [Fact]
        public async Task Down_BlockedByConfirmedJoiningPayment_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();

            var customerId = await GetTenantOneCustomerIdAsync();
            var initiatedAt = DateTime.UtcNow;
            var confirmedAt = initiatedAt.AddSeconds(1);

            Guid paymentId;

            await using (var context = CreateDbContext())
            {
                var participation = EntryParticipation.StartIndependently(
                    tenantId: 1,
                    customerId: customerId,
                    terms: EntryProgrammeTerms.CreateSingleJoiningPayment(
                        version: "2026-07-single-1200",
                        effectiveFrom: new DateTime(
                            2026,
                            7,
                            26,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        joiningPaymentAmount: 1200m,
                        monthlyCommitmentAmount: 600m,
                        gracePeriodDays: 7),
                    startedAt: initiatedAt);

                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                var payment = MemberPayment.CreatePending(
                    tenantId: 1,
                    customerId: customerId,
                    purpose: MemberPaymentPurpose.AQGreenJoining,
                    amount: 1200m,
                    provider: "Yoco",
                    externalReference: "chk_test",
                    initiatedAt: initiatedAt);

                payment.Status.ShouldBe(MemberPaymentStatus.Pending);
                payment.ConfirmedAt.ShouldBeNull();

                payment.Confirm(confirmedAt);

                payment.Status.ShouldBe(MemberPaymentStatus.Confirmed);
                payment.ConfirmedAt.ShouldBe(confirmedAt);

                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();

                paymentId = payment.Id;

                context.Entry(participation)
                    .Property("JoiningPaymentId")
                    .CurrentValue = payment.Id;

                await context.SaveChangesAsync();
            }

            await using (var arrangementVerificationContext = CreateDbContext())
            {
                var persistedPayment = await arrangementVerificationContext.MemberPayments
                    .AsNoTracking()
                    .SingleAsync(payment => payment.Id == paymentId);

                persistedPayment.Status.ShouldBe(MemberPaymentStatus.Confirmed);
                persistedPayment.ConfirmedAt.ShouldNotBeNull();

                persistedPayment.ConfirmedAt.Value
                    .ToUniversalTime()
                    .ShouldBe(
                        confirmedAt.ToUniversalTime(),
                        tolerance: TimeSpan.FromMilliseconds(1));

                var persistedJoiningPaymentId =
                    await arrangementVerificationContext.EntryParticipations
                        .Where(participation => participation.CustomerId == customerId)
                        .Select(participation =>
                            EF.Property<Guid?>(
                                participation,
                                "JoiningPaymentId"))
                        .SingleAsync();

                persistedJoiningPaymentId.ShouldBe(paymentId);
            }

            var ex = await Should.ThrowAsync<PostgresException>(async () =>
            {
                await MigrateToAsync(
                    "20260726145201_AddDirectOnyxCheckoutIntents");
            });

            ex.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            ex.MessageText.ShouldContain(
                "Cannot downgrade the AQGreen single-joining-payment migration");

            await using var verifyContext = CreateDbContext();

            var pending = await verifyContext.Database.GetPendingMigrationsAsync();
            pending.ShouldNotContain(
                "20260726162000_AddAQGreenSingleJoiningPayment");

            var applied = await verifyContext.Database.GetAppliedMigrationsAsync();
            applied.ShouldContain(
                "20260726162000_AddAQGreenSingleJoiningPayment");
        }

        [Fact]
        public async Task Down_BlockedByCheckoutRecords_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();

            var customerId = await GetTenantOneCustomerIdAsync();

            await using (var context = CreateDbContext())
            {
                var participation = EntryParticipation.StartIndependently(
                    tenantId: 1,
                    customerId: customerId,
                    terms: EntryProgrammeTerms.CreateSingleJoiningPayment(
                        version: "2026-07-single-1200",
                        effectiveFrom: new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                        joiningPaymentAmount: 1200m,
                        monthlyCommitmentAmount: 600m,
                        gracePeriodDays: 7),
                    startedAt: DateTime.UtcNow);

                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                var checkout = AQGreenJoiningCheckout.Create(
                    tenantId: 1,
                    participationId: participation.Id,
                    customerId: customerId,
                    schedule: AQGreenJoiningPaymentSchedule.Full,
                    stage: AQGreenJoiningPaymentStage.Full,
                    amount: 1200m,
                    currency: "ZAR",
                    createdAt: DateTime.UtcNow);

                context.Set<AQGreenJoiningCheckout>().Add(checkout);
                await context.SaveChangesAsync();
            }

            var ex = await Should.ThrowAsync<Npgsql.PostgresException>(async () =>
            {
                await MigrateToAsync("20260726145201_AddDirectOnyxCheckoutIntents");
            });

            ex.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            ex.MessageText.ShouldContain("Cannot downgrade the AQGreen single-joining-payment migration");

            await using var verifyContext = CreateDbContext();
            var pending = await verifyContext.Database.GetPendingMigrationsAsync();
            pending.ShouldNotContain("20260726162000_AddAQGreenSingleJoiningPayment");

            var checkoutCount = await verifyContext.Set<AQGreenJoiningCheckout>().CountAsync();
            checkoutCount.ShouldBe(1);
        }

        [Fact]
        public async Task Down_Succeeds_When_NoProtectedDataExists_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();

            var customerId = await GetTenantOneCustomerIdAsync();

            await using (var context = CreateDbContext())
            {
                var participation = EntryParticipation.StartIndependently(
                    tenantId: 1,
                    customerId: customerId,
                    terms: EntryProgrammeTerms.CreateSingleJoiningPayment(
                        version: "2026-07-single-1200",
                        effectiveFrom: new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                        joiningPaymentAmount: 1200m,
                        monthlyCommitmentAmount: 600m,
                        gracePeriodDays: 7),
                    startedAt: DateTime.UtcNow);

                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
            }

            await MigrateToAsync("20260726145201_AddDirectOnyxCheckoutIntents");

            await using var verifyContext = CreateDbContext();
            var pending = await verifyContext.Database.GetPendingMigrationsAsync();
            pending.ShouldContain("20260726162000_AddAQGreenSingleJoiningPayment");

            var applied = await verifyContext.Database.GetAppliedMigrationsAsync();
            applied.ShouldNotContain("20260726162000_AddAQGreenSingleJoiningPayment");

            var hasJoiningPaymentColumn = await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'EntryParticipations' AND column_name = 'JoiningPaymentId'");
            hasJoiningPaymentColumn.ShouldBe(0);

            var hasCheckoutTable = await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AQGreenJoiningCheckouts'");
            hasCheckoutTable.ShouldBe(0);

            var hasBackupTable = await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AQGreenMigrationBackup'");
            hasBackupTable.ShouldBe(0);
        }

        [Fact]
        public async Task DuePolicyHistory_RejectsDirectUpdateAndDelete_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            await using (var context = CreateDbContext())
            {
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "append-only-v1",
                        10,
                        EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 9)));
                await context.SaveChangesAsync();
            }

            await using (var updateContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await updateContext.Database.ExecuteSqlRawAsync(
                        "UPDATE \"EntryMonthlyObligationDuePolicies\" SET \"DueDayOfMonth\" = 11 WHERE \"Version\" = 'append-only-v1'"));
            }

            await using (var deleteContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await deleteContext.Database.ExecuteSqlRawAsync(
                        "DELETE FROM \"EntryMonthlyObligationDuePolicies\" WHERE \"Version\" = 'append-only-v1'"));
            }
        }

        [Fact]
        public async Task DuePolicyMigration_RefusesRollbackAfterEvidenceExists_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            await using (var context = CreateDbContext())
            {
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "rollback-protected-v1",
                        10,
                        EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 9)));
                await context.SaveChangesAsync();
            }

            await Should.ThrowAsync<PostgresException>(async () =>
                await MigrateToAsync(
                    "20260809043240_AddAQGreenFuneralCoverEntitlements"));
        }

        [Fact]
        public async Task AreaActivationHistory_RejectsDirectUpdateAndDelete_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            int tenantId;
            await using (var context = CreateDbContext())
            {
                var recordedAt = DateTime.UtcNow;
                var tenant = new Tenant(
                    "area-history-append-only",
                    "Area history append-only");
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
                tenantId = tenant.Id;
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        tenantId,
                        true,
                        recordedAt,
                        recordedAt,
                        null,
                        "PostgreSQL append-only test",
                        AreaActivationStateRecordKind.ObservedBaseline));
                await context.SaveChangesAsync();
            }

            await using (var updateContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await updateContext.Database.ExecuteSqlAsync(
                        $"UPDATE \"AreaActivationStateRecords\" SET \"IsActive\" = FALSE WHERE \"TenantId\" = {tenantId}"));
            }

            await using (var deleteContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await deleteContext.Database.ExecuteSqlAsync(
                        $"DELETE FROM \"AreaActivationStateRecords\" WHERE \"TenantId\" = {tenantId}"));
            }

            await using (var truncateContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await truncateContext.Database.ExecuteSqlRawAsync(
                        "TRUNCATE TABLE \"AreaActivationStateRecords\""));
            }
        }

        [Fact]
        public async Task AreaActivationHistoryMigration_RefusesRollbackAfterEvidenceExists_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            await using (var context = CreateDbContext())
            {
                var recordedAt = DateTime.UtcNow;
                var tenant = new Tenant(
                    "area-history-rollback",
                    "Area history rollback");
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        tenant.Id,
                        true,
                        recordedAt,
                        recordedAt,
                        null,
                        "PostgreSQL rollback protection test",
                        AreaActivationStateRecordKind.ObservedBaseline));
                await context.SaveChangesAsync();
            }

            await Should.ThrowAsync<PostgresException>(async () =>
                await MigrateToAsync(
                    "20260809054416_AddAQGreenMonthlyObligationDuePolicies"));

            await using var verifyContext = CreateDbContext();
            var applied = await verifyContext.Database.GetAppliedMigrationsAsync();
            applied.ShouldContain("20260809081746_AddAreaActivationStateHistory");
        }

        [Fact]
        public async Task MonthlyCheckoutMigration_AppliesAndRollsBackWhenEmpty_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(PreviousMonthlyCheckoutMigration);
            await ExecuteAsync(
                BuildTestConnectionString(),
                """
                INSERT INTO "AbpRoles"
                    ("Id", "CreationTime", "IsDeleted", "TenantId", "Name", "DisplayName", "IsStatic", "IsDefault", "NormalizedName")
                VALUES
                    (9101, NOW(), FALSE, 1, 'Member', 'Area 1 Member', TRUE, FALSE, 'MEMBER'),
                    (9102, NOW(), FALSE, 2, 'Member', 'Area 2 Member', TRUE, FALSE, 'MEMBER');
                """);

            await MigrateToAsync(MonthlyCheckoutMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AQGreenMonthlyObligationCheckouts'"))
                .ShouldBe(1);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EntryMonthlyObligations' AND indexname = 'IX_EntryMonthlyObligations_PaymentId' AND indexdef LIKE 'CREATE UNIQUE INDEX%'"))
                .ShouldBe(1);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'AQGreenMonthlyObligationCheckouts' AND indexname = 'IX_AQGreenMonthlyObligationCheckouts_EntryMonthlyObligationId' AND indexdef LIKE 'CREATE UNIQUE INDEX%' AND indexdef LIKE '%WHERE (\"Status\" = ANY (ARRAY[0, 1, 2]))%'"))
                .ShouldBe(1);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'AQGreenMonthlyObligationCheckouts' AND indexname = 'IX_AQGreenMonthlyObligationCheckouts_PaymentId' AND indexdef LIKE 'CREATE INDEX%'"))
                .ShouldBe(1);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AbpPermissions\" WHERE \"Name\" = 'Aqua.EntryMonthlyObligations.Pay' AND \"RoleId\" IN (9101, 9102) AND \"IsGranted\" = TRUE"))
                .ShouldBe(2);

            await MigrateToAsync(PreviousMonthlyCheckoutMigration);

            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AQGreenMonthlyObligationCheckouts'"))
                .ShouldBe(0);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EntryMonthlyObligations' AND indexname = 'IX_EntryMonthlyObligations_PaymentId' AND indexdef LIKE 'CREATE INDEX%'"))
                .ShouldBe(1);
            (await CountAsync(
                BuildTestConnectionString(),
                "SELECT COUNT(*) FROM \"AbpPermissions\" WHERE \"Name\" = 'Aqua.EntryMonthlyObligations.Pay' AND \"RoleId\" IN (9101, 9102) AND \"IsGranted\" = TRUE"))
                .ShouldBe(2);
        }

        [Fact]
        public async Task MonthlyCheckoutMigration_RejectsDuplicatePaymentAssociations_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToAsync(PreviousMonthlyCheckoutMigration);
            await SeedMinimalUserAsync();
            var obligations = await SeedMonthlyObligationsAsync();
            await using (var context = CreateDbContext())
            {
                var payment = CreateConfirmedPayment(
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    "duplicate-monthly-association",
                    DateTime.UtcNow);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "EntryMonthlyObligations"
                    SET "PaymentId" = {payment.Id}
                    WHERE "Id" IN ({obligations.First.Id}, {obligations.Second.Id})
                    """);
            }

            var ex = await Should.ThrowAsync<PostgresException>(async () =>
                await MigrateToAsync(MonthlyCheckoutMigration));

            ex.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            ex.MessageText.ShouldContain("duplicate payment associations exist");
            await using var verifyContext = CreateDbContext();
            (await verifyContext.Database.GetAppliedMigrationsAsync())
                .ShouldNotContain(MonthlyCheckoutMigration);
        }

        [Fact]
        public async Task MonthlyCheckoutSchema_EnforcesActiveUniquenessAndProtectsEvidence_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            var obligations = await SeedMonthlyObligationsAsync();
            await using (var context = CreateDbContext())
            {
                context.AQGreenMonthlyObligationCheckouts.Add(
                    AQGreenMonthlyObligationCheckout.Create(
                        obligations.First,
                        DateTime.UtcNow));
                await context.SaveChangesAsync();
            }

            await using (var duplicateContext = CreateDbContext())
            {
                duplicateContext.AQGreenMonthlyObligationCheckouts.Add(
                    AQGreenMonthlyObligationCheckout.Create(
                        obligations.First,
                        DateTime.UtcNow.AddSeconds(1)));
                var duplicate = await Should.ThrowAsync<DbUpdateException>(async () =>
                    await duplicateContext.SaveChangesAsync());
                ((PostgresException)duplicate.InnerException).SqlState
                    .ShouldBe(PostgresErrorCodes.UniqueViolation);
            }

            var rollback = await Should.ThrowAsync<PostgresException>(async () =>
                await MigrateToAsync(PreviousMonthlyCheckoutMigration));

            rollback.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            rollback.MessageText.ShouldContain(
                "Cannot remove AQGreen monthly checkout schema");
            await using var verifyContext = CreateDbContext();
            (await verifyContext.Database.GetAppliedMigrationsAsync())
                .ShouldContain(MonthlyCheckoutMigration);
            (await verifyContext.AQGreenMonthlyObligationCheckouts.CountAsync())
                .ShouldBe(1);
        }

        [Fact]
        public async Task CommissionTermsVersions_RejectDirectUpdateAndDelete_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            await using (var context = CreateDbContext())
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "append-only-entry-v1",
                        new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                        150m,
                        250m,
                        1250m));
                context.OnyxCommissionTermsVersions.Add(
                    OnyxCommissionTermsVersion.Create(
                        "append-only-onyx-v1",
                        new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                        50m,
                        20m,
                        12.62m,
                        5m,
                        4m));
                await context.SaveChangesAsync();
            }

            await using (var updateContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await updateContext.Database.ExecuteSqlRawAsync(
                        "UPDATE \"EntryCommissionTermsVersions\" SET \"LevelOneComponentAmount\" = 1 WHERE \"Version\" = 'append-only-entry-v1'"));
            }

            await using (var deleteContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await deleteContext.Database.ExecuteSqlRawAsync(
                        "DELETE FROM \"OnyxCommissionTermsVersions\" WHERE \"Version\" = 'append-only-onyx-v1'"));
            }

            await using (var truncateContext = CreateDbContext())
            {
                await Should.ThrowAsync<PostgresException>(async () =>
                    await truncateContext.Database.ExecuteSqlRawAsync(
                        "TRUNCATE TABLE \"EntryCommissionTermsVersions\""));
            }
        }

        [Fact]
        public async Task CommissionTermsVersionsMigration_RefusesRollbackAfterEvidenceExists_PostgreSQL()
        {
            await ResetDatabaseAsync();
            await MigrateToLatestAsync();
            await using (var context = CreateDbContext())
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "rollback-protected-entry-v1",
                        new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                        150m,
                        250m,
                        1250m));
                await context.SaveChangesAsync();
            }

            var ex = await Should.ThrowAsync<PostgresException>(async () =>
                await MigrateToAsync(PreviousMonthlyCheckoutMigration));

            ex.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            ex.MessageText.ShouldContain(
                "Cannot remove commission terms versions after evidence has been recorded");
            await using var verifyContext = CreateDbContext();
            (await verifyContext.Database.GetAppliedMigrationsAsync())
                .ShouldContain(TermsVersionsMigration);
        }

        private async Task<(EntryMonthlyObligation First, EntryMonthlyObligation Second)>
            SeedMonthlyObligationsAsync()
        {
            var customerId = await GetTenantOneCustomerIdAsync();
            var startedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                EntryProgrammeTerms.Create(
                    $"migration-{suffix}",
                    startedAt,
                    600m,
                    600m,
                    600m,
                    7),
                startedAt);
            var registration = CreateConfirmedPayment(
                MemberPaymentPurpose.EntryRegistration,
                $"migration-registration-{Guid.NewGuid():N}",
                startedAt);
            var activation = CreateConfirmedPayment(
                MemberPaymentPurpose.EntryActivation,
                $"migration-activation-{Guid.NewGuid():N}",
                startedAt.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(registration);
            participation.ApplyConfirmedActivationPayment(activation);
            participation.ApproveByAdministrator(1, startedAt.AddMinutes(4));
            var policyVersion = $"migration-policy-{suffix}";
            var first = EntryMonthlyObligation.Create(
                participation,
                2026,
                6,
                new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                policyVersion);
            var second = EntryMonthlyObligation.Create(
                participation,
                2026,
                7,
                new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                policyVersion);
            await using var context = CreateDbContext();
            context.EntryMonthlyObligationDuePolicies.Add(
                EntryMonthlyObligationDuePolicy.Create(
                    policyVersion,
                    10,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(
                        2026,
                        6)));
            context.MemberPayments.AddRange(registration, activation);
            context.EntryParticipations.Add(participation);
            context.EntryMonthlyObligations.AddRange(first, second);
            await context.SaveChangesAsync();
            return (first, second);
        }

        private static MemberPayment CreateConfirmedPayment(
            MemberPaymentPurpose purpose,
            string reference,
            DateTime initiatedAt)
        {
            var payment = MemberPayment.CreatePending(
                1,
                1,
                purpose,
                600m,
                "MigrationTest",
                reference,
                initiatedAt);
            payment.Confirm(initiatedAt.AddMinutes(1));
            return payment;
        }

        private void TraceLine(string message)
        {
            Console.WriteLine($"[AQGreenMigrationTest] {message}");
        }
    }
}
