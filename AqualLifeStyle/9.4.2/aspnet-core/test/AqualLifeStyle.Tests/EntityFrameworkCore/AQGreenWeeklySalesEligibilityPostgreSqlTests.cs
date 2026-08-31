using System;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenWeeklySalesEligibilityPostgreSqlTests
    {
        private const string PreviousMigration =
            "20260828205640_AddAQGreenV2GraduationEvidence";
        private static readonly DateTime WeekStartUtc =
            new(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime ReviewedAtUtc =
            WeekStartUtc.AddDays(7);
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;

        public AQGreenWeeklySalesEligibilityPostgreSqlTests(
            AQGreenPlacementAllocatorPostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ConfirmedMet_RoundTripsThroughFailClosedReader()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                5,
                5,
                AQGreenWeeklySalesThresholdResult.Met,
                null);
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            var reader = new AQGreenWeeklySalesEligibilityDecisionReader(provider);

            var snapshot = await reader.GetFinalDecisionAsync(
                1,
                P(1),
                WeekStartUtc,
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);

            snapshot.DecisionId.ShouldBe(decisionId);
            snapshot.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Confirmed);
            snapshot.ThresholdResult.ShouldBe(AQGreenWeeklySalesThresholdResult.Met);
            snapshot.ReviewedAt.ShouldBe(ReviewedAtUtc);
        }

        [Fact]
        public async Task ConfirmedNotMet_IsPersistableAndReaderRechecksEvaluator()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                4,
                5,
                AQGreenWeeklySalesThresholdResult.NotMet,
                null);

            (await ScalarAsync<int>(
                    database,
                    "SELECT \"ThresholdResult\" FROM \"AQGreenWeeklySalesEligibilityDecisions\""))
                .ShouldBe((int)AQGreenWeeklySalesThresholdResult.NotMet);
        }

        [Fact]
        public async Task Reader_FailsClosedForMissingHeldAndEvaluatorMismatch()
        {
            await using var database = await _fixture.CreateDatabaseAsync();

            await Should.ThrowAsync<AQGreenWeeklySalesEligibilityUnavailableException>(() =>
                ReadAsync(database, P(1)));

            await ExecuteAsync(database, BaseInsertSql(Guid.NewGuid(), 1));
            await Should.ThrowAsync<AQGreenWeeklySalesEligibilityUnavailableException>(() =>
                ReadAsync(database, P(1)));

            await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                5,
                5,
                AQGreenWeeklySalesThresholdResult.NotMet,
                null,
                participantNumber: 2);
            await Should.ThrowAsync<AQGreenWeeklySalesEligibilityIntegrityException>(() =>
                ReadAsync(database, P(2)));
            await Should.ThrowAsync<AQGreenWeeklySalesEligibilityUnavailableException>(() =>
                ReadAsync(database, AQGreenPlacementAllocatorPostgreSqlFixture.Participant(2, 1)));
        }

        [Fact]
        public async Task Rejected_HasNoQuantitiesOrThresholdAndIsImmutable()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Rejected,
                null,
                null,
                null,
                null,
                "evidence could not be verified");

            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEligibilityDecisions\" " +
                    "WHERE \"ReviewStatus\" = 3 AND \"ThresholdResult\" IS NULL " +
                    "AND \"ReviewedSprayQuantity\" IS NULL AND \"ReviewedOneLitreQuantity\" IS NULL " +
                    "AND \"ReviewedFiveLitreQuantity\" IS NULL"))
                .ShouldBe(1);

            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                $"UPDATE \"AQGreenWeeklySalesEligibilityDecisions\" SET \"RejectionReason\" = 'changed' WHERE \"Id\" = '{decisionId}'"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                $"DELETE FROM \"AQGreenWeeklySalesEligibilityDecisions\" WHERE \"Id\" = '{decisionId}'"));
        }

        [Fact]
        public async Task DatabaseRejectsDirectFinalInvalidShapeAndLateEvidence()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 2) + ";"));

            var decisionId = await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                5,
                5,
                AQGreenWeeklySalesThresholdResult.Met,
                null);
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                EvidenceInsertSql(Guid.NewGuid(), decisionId, "ticket:late") + ";"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                "TRUNCATE TABLE \"AQGreenWeeklySalesEvidenceReferences\""));
        }

        [Fact]
        public async Task DatabaseRejectsCrossTenantUnsupportedVersionAndNonCanonicalWeek()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 1)
                    .Replace("AQGreenWeeklySalesEligibilityV1", "unsupported")));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 1)
                    .Replace("2026-08-20 22:00:00+00", "2026-08-20 23:00:00+00")));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 1)
                    .Replace("VALUES (", "VALUES (")
                    .Replace(", 1, '00000001", ", 2, '00000001")));
        }

        [Fact]
        public async Task NaturalIdentityAndTenantCoherentForeignKeysAreDatabaseEnforced()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = Guid.NewGuid();
            await ExecuteAsync(database, BaseInsertSql(decisionId, 1, 3));

            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 1, 3)));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 1, 4)
                    .Replace($"'{P(4)}'", $"'{AQGreenPlacementAllocatorPostgreSqlFixture.Participant(2, 4)}'")));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                EvidenceInsertSql(
                    Guid.NewGuid(),
                    decisionId,
                    "ticket:cross-tenant",
                    tenantId: 2)));
        }

        [Fact]
        public async Task DatabaseRejectsInvalidEnumsNegativeQuantitiesAndFinalWithoutEvidence()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 0, 5)));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                BaseInsertSql(Guid.NewGuid(), 4, 5)));

            var invalidQuantityId = Guid.NewGuid();
            await ExecuteAsync(database, BaseInsertSql(invalidQuantityId, 1, 5));
            await ExecuteAsync(
                database,
                EvidenceInsertSql(Guid.NewGuid(), invalidQuantityId, "ticket:negative"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                ConfirmedUpdateSql(invalidQuantityId, -1, 5, 5, 1)));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                ConfirmedUpdateSql(invalidQuantityId, 5, 5, 5, 0)));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                EvidenceInsertSql(
                    Guid.NewGuid(),
                    invalidQuantityId,
                    "ticket:bad-source",
                    source: 0)));

            var noEvidenceId = Guid.NewGuid();
            await ExecuteAsync(database, BaseInsertSql(noEvidenceId, 1, 6));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                ConfirmedUpdateSql(noEvidenceId, 5, 5, 5, 1)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DatabaseRejectsNullBlankOrWhitespaceRejectedReason(
            string rejectionReason)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = Guid.NewGuid();
            await ExecuteAsync(database, BaseInsertSql(decisionId, 1, 7));
            await ExecuteAsync(
                database,
                EvidenceInsertSql(Guid.NewGuid(), decisionId, "ticket:rejection-shape"));
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                UPDATE public."AQGreenWeeklySalesEligibilityDecisions"
                SET "ReviewStatus" = 3,
                    "ReviewedAt" = TIMESTAMPTZ '2026-08-27 22:00:00+00',
                    "ReviewedByUserId" = 3001,
                    "RejectionReason" = @rejectionReason
                WHERE "Id" = @decisionId;
                """,
                connection);
            command.Parameters.AddWithValue(
                "rejectionReason",
                rejectionReason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("decisionId", decisionId);

            var exception = await Should.ThrowAsync<PostgresException>(() =>
                command.ExecuteNonQueryAsync());

            exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            exception.ConstraintName.ShouldBe(
                "CK_AQGreenWeeklySalesDecisions_StateShape");
        }

        [Fact]
        public async Task TemporaryEvidenceTableCannotSatisfyDurableFinalEvidence()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = Guid.NewGuid();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    BaseInsertSql(decisionId, 1, 8).Replace(
                        "INSERT INTO \"AQGreenWeeklySalesEligibilityDecisions\"",
                        "INSERT INTO public.\"AQGreenWeeklySalesEligibilityDecisions\""));
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    CREATE TEMP TABLE "AQGreenWeeklySalesEvidenceReferences" (
                        "TenantId" integer NOT NULL,
                        "DecisionId" uuid NOT NULL)
                    ON COMMIT DROP;
                    """);
                await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    INSERT INTO pg_temp."AQGreenWeeklySalesEvidenceReferences" (
                        "TenantId", "DecisionId")
                    VALUES (1, '{decisionId}');
                    """);
                await ExecuteAsync(
                    connection,
                    transaction,
                    ConfirmedUpdateSql(decisionId, 5, 5, 5, 1).Replace(
                        "UPDATE \"AQGreenWeeklySalesEligibilityDecisions\"",
                        "UPDATE public.\"AQGreenWeeklySalesEligibilityDecisions\""));

                var exception = await Should.ThrowAsync<PostgresException>(() =>
                    ExecuteAsync(
                        connection,
                        transaction,
                        "SET CONSTRAINTS \"TR_AQGreenWeeklySalesDecisions_RequireEvidence\" IMMEDIATE;"));

                exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
                exception.MessageText.ShouldBe(
                    "A finalized AQGreen weekly-sales decision requires evidence.");
            }
            finally
            {
                await transaction.RollbackAsync();
            }
        }

        [Fact]
        public async Task TemporaryDecisionTableCannotAuthorizeLateDurableEvidence()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                5,
                5,
                AQGreenWeeklySalesThresholdResult.Met,
                null,
                participantNumber: 9);
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    CREATE TEMP TABLE "AQGreenWeeklySalesEligibilityDecisions" (
                        "TenantId" integer NOT NULL,
                        "Id" uuid NOT NULL,
                        "ReviewStatus" integer NOT NULL)
                    ON COMMIT DROP;
                    """);
                await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    INSERT INTO pg_temp."AQGreenWeeklySalesEligibilityDecisions" (
                        "TenantId", "Id", "ReviewStatus")
                    VALUES (1, '{decisionId}', 1);
                    """);

                var exception = await Should.ThrowAsync<PostgresException>(() =>
                    ExecuteAsync(
                        connection,
                        transaction,
                        EvidenceInsertSql(
                            Guid.NewGuid(),
                            decisionId,
                            "ticket:late-shadow").Replace(
                            "INSERT INTO \"AQGreenWeeklySalesEvidenceReferences\"",
                            "INSERT INTO public.\"AQGreenWeeklySalesEvidenceReferences\"")));

                exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
                exception.MessageText.ShouldBe(
                    "AQGreen weekly-sales evidence can be added only while the parent is HeldForEvidence.");
            }
            finally
            {
                await transaction.RollbackAsync();
            }
        }

        [Fact]
        public async Task ReplicaRoleCannotBypassAnyWeeklySalesIntegrityTrigger()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var finalDecisionId = await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                5,
                5,
                AQGreenWeeklySalesThresholdResult.Met,
                null,
                participantNumber: 10);
            var evidenceId = await ScalarAsync<Guid>(
                database,
                $"SELECT \"Id\" FROM public.\"AQGreenWeeklySalesEvidenceReferences\" WHERE \"DecisionId\" = '{finalDecisionId}'");

            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    DirectConfirmedInsertSql(Guid.NewGuid(), 11)));
            await ExpectReplicaRoleRejectedAsync(database, async (connection, transaction) =>
            {
                var heldId = Guid.NewGuid();
                await ExecuteAsync(connection, transaction, BaseInsertSql(heldId, 1, 12));
                await ExecuteAsync(
                    connection,
                    transaction,
                    ConfirmedUpdateSql(heldId, 5, 5, 5, 1));
                await ExecuteAsync(
                    connection,
                    transaction,
                    "SET CONSTRAINTS \"TR_AQGreenWeeklySalesDecisions_RequireEvidence\" IMMEDIATE;");
            });
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    $"UPDATE public.\"AQGreenWeeklySalesEligibilityDecisions\" SET \"ReviewedByUserId\" = 4001 WHERE \"Id\" = '{finalDecisionId}';"));
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    $"DELETE FROM public.\"AQGreenWeeklySalesEligibilityDecisions\" WHERE \"Id\" = '{finalDecisionId}';"));
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    $"UPDATE public.\"AQGreenWeeklySalesEvidenceReferences\" SET \"TechnicalReference\" = 'ticket:replica-change' WHERE \"Id\" = '{evidenceId}';"));
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    $"DELETE FROM public.\"AQGreenWeeklySalesEvidenceReferences\" WHERE \"Id\" = '{evidenceId}';"));
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    EvidenceInsertSql(
                            Guid.NewGuid(),
                            finalDecisionId,
                            "ticket:replica-late")
                        .Replace(
                            "INSERT INTO \"AQGreenWeeklySalesEvidenceReferences\"",
                            "INSERT INTO public.\"AQGreenWeeklySalesEvidenceReferences\"")));
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    "TRUNCATE public.\"AQGreenWeeklySalesEligibilityDecisions\" CASCADE;"));
            await ExpectReplicaRoleRejectedAsync(
                database,
                (connection, transaction) => ExecuteAsync(
                    connection,
                    transaction,
                    "TRUNCATE public.\"AQGreenWeeklySalesEvidenceReferences\";"));

            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM public.\"AQGreenWeeklySalesEligibilityDecisions\""))
                .ShouldBe(1);
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM public.\"AQGreenWeeklySalesEvidenceReferences\""))
                .ShouldBe(1);
        }

        [Fact]
        public async Task FinalParentAndEvidenceRejectEveryMutationAndTruncatePath()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = await InsertFinalAsync(
                database,
                AQGreenWeeklySalesReviewStatus.Confirmed,
                5,
                5,
                5,
                AQGreenWeeklySalesThresholdResult.Met,
                null,
                participantNumber: 7);
            var evidenceId = await ScalarAsync<Guid>(
                database,
                $"SELECT \"Id\" FROM \"AQGreenWeeklySalesEvidenceReferences\" WHERE \"DecisionId\" = '{decisionId}'");

            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                $"UPDATE \"AQGreenWeeklySalesEligibilityDecisions\" SET \"ReviewedByUserId\" = 4001 WHERE \"Id\" = '{decisionId}'"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                $"DELETE FROM \"AQGreenWeeklySalesEligibilityDecisions\" WHERE \"Id\" = '{decisionId}'"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                "TRUNCATE TABLE \"AQGreenWeeklySalesEligibilityDecisions\""));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                $"UPDATE \"AQGreenWeeklySalesEvidenceReferences\" SET \"TechnicalReference\" = 'ticket:changed' WHERE \"Id\" = '{evidenceId}'"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                $"DELETE FROM \"AQGreenWeeklySalesEvidenceReferences\" WHERE \"Id\" = '{evidenceId}'"));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                "TRUNCATE TABLE \"AQGreenWeeklySalesEvidenceReferences\""));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                database,
                EvidenceInsertSql(Guid.NewGuid(), decisionId, "ticket:late")));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task TransactionRollbackLeavesNoPartialDecisionOrEvidence(int stage)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var decisionId = Guid.NewGuid();
            await using (var connection = new NpgsqlConnection(database.ConnectionString()))
            {
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await ExecuteAsync(
                    connection,
                    transaction,
                    BaseInsertSql(decisionId, 1, 10 + stage));
                if (stage >= 2)
                    await ExecuteAsync(
                        connection,
                        transaction,
                        EvidenceInsertSql(Guid.NewGuid(), decisionId, "ticket:rollback"));
                if (stage >= 3)
                    await ExecuteAsync(
                        connection,
                        transaction,
                        ConfirmedUpdateSql(decisionId, 5, 5, 5, 1));
                await transaction.RollbackAsync();
            }

            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEligibilityDecisions\""))
                .ShouldBe(0);
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEvidenceReferences\""))
                .ShouldBe(0);
        }

        [Fact]
        public async Task UpAddsEmptyTablesWithoutChangingExistingV1CommissionAndEmptyDownSucceeds()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await MigrateAsync(database, PreviousMigration);
            await ExecuteAsync(
                database,
                """
                INSERT INTO "EntryCommissionPeriods" (
                    "Id", "TenantId", "PeriodStart", "PeriodEnd", "TimeZoneId",
                    "CalculatedAt", "RulesVersion", "CreationTime", "IsDeleted")
                VALUES (
                    '30000000-0000-0000-0000-000000000053', 1,
                    TIMESTAMPTZ '2026-08-03 00:00:00+00',
                    TIMESTAMPTZ '2026-08-10 00:00:00+00', 'UTC', NOW(),
                    'AQGreenWeeklyCommissionV1', NOW(), FALSE);
                """);

            await MigrateAsync(database, null);
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEligibilityDecisions\""))
                .ShouldBe(0);
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEvidenceReferences\""))
                .ShouldBe(0);
            (await ScalarAsync<string>(
                    database,
                    "SELECT \"RulesVersion\" FROM \"EntryCommissionPeriods\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000053'"))
                .ShouldBe("AQGreenWeeklyCommissionV1");

            await MigrateAsync(database, PreviousMigration);
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"EntryCommissionPeriods\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000053'"))
                .ShouldBe(1);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task DownRefusesHeldConfirmedRejectedAndHeldEvidenceWithoutDataLoss(
            int durableState)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            if (durableState == 1 || durableState == 4)
            {
                var heldId = Guid.NewGuid();
                await ExecuteAsync(database, BaseInsertSql(heldId, 1, 20 + durableState));
                if (durableState == 4)
                    await ExecuteAsync(
                        database,
                        EvidenceInsertSql(Guid.NewGuid(), heldId, "ticket:held"));
            }
            else
            {
                await InsertFinalAsync(
                    database,
                    durableState == 2
                        ? AQGreenWeeklySalesReviewStatus.Confirmed
                        : AQGreenWeeklySalesReviewStatus.Rejected,
                    durableState == 2 ? 5 : null,
                    durableState == 2 ? 5 : null,
                    durableState == 2 ? 5 : null,
                    durableState == 2
                        ? AQGreenWeeklySalesThresholdResult.Met
                        : null,
                    durableState == 3 ? "rejected evidence" : null,
                    participantNumber: 20 + durableState);
            }

            await Should.ThrowAsync<PostgresException>(() =>
                MigrateAsync(database, PreviousMigration));
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEligibilityDecisions\""))
                .ShouldBe(1);
            (await ScalarAsync<int>(
                    database,
                    "SELECT COUNT(*) FROM \"AQGreenWeeklySalesEvidenceReferences\""))
                .ShouldBe(durableState == 1 ? 0 : 1);
        }

        private static async Task<Guid> InsertFinalAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            AQGreenWeeklySalesReviewStatus status,
            int? spray,
            int? oneLitre,
            int? fiveLitre,
            AQGreenWeeklySalesThresholdResult? result,
            string rejectionReason,
            int participantNumber = 1)
        {
            var decisionId = Guid.NewGuid();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await ExecuteAsync(
                connection,
                transaction,
                BaseInsertSql(decisionId, 1, participantNumber));
            await ExecuteAsync(
                connection,
                transaction,
                EvidenceInsertSql(Guid.NewGuid(), decisionId, "ticket:review"));
            var finalSql = status == AQGreenWeeklySalesReviewStatus.Confirmed
                ? ConfirmedUpdateSql(
                    decisionId,
                    spray.Value,
                    oneLitre.Value,
                    fiveLitre.Value,
                    (int)result.Value)
                : $"""
                   UPDATE "AQGreenWeeklySalesEligibilityDecisions"
                   SET "ReviewStatus" = 3,
                       "ReviewedAt" = TIMESTAMPTZ '2026-08-27 22:00:00+00',
                       "ReviewedByUserId" = 3001,
                       "RejectionReason" = '{rejectionReason}'
                   WHERE "Id" = '{decisionId}';
                   """;
            await ExecuteAsync(connection, transaction, finalSql);
            await transaction.CommitAsync();
            return decisionId;
        }

        private static string ConfirmedUpdateSql(
            Guid decisionId,
            int spray,
            int oneLitre,
            int fiveLitre,
            int thresholdResult) =>
            $"""
            UPDATE "AQGreenWeeklySalesEligibilityDecisions"
            SET "ReviewStatus" = 2,
                "ReviewedSprayQuantity" = {spray},
                "ReviewedOneLitreQuantity" = {oneLitre},
                "ReviewedFiveLitreQuantity" = {fiveLitre},
                "ThresholdResult" = {thresholdResult},
                "ReviewedAt" = TIMESTAMPTZ '2026-08-27 22:00:00+00',
                "ReviewedByUserId" = 3001
            WHERE "Id" = '{decisionId}';
            """;

        private static string DirectConfirmedInsertSql(
            Guid decisionId,
            int participantNumber) =>
            $"""
            INSERT INTO public."AQGreenWeeklySalesEligibilityDecisions" (
                "Id", "TenantId", "ParticipantId", "CommissionWeekStartUtc",
                "SalesEligibilityRulesVersion", "ReviewStatus",
                "ReviewedSprayQuantity", "ReviewedOneLitreQuantity",
                "ReviewedFiveLitreQuantity", "ThresholdResult", "ReviewedAt",
                "ReviewedByUserId", "RejectionReason", "CreationTime")
            VALUES ('{decisionId}', 1, '{P(participantNumber)}',
                TIMESTAMPTZ '2026-08-20 22:00:00+00',
                'AQGreenWeeklySalesEligibilityV1', 2, 5, 5, 5, 1,
                TIMESTAMPTZ '2026-08-27 22:00:00+00', 3001, NULL, NOW());
            """;

        private static string BaseInsertSql(
            Guid decisionId,
            int status,
            int participantNumber = 1) =>
            $"""
            INSERT INTO "AQGreenWeeklySalesEligibilityDecisions" (
                "Id", "TenantId", "ParticipantId", "CommissionWeekStartUtc",
                "SalesEligibilityRulesVersion", "ReviewStatus", "CreationTime")
            VALUES ('{decisionId}', 1, '{P(participantNumber)}',
                TIMESTAMPTZ '2026-08-20 22:00:00+00',
                'AQGreenWeeklySalesEligibilityV1', {status}, NOW())
            """;

        private static string EvidenceInsertSql(
            Guid evidenceId,
            Guid decisionId,
            string technicalReference,
            int tenantId = 1,
            int source = 1) =>
            $"""
            INSERT INTO "AQGreenWeeklySalesEvidenceReferences" (
                "Id", "TenantId", "DecisionId", "Source",
                "TechnicalReference", "RecordedAt")
            VALUES ('{evidenceId}', {tenantId}, '{decisionId}', {source},
                '{technicalReference}', TIMESTAMPTZ '2026-08-27 22:00:00+00')
            """;

        private static async Task ExecuteAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            string sql)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task ExpectReplicaRoleRejectedAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Func<NpgsqlConnection, NpgsqlTransaction, Task> action)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            PostgresException exception = null;
            try
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "SET LOCAL session_replication_role = replica;");
                exception = await Should.ThrowAsync<PostgresException>(() =>
                    action(connection, transaction));
            }
            finally
            {
                await transaction.RollbackAsync();
            }

            exception.ShouldNotBeNull();
            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            await using var roleCommand = new NpgsqlCommand(
                "SHOW session_replication_role;",
                connection);
            (await roleCommand.ExecuteScalarAsync()).ShouldBe("origin");
        }

        private static async Task ExecuteAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<T> ScalarAsync<T>(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            string sql)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var value = await command.ExecuteScalarAsync();
            return value is T typed
                ? typed
                : (T)Convert.ChangeType(value, typeof(T));
        }

        private async Task MigrateAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            string targetMigration)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
        }

        private async Task<AQGreenWeeklySalesEligibilitySnapshot> ReadAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Guid participantId)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            return await new AQGreenWeeklySalesEligibilityDecisionReader(provider)
                .GetFinalDecisionAsync(
                    participantId == AQGreenPlacementAllocatorPostgreSqlFixture.Participant(2, 1)
                        ? 2
                        : 1,
                    participantId,
                    WeekStartUtc,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
        }

        private static Guid P(int number) =>
            AQGreenPlacementAllocatorPostgreSqlFixture.Participant(1, number);
    }
}
