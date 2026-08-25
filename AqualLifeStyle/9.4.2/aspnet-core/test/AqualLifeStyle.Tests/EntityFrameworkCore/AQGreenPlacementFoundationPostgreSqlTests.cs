using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class AQGreenPlacementFoundationPostgreSqlTests : IAsyncLifetime
    {
        private const string PreviousMigration =
            "20260821120000_WidenCommissionRulesVersions";
        private const string FoundationMigration =
            "20260825095740_AddAQGreenPlacementV2Foundation";
        private readonly string _containerName =
            $"aqgreen-placement-foundation-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName =
            $"aqgreen_placement_foundation_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public AQGreenPlacementFoundationPostgreSqlTests()
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
            await MigrateAsync(FoundationMigration);
            await SeedParticipantsAsync();
        }

        public async Task DisposeAsync() =>
            await RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);

        [Fact]
        public async Task Migration_AppliesOnlyFoundationSchemaAndRollsBackWhenEmpty()
        {
            (await ScalarAsync(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('AQGreenPlacementTreeScopes', 'AQGreenNetworkPlacements');"))
                .ShouldBe(2);
            (await ScalarAsync(
                    "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name IN ('AQGreenPlacementTreeScopes', 'AQGreenNetworkPlacements') AND column_name IN ('AreaId', 'PlacementSequence', 'CreditedSponsorParticipantId');"))
                .ShouldBe(0);

            var rollbackDatabase = $"aqgreen_placement_rollback_{Guid.NewGuid():N}";
            var rollbackConnectionString = BuildConnectionString(rollbackDatabase);
            await ExecuteAsync(
                AdminConnectionString,
                $"CREATE DATABASE \"{rollbackDatabase}\" WITH OWNER = aqualifestyle;");

            await MigrateAsync(FoundationMigration, rollbackConnectionString);
            await MigrateAsync(PreviousMigration, rollbackConnectionString);

            (await ScalarAsync(
                    rollbackConnectionString,
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('AQGreenPlacementTreeScopes', 'AQGreenNetworkPlacements');"))
                .ShouldBe(0);
            (await ScalarAsync(
                    rollbackConnectionString,
                    "SELECT COUNT(*) FROM pg_constraint WHERE conname = 'AK_EntryParticipations_TenantId_Id';"))
                .ShouldBe(0);
        }

        [Fact]
        public async Task ValidTopology_DerivesPathAndAllowsSameTenantCrossAreaParentage()
        {
            var scopeId = Guid.NewGuid();
            await using var connection = await OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await InsertScopeAndRootAsync(
                connection,
                transaction,
                1,
                scopeId,
                Participant(1));
            await InsertPlacementAsync(
                connection,
                transaction,
                1,
                scopeId,
                Participant(2),
                Participant(1),
                1,
                "1");
            await InsertPlacementAsync(
                connection,
                transaction,
                1,
                scopeId,
                Participant(3),
                Participant(2),
                5,
                "15");
            await ExecuteAsync(connection, transaction, "SET CONSTRAINTS ALL IMMEDIATE;");

            (await StringScalarAsync(
                    connection,
                    transaction,
                    $"SELECT \"CanonicalPath\" FROM \"AQGreenNetworkPlacements\" WHERE \"ParticipantId\" = '{Participant(3)}';"))
                .ShouldBe("15");
            (await ScalarAsync(
                    connection,
                    transaction,
                    $$"""
                    SELECT COUNT(DISTINCT c."AreaId")
                    FROM "AQGreenNetworkPlacements" p
                    JOIN "EntryParticipations" ep
                      ON ep."TenantId" = p."TenantId" AND ep."Id" = p."ParticipantId"
                    JOIN "Customers" c
                      ON c."TenantId" = ep."TenantId" AND c."Id" = ep."CustomerId"
                    WHERE p."ParticipantId" IN ('{{Participant(1)}}', '{{Participant(2)}}');
                    """))
                .ShouldBe(2);
            await transaction.RollbackAsync();
        }

        [Fact]
        public async Task EfMapping_PersistsScopeRootAndChildAtomically()
        {
            var scope = AQGreenPlacementTreeScope.Create(1);
            var root = AQGreenNetworkPlacement.CreateRoot(
                scope,
                Guid.Parse(Participant(4)),
                new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc),
                AQGreenPlacementRules.CurrentVersion);
            var child = AQGreenNetworkPlacement.CreateChild(
                root,
                Guid.Parse(Participant(5)),
                3,
                root.PlacedAt.AddMinutes(1),
                AQGreenPlacementRules.CurrentVersion);

            await using (var context = CreateDbContext())
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                context.AddRange(scope, root, child);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE;");
                var reloaded = await context.AQGreenNetworkPlacements
                    .AsNoTracking()
                    .SingleAsync(placement => placement.Id == child.Id);
                reloaded.TenantId.ShouldBe(1);
                reloaded.PlacementTreeScopeId.ShouldBe(scope.Id);
                reloaded.PlacementParentParticipantId.ShouldBe(root.ParticipantId);
                reloaded.PlacementSlot.ShouldBe(3);
                reloaded.CanonicalPath.ShouldBe("3");
                await transaction.RollbackAsync();
            }
        }

        [Fact]
        public async Task Scope_RequiresExactlyOneRoot()
        {
            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        $"INSERT INTO \"AQGreenPlacementTreeScopes\" (\"Id\", \"TenantId\") VALUES ('{Guid.NewGuid()}', 1);");
                },
                PostgresErrorCodes.RaiseException);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(
                        connection,
                        transaction,
                        1,
                        scopeId,
                        Participant(4));
                    await InsertPlacementAsync(
                        connection,
                        transaction,
                        1,
                        scopeId,
                        Participant(5),
                        null,
                        null,
                        string.Empty);
                },
                PostgresErrorCodes.UniqueViolation);
        }

        [Fact]
        public async Task Uniqueness_RejectsDuplicateParticipantAndParentSlot()
        {
            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(
                        connection,
                        transaction,
                        1,
                        scopeId,
                        Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), 1, "1");
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), 2, "2");
                },
                PostgresErrorCodes.UniqueViolation);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(
                        connection,
                        transaction,
                        1,
                        scopeId,
                        Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), 1, "1");
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(3), Participant(1), 1, "1");
                },
                PostgresErrorCodes.UniqueViolation);
        }

        [Fact]
        public async Task ShapeChecks_RejectInvalidSlotsRootsAndSelfParenting()
        {
            foreach (var invalidSlot in new[] { 0, 6 })
            {
                await ExpectRejectedAsync(
                    async (connection, transaction) =>
                    {
                        var scopeId = Guid.NewGuid();
                        await InsertScopeAndRootAsync(connection, transaction, 1,
                            scopeId, Participant(1));
                        await InsertPlacementAsync(connection, transaction, 1, scopeId,
                            Participant(2), Participant(1), invalidSlot,
                            invalidSlot.ToString());
                    },
                    PostgresErrorCodes.CheckViolation);
            }

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await ExecuteAsync(connection, transaction,
                        $"INSERT INTO \"AQGreenPlacementTreeScopes\" (\"Id\", \"TenantId\") VALUES ('{scopeId}', 1);");
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(1), null, 1, "1");
                },
                PostgresErrorCodes.CheckViolation);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        scopeId, Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), null, "1");
                },
                PostgresErrorCodes.CheckViolation);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        scopeId, Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(2), 1, "1");
                },
                PostgresErrorCodes.CheckViolation);
        }

        [Fact]
        public async Task CompositeForeignKeys_RejectCrossTenantAndCrossScopeReferences()
        {
            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        scopeId, Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(9), Participant(1), 1, "1");
                },
                PostgresErrorCodes.ForeignKeyViolation);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var tenantOneScope = Guid.NewGuid();
                    var tenantTwoScope = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        tenantOneScope, Participant(1));
                    await InsertScopeAndRootAsync(connection, transaction, 2,
                        tenantTwoScope, Participant(9));
                    await InsertPlacementAsync(connection, transaction, 2,
                        tenantTwoScope, Participant(10), Participant(1), 1, "1");
                },
                PostgresErrorCodes.RaiseException);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var firstScope = Guid.NewGuid();
                    var secondScope = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        firstScope, Participant(1));
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        secondScope, Participant(2));
                    await InsertPlacementAsync(connection, transaction, 1,
                        secondScope, Participant(3), Participant(1), 1, "1");
                },
                PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task CanonicalPath_MustExactlyMatchParentPathAndSlot()
        {
            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        scopeId, Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), 1, "2");
                },
                PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task InsertTrigger_RejectsInvalidTimeAndWhitespaceRulesVersion()
        {
            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        scopeId, Participant(1));
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), 1, "1",
                        placedAt: "2026-08-25 09:59:59+00");
                },
                PostgresErrorCodes.RaiseException);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await ExecuteAsync(connection, transaction,
                        $"INSERT INTO \"AQGreenPlacementTreeScopes\" (\"Id\", \"TenantId\") VALUES ('{scopeId}', 1);");
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(1), null, null, string.Empty,
                        rulesVersion: "\t");
                },
                PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task CrossRowTriggers_CannotBeBypassedByTemporaryTableShadowing()
        {
            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await CreateShadowPlacementTableAsync(connection, transaction);
                    await ExecuteAsync(connection, transaction,
                        $"INSERT INTO \"AQGreenNetworkPlacements\" (\"TenantId\", \"PlacementTreeScopeId\", \"ParticipantId\", \"PlacementParentParticipantId\", \"CanonicalPath\", \"PlacedAt\") VALUES (1, '{scopeId}', '{Participant(1)}', NULL, '', TIMESTAMPTZ '2026-08-25 10:00:00+00');");
                    await ExecuteAsync(connection, transaction,
                        $"INSERT INTO public.\"AQGreenPlacementTreeScopes\" (\"Id\", \"TenantId\") VALUES ('{scopeId}', 1);");
                },
                PostgresErrorCodes.RaiseException);

            await ExpectRejectedAsync(
                async (connection, transaction) =>
                {
                    var scopeId = Guid.NewGuid();
                    await InsertScopeAndRootAsync(connection, transaction, 1,
                        scopeId, Participant(1));
                    await CreateShadowPlacementTableAsync(connection, transaction);
                    await ExecuteAsync(connection, transaction,
                        $"INSERT INTO \"AQGreenNetworkPlacements\" (\"TenantId\", \"PlacementTreeScopeId\", \"ParticipantId\", \"PlacementParentParticipantId\", \"CanonicalPath\", \"PlacedAt\") VALUES (1, '{scopeId}', '{Participant(1)}', NULL, '4', TIMESTAMPTZ '2026-08-25 10:00:00+00');");
                    await InsertPlacementAsync(connection, transaction, 1, scopeId,
                        Participant(2), Participant(1), 1, "41");
                },
                PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task TopologyEvidence_RejectsDirectUpdateDeleteAndTruncate()
        {
            var scopeId = Guid.NewGuid();
            await using (var connection = await OpenConnectionAsync())
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await InsertScopeAndRootAsync(connection, transaction, 1,
                    scopeId, Participant(7));
                await InsertPlacementAsync(connection, transaction, 1, scopeId,
                    Participant(8), Participant(7), 1, "1");
                await transaction.CommitAsync();
            }

            var statements = new[]
            {
                $"UPDATE \"AQGreenNetworkPlacements\" SET \"RulesVersion\" = 'changed' WHERE \"ParticipantId\" = '{Participant(8)}';",
                $"DELETE FROM \"AQGreenNetworkPlacements\" WHERE \"ParticipantId\" = '{Participant(8)}';",
                $"UPDATE \"AQGreenPlacementTreeScopes\" SET \"TenantId\" = 2 WHERE \"Id\" = '{scopeId}';",
                $"DELETE FROM \"AQGreenPlacementTreeScopes\" WHERE \"Id\" = '{scopeId}';",
                "TRUNCATE TABLE \"AQGreenNetworkPlacements\";",
                "TRUNCATE TABLE \"AQGreenPlacementTreeScopes\" CASCADE;"
            };

            foreach (var statement in statements)
            {
                var exception = await Should.ThrowAsync<PostgresException>(() =>
                    ExecuteAsync(TestConnectionString, statement));
                exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            }

            var rollback = await Should.ThrowAsync<PostgresException>(() =>
                MigrateAsync(PreviousMigration));
            rollback.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            (await ScalarAsync(
                    $"SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{FoundationMigration}';"))
                .ShouldBe(1);
        }

        [Fact]
        public async Task Down_WaitsForConcurrentWriterAndThenRefusesCommittedEvidence()
        {
            var rollbackDatabase = $"aqgreen_placement_concurrent_down_{Guid.NewGuid():N}";
            var rollbackConnectionString = BuildConnectionString(rollbackDatabase);
            await ExecuteAsync(
                AdminConnectionString,
                $"CREATE DATABASE \"{rollbackDatabase}\" WITH OWNER = aqualifestyle;");
            await MigrateAsync(FoundationMigration, rollbackConnectionString);
            await SeedParticipantsAsync(rollbackConnectionString);

            await using var writerConnection = new NpgsqlConnection(rollbackConnectionString);
            await writerConnection.OpenAsync();
            await using var writerTransaction = await writerConnection.BeginTransactionAsync();
            await InsertScopeAndRootAsync(
                writerConnection,
                writerTransaction,
                1,
                Guid.NewGuid(),
                Participant(6));

            var rollbackTask = MigrateAsync(PreviousMigration, rollbackConnectionString);
            await Task.Delay(500);
            rollbackTask.IsCompleted.ShouldBeFalse();

            await writerTransaction.CommitAsync();
            var exception = await Should.ThrowAsync<PostgresException>(() => rollbackTask);
            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            (await ScalarAsync(
                    rollbackConnectionString,
                    $"SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{FoundationMigration}';"))
                .ShouldBe(1);
        }

        private async Task SeedParticipantsAsync(string connectionString = null)
        {
            await ExecuteAsync(
                connectionString ?? TestConnectionString,
                $$"""
                INSERT INTO "AbpTenants" (
                    "Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES
                    (1, 'placement-one', 'Placement One', TRUE, NOW(), FALSE),
                    (2, 'placement-two', 'Placement Two', TRUE, NOW(), FALSE);

                INSERT INTO "Areas" (
                    "Id", "TenantId", "Code", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES
                    ('a0000000-0000-0000-0000-000000000001', 1, 'ONE', 'Area One', TRUE, NOW(), FALSE),
                    ('a0000000-0000-0000-0000-000000000002', 1, 'TWO', 'Area Two', TRUE, NOW(), FALSE),
                    ('a0000000-0000-0000-0000-000000000003', 2, 'THREE', 'Area Three', TRUE, NOW(), FALSE);

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id + 100, 1, 'placement-user-' || id,
                    'placement-user-' || id || '@example.test', 'Placement', id::text,
                    'PLACEMENT-USER-' || id,
                    'PLACEMENT-USER-' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(1, 8) id;

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id + 200, 2, 'placement-user-two-' || id,
                    'placement-user-two-' || id || '@example.test', 'Placement Two', id::text,
                    'PLACEMENT-USER-TWO-' || id,
                    'PLACEMENT-USER-TWO-' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(1, 4) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id, 1, 'Placement Customer ' || id,
                    'placement-customer-' || id || '@example.test',
                    CASE WHEN id % 2 = 1
                        THEN 'a0000000-0000-0000-0000-000000000001'::uuid
                        ELSE 'a0000000-0000-0000-0000-000000000002'::uuid END,
                    TRUE, NOW(), 'CLB-PLACE-' || id, id + 100, FALSE
                FROM generate_series(1, 8) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id + 8, 2, 'Placement Tenant Two ' || id,
                    'placement-tenant-two-' || id || '@example.test',
                    'a0000000-0000-0000-0000-000000000003'::uuid,
                    TRUE, NOW(), 'CLB-PLACE-TWO-' || id, id + 200, FALSE
                FROM generate_series(1, 4) id;

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    ('10000000-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
                    1, id, 2, TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    'entry-terms-v1', TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 8) id;

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    ('10000000-0000-0000-0000-' || lpad((id + 8)::text, 12, '0'))::uuid,
                    2, id + 8, 2, TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    'entry-terms-v1', TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 4) id;
                """);
        }

        private async Task ExpectRejectedAsync(
            Func<NpgsqlConnection, NpgsqlTransaction, Task> action,
            string expectedSqlState)
        {
            var exception = await Should.ThrowAsync<PostgresException>(async () =>
            {
                await using var connection = await OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await action(connection, transaction);
                await transaction.CommitAsync();
            });
            exception.SqlState.ShouldBe(expectedSqlState);
        }

        private static async Task InsertScopeAndRootAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int tenantId,
            Guid scopeId,
            string participantId)
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"INSERT INTO public.\"AQGreenPlacementTreeScopes\" (\"Id\", \"TenantId\") VALUES ('{scopeId}', {tenantId});");
            await InsertPlacementAsync(
                connection,
                transaction,
                tenantId,
                scopeId,
                participantId,
                null,
                null,
                string.Empty);
        }

        private static Task InsertPlacementAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int tenantId,
            Guid scopeId,
            string participantId,
            string parentParticipantId,
            int? slot,
            string canonicalPath,
            string placedAt = "2026-08-25 10:00:00+00",
            string rulesVersion = "AQGreenPlacementV2")
        {
            var parentSql = parentParticipantId == null
                ? "NULL"
                : $"'{parentParticipantId}'";
            var slotSql = slot?.ToString() ?? "NULL";
            return ExecuteAsync(
                connection,
                transaction,
                $$"""
                INSERT INTO public."AQGreenNetworkPlacements" (
                    "Id", "TenantId", "PlacementTreeScopeId", "ParticipantId",
                    "PlacementParentParticipantId", "PlacementSlot", "CanonicalPath",
                    "PlacedAt", "RulesVersion")
                VALUES (
                    '{{Guid.NewGuid()}}', {{tenantId}}, '{{scopeId}}', '{{participantId}}',
                    {{parentSql}}, {{slotSql}}, '{{canonicalPath}}',
                    TIMESTAMPTZ '{{placedAt}}', '{{rulesVersion}}');
                """);
        }

        private static Task CreateShadowPlacementTableAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction) =>
            ExecuteAsync(
                connection,
                transaction,
                """
                CREATE TEMP TABLE "AQGreenNetworkPlacements" (
                    "TenantId" integer NOT NULL,
                    "PlacementTreeScopeId" uuid NOT NULL,
                    "ParticipantId" uuid NOT NULL,
                    "PlacementParentParticipantId" uuid NULL,
                    "CanonicalPath" text NOT NULL,
                    "PlacedAt" timestamp with time zone NOT NULL);
                """);

        private async Task MigrateAsync(
            string migration,
            string connectionString = null)
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(connectionString ?? TestConnectionString)
                    .Options);
            await context.GetService<IMigrator>().MigrateAsync(migration);
        }

        private AqualLifeStyleDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseNpgsql(TestConnectionString)
                .Options);

        private async Task<NpgsqlConnection> OpenConnectionAsync()
        {
            var connection = new NpgsqlConnection(TestConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        private async Task<long> ScalarAsync(string sql) =>
            await ScalarAsync(TestConnectionString, sql);

        private static async Task<long> ScalarAsync(
            string connectionString,
            string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        private static async Task<long> ScalarAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        private static async Task<string> StringScalarAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            return Convert.ToString(await command.ExecuteScalarAsync());
        }

        private static async Task ExecuteAsync(
            string connectionString,
            string sql)
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
            {
                throw new InvalidOperationException($"Docker command failed: {error}");
            }
        }

        private static string Participant(int number) =>
            $"10000000-0000-0000-0000-{number:D12}";

        private string BuildConnectionString(string database) =>
            $"Host=localhost;Port={_hostPort};Database={database};Username=aqualifestyle;Password=aqualifestyle";

        private string AdminConnectionString => BuildConnectionString("postgres");
        private string TestConnectionString => BuildConnectionString(_databaseName);
    }
}
