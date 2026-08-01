using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class AQGreenMigrationRollbackPostgreSqlTests : IAsyncLifetime
    {
        static AQGreenMigrationRollbackPostgreSqlTests()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        private const string PostgresImage = "postgres:16-alpine";
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

        private void TraceLine(string message)
        {
            Console.WriteLine($"[AQGreenMigrationTest] {message}");
        }
    }
}
