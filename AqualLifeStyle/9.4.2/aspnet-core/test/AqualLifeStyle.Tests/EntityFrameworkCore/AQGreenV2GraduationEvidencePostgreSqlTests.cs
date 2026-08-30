using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenV2GraduationEvidencePostgreSqlTests
    {
        private const string PreviousMigration =
            "20260826011850_AddAQGreenRecruitmentAttributionFoundation";
        private static readonly DateTime PlacedAt =
            new(2026, 8, 27, 8, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Cutoff =
            new(2026, 8, 28, 8, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ScopeId =
            Guid.Parse("b5000000-0000-0000-0000-000000000001");
        private static readonly Guid DecisionId =
            Guid.Parse("b5000000-0000-0000-0000-000000000002");
        private static readonly Guid LoanId =
            Guid.Parse("b5000000-0000-0000-0000-000000000003");
        private static readonly Guid OnyxParticipationId =
            Guid.Parse("b5000000-0000-0000-0000-000000000004");
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;

        public AQGreenV2GraduationEvidencePostgreSqlTests(
            AQGreenPlacementAllocatorPostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ValidGraph_PersistsReplaysAndIsAppendOnly_PostgreSQL()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using (var connection = new NpgsqlConnection(database.ConnectionString()))
            {
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await SeedTopologyAsync(connection, transaction, includeLaterPlacement: false);
                await SeedGraduationDependenciesAsync(connection, transaction);
                await InsertDecisionAndEvidenceAsync(
                    connection,
                    transaction,
                    MalformedGraph.None);
                await transaction.CommitAsync();
            }

            await using (var connection = new NpgsqlConnection(database.ConnectionString()))
            {
                await connection.OpenAsync();
                await using var context = _fixture.CreateDbContext(connection);
                var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
                provider.GetDbContext().Returns(context);
                var replay = await new AQGreenV2GraduationEvidenceReplayValidator(provider)
                    .ValidateAsync(DecisionId);

                replay.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level2);
                replay.QualifyingDepth1Count.ShouldBe(5);
                replay.QualifyingDepth2Count.ShouldBe(25);
                replay.EvidenceNodeCount.ShouldBe(31);
            }

            await AssertDirectMutationRejectedAsync(
                database.ConnectionString(),
                """
                UPDATE public."AQGreenV2GraduationEvidence"
                SET "QualifyingDepth2Count" = 24
                WHERE "OnyxGraduationDecisionId" = 'b5000000-0000-0000-0000-000000000002';
                """);
            await AssertDirectMutationRejectedAsync(
                database.ConnectionString(),
                """
                DELETE FROM public."AQGreenV2GraduationEvidenceNodes"
                WHERE "EvidenceId" = 'b5000000-0000-0000-0000-000000000002'
                  AND "CanonicalOrdinal" = 30;
                """);
            await AssertDirectMutationRejectedAsync(
                database.ConnectionString(),
                "TRUNCATE TABLE public.\"AQGreenV2GraduationEvidenceNodes\";");
        }

        [Theory]
        [InlineData(MalformedGraph.DuplicateSourcePlacement)]
        [InlineData(MalformedGraph.DuplicateOrdinal)]
        [InlineData(MalformedGraph.CrossTenantNode)]
        [InlineData(MalformedGraph.IncompleteManifest)]
        [InlineData(MalformedGraph.PlacementAfterCutoff)]
        [InlineData(MalformedGraph.LegacyDecisionWithHeader)]
        [InlineData(MalformedGraph.PlacementV2DecisionWithoutHeader)]
        public async Task DatabaseRejectsMalformedDecisionEvidenceGraphs_PostgreSQL(
            MalformedGraph malformedGraph)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await SeedTopologyAsync(
                connection,
                transaction,
                includeLaterPlacement:
                    malformedGraph == MalformedGraph.PlacementAfterCutoff);
            await SeedGraduationDependenciesAsync(connection, transaction);

            await Should.ThrowAsync<PostgresException>(async () =>
            {
                await InsertDecisionAndEvidenceAsync(
                    connection,
                    transaction,
                    malformedGraph);
                await ExecuteAsync(
                    connection,
                    transaction,
                    "SET CONSTRAINTS ALL IMMEDIATE;");
            });
        }

        [Fact]
        public async Task MigrationBackfillsHistoricalDecisionAsLegacyWithoutFabricatedVersions_PostgreSQL()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SeedGraduationDependenciesAsync(connection, transaction);
                await InsertLegacyDecisionAsync(connection, transaction);
                await transaction.CommitAsync();
            }

            await context.GetService<IMigrator>().MigrateAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT "StructuralModel", "GraduationRulesVersion",
                       "EvaluatedLoanTermsVersion", "EvaluatedNetworkLevel"
                FROM public."OnyxGraduationDecisions"
                WHERE "Id" = @decisionId;
                """,
                connection);
            command.Parameters.AddWithValue("decisionId", DecisionId);
            await using var reader = await command.ExecuteReaderAsync();
            (await reader.ReadAsync()).ShouldBeTrue();
            reader.GetInt32(0).ShouldBe((int)AQGreenGraduationStructuralModel.LegacyV1);
            reader.IsDBNull(1).ShouldBeTrue();
            reader.IsDBNull(2).ShouldBeTrue();
            reader.GetInt32(3).ShouldBe(2);
        }

        [Theory]
        [InlineData(UnversionedLegacyWrite.NewInsert)]
        [InlineData(UnversionedLegacyWrite.CurrentRowUpdate)]
        public async Task PostgreSQLRejectsFutureUnversionedLegacyWrites(
            UnversionedLegacyWrite write)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            if (write == UnversionedLegacyWrite.NewInsert)
            {
                await using var transaction = await connection.BeginTransactionAsync();
                await SeedGraduationDependenciesAsync(connection, transaction);

                var exception = await Should.ThrowAsync<PostgresException>(() =>
                    InsertCurrentDecisionAsync(
                        connection,
                        transaction,
                        AQGreenGraduationStructuralModel.LegacyV1,
                        graduationRulesVersion: null,
                        evaluatedLoanTermsVersion: null));

                exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
                exception.ConstraintName.ShouldBe(
                    "CK_OnyxGraduationDecisions_VersionSnapshots_Required");
                return;
            }

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SeedGraduationDependenciesAsync(connection, transaction);
                await InsertCurrentDecisionAsync(
                    connection,
                    transaction,
                    AQGreenGraduationStructuralModel.LegacyV1);
                await transaction.CommitAsync();
            }

            var updateException = await Should.ThrowAsync<PostgresException>(async () =>
            {
                await using var command = new NpgsqlCommand(
                    """
                    UPDATE public."OnyxGraduationDecisions"
                    SET "GraduationRulesVersion" = NULL,
                        "EvaluatedLoanTermsVersion" = NULL
                    WHERE "Id" = @decisionId;
                    """,
                    connection);
                command.Parameters.AddWithValue("decisionId", DecisionId);
                await command.ExecuteNonQueryAsync();
            });
            updateException.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            updateException.ConstraintName.ShouldBe(
                "CK_OnyxGraduationDecisions_VersionSnapshots_Required");
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public async Task DownMigrationRestoresRequiredNetworkLevelWithoutDatabaseDefault_PostgreSQL(
            int evaluatedNetworkLevel)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SeedGraduationDependenciesAsync(connection, transaction);
                await InsertLegacyDecisionAsync(
                    connection,
                    transaction,
                    evaluatedNetworkLevel);
                await transaction.CommitAsync();
            }

            await context.GetService<IMigrator>().MigrateAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            await using var command = new NpgsqlCommand(
                """
                SELECT column_metadata.is_nullable,
                       column_metadata.column_default,
                       decision."EvaluatedNetworkLevel"
                FROM information_schema.columns AS column_metadata
                CROSS JOIN public."OnyxGraduationDecisions" AS decision
                WHERE column_metadata.table_schema = 'public'
                  AND column_metadata.table_name = 'OnyxGraduationDecisions'
                  AND column_metadata.column_name = 'EvaluatedNetworkLevel'
                  AND decision."Id" = @decisionId;
                """,
                connection);
            command.Parameters.AddWithValue("decisionId", DecisionId);
            await using var reader = await command.ExecuteReaderAsync();
            (await reader.ReadAsync()).ShouldBeTrue();
            reader.GetString(0).ShouldBe("NO");
            reader.IsDBNull(1).ShouldBeTrue();
            reader.GetInt32(2).ShouldBe(evaluatedNetworkLevel);
        }

        [Fact]
        public async Task DownMigrationRefusesAfterPlacementV2EvidenceExists_PostgreSQL()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SeedTopologyAsync(connection, transaction, includeLaterPlacement: false);
                await SeedGraduationDependenciesAsync(connection, transaction);
                await InsertDecisionAndEvidenceAsync(
                    connection,
                    transaction,
                    MalformedGraph.None);
                await transaction.CommitAsync();
            }

            await using var context = _fixture.CreateDbContext(connection);
            await Should.ThrowAsync<PostgresException>(() =>
                context.GetService<IMigrator>().MigrateAsync(PreviousMigration));

            await using var verify = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM public."AQGreenV2GraduationEvidence"
                WHERE "OnyxGraduationDecisionId" = @decisionId;
                """,
                connection);
            verify.Parameters.AddWithValue("decisionId", DecisionId);
            Convert.ToInt32(await verify.ExecuteScalarAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task ConcurrentSameLoanDecisionInsert_HasOneDurableWinner_PostgreSQL()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await using (var seedConnection = new NpgsqlConnection(database.ConnectionString()))
            {
                await seedConnection.OpenAsync();
                await using var seedTransaction = await seedConnection.BeginTransactionAsync();
                await SeedGraduationDependenciesAsync(seedConnection, seedTransaction);
                await seedTransaction.CommitAsync();
            }

            await using var firstConnection =
                new NpgsqlConnection(database.ConnectionString("b52-first-graduation"));
            await using var secondConnection =
                new NpgsqlConnection(database.ConnectionString("b52-second-graduation"));
            await firstConnection.OpenAsync();
            await secondConnection.OpenAsync();
            await using var firstTransaction = await firstConnection.BeginTransactionAsync();
            await using var secondTransaction = await secondConnection.BeginTransactionAsync();
            await InsertCurrentDecisionAsync(
                firstConnection,
                firstTransaction,
                AQGreenGraduationStructuralModel.LegacyV1,
                DecisionId);

            var losingDecisionId =
                Guid.Parse("b5000000-0000-0000-0000-000000000099");
            var losingInsert = InsertCurrentDecisionAsync(
                secondConnection,
                secondTransaction,
                AQGreenGraduationStructuralModel.LegacyV1,
                losingDecisionId);
            await Task.Delay(100);
            losingInsert.IsCompleted.ShouldBeFalse();
            await firstTransaction.CommitAsync();
            var collision = await Should.ThrowAsync<PostgresException>(() => losingInsert);
            collision.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
            collision.ConstraintName.ShouldBe(
                "IX_OnyxGraduationDecisions_EntryParticipationId");
            await secondTransaction.RollbackAsync();

            await using var verifyConnection = new NpgsqlConnection(database.ConnectionString());
            await verifyConnection.OpenAsync();
            await using var verify = new NpgsqlCommand(
                "SELECT COUNT(*) FROM public.\"OnyxGraduationDecisions\";",
                verifyConnection);
            Convert.ToInt32(await verify.ExecuteScalarAsync()).ShouldBe(1);
        }

        private static async Task SeedTopologyAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            bool includeLaterPlacement)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."AQGreenPlacementTreeScopes" ("Id", "TenantId")
                VALUES (@scopeId, 1);
                """,
                new NpgsqlParameter("scopeId", ScopeId));

            for (var number = 1; number <= 31; number++)
            {
                Guid? parentParticipantId;
                int? placementSlot;
                string canonicalPath;
                if (number == 1)
                {
                    parentParticipantId = null;
                    placementSlot = null;
                    canonicalPath = string.Empty;
                }
                else if (number <= 6)
                {
                    parentParticipantId = Participant(1);
                    placementSlot = number - 1;
                    canonicalPath = placementSlot.Value.ToString();
                }
                else
                {
                    var depthTwoIndex = number - 7;
                    var parentSlot = depthTwoIndex / 5 + 1;
                    placementSlot = depthTwoIndex % 5 + 1;
                    parentParticipantId = Participant(parentSlot + 1);
                    canonicalPath = $"{parentSlot}{placementSlot}";
                }

                await InsertPlacementAsync(
                    connection,
                    transaction,
                    number,
                    parentParticipantId,
                    placementSlot,
                    canonicalPath,
                    PlacedAt);
            }

            if (includeLaterPlacement)
            {
                await InsertPlacementAsync(
                    connection,
                    transaction,
                    32,
                    Participant(7),
                    1,
                    "111",
                    Cutoff.AddMinutes(1));
            }
        }

        private static Task InsertPlacementAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int number,
            Guid? parentParticipantId,
            int? placementSlot,
            string canonicalPath,
            DateTime placedAt) =>
            ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."AQGreenNetworkPlacements" (
                    "Id", "TenantId", "PlacementTreeScopeId", "ParticipantId",
                    "PlacementParentParticipantId", "PlacementSlot", "CanonicalPath",
                    "PlacedAt", "RulesVersion")
                VALUES (
                    @id, 1, @scopeId, @participantId,
                    @parentParticipantId, @placementSlot, @canonicalPath,
                    @placedAt, @rulesVersion);
                """,
                new NpgsqlParameter("id", Placement(number)),
                new NpgsqlParameter("scopeId", ScopeId),
                new NpgsqlParameter("participantId", Participant(number)),
                new NpgsqlParameter(
                    "parentParticipantId",
                    parentParticipantId.HasValue
                        ? parentParticipantId.Value
                        : DBNull.Value),
                new NpgsqlParameter(
                    "placementSlot",
                    placementSlot.HasValue ? placementSlot.Value : DBNull.Value),
                new NpgsqlParameter("canonicalPath", canonicalPath),
                new NpgsqlParameter("placedAt", placedAt),
                new NpgsqlParameter("rulesVersion", AQGreenPlacementRules.CurrentVersion));

        private static async Task SeedGraduationDependenciesAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."Memberships" (
                    "Id", "TenantId", "Name", "Description", "MembershipType",
                    "IsActive", "MonthlyObligationAmount", "ActivationDate",
                    "LastObligationMetDate")
                VALUES (910001, 1, 'B5.2 Onyx', NULL, 1, TRUE, 0, NULL, NULL);

                INSERT INTO public."OnyxLoanAgreements" (
                    "Id", "TenantId", "EntryParticipationId", "CustomerId", "Status",
                    "TermsVersion", "PrincipalAmount", "InterestRatePercent",
                    "TotalPayableAmount", "OutstandingAmount", "Currency",
                    "RepaymentPeriodMonths", "InitialWeeklyRequirementCount",
                    "InitialWeeklyMinimumAmount", "OfferedAt", "MemberAcceptedByUserId",
                    "MemberConfirmation", "MemberAcceptedAt",
                    "ApprovedByAdministratorUserId", "ApprovedAt", "EffectiveAt",
                    "RepaymentDeadlineAt", "LastAssessedAt", "SettledAt",
                    "CreationTime", "CreatorUserId", "LastModificationTime",
                    "LastModifierUserId", "IsDeleted", "DeleterUserId", "DeletionTime")
                VALUES (
                    @loanId, 1, @entryParticipationId, 1, 2,
                    'accepted-loan-v1', 6120, 30, 7956, 7956, 'ZAR',
                    3, 4, 200, @offeredAt, 3001,
                    'Accepted', @acceptedAt, 3001, @approvedAt, @effectiveAt,
                    @repaymentDeadlineAt, NULL, NULL,
                    @creationTime, NULL, NULL, NULL, FALSE, NULL, NULL);

                INSERT INTO public."OnyxParticipations" (
                    "Id", "TenantId", "CustomerId", "RecruiterCustomerId",
                    "OnyxMembershipId", "AdmissionRoute", "Status", "StartedAt",
                    "ActivatedAt", "DirectEntryPaymentId", "EntryParticipationId",
                    "LoanAgreementId", "TermsVersion", "TermsEffectiveFrom",
                    "DirectEntryAmount", "Currency", "CreationTime", "CreatorUserId",
                    "LastModificationTime", "LastModifierUserId", "IsDeleted",
                    "DeleterUserId", "DeletionTime")
                VALUES (
                    @onyxParticipationId, 1, 1, NULL, 910001, 1, 1,
                    @effectiveAt, @effectiveAt, NULL, @entryParticipationId,
                    @loanId, 'accepted-loan-v1', @effectiveAt, 6120, 'ZAR',
                    @creationTime, NULL, NULL, NULL, FALSE, NULL, NULL);
                """,
                new NpgsqlParameter("loanId", LoanId),
                new NpgsqlParameter("entryParticipationId", Participant(1)),
                new NpgsqlParameter("onyxParticipationId", OnyxParticipationId),
                new NpgsqlParameter("offeredAt", Cutoff.AddDays(-5)),
                new NpgsqlParameter("acceptedAt", Cutoff.AddDays(-4)),
                new NpgsqlParameter("approvedAt", Cutoff.AddDays(-3)),
                new NpgsqlParameter("effectiveAt", Cutoff.AddDays(-3)),
                new NpgsqlParameter("repaymentDeadlineAt", Cutoff.AddMonths(3)),
                new NpgsqlParameter("creationTime", Cutoff.AddDays(-5)));
        }

        private static async Task InsertDecisionAndEvidenceAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            MalformedGraph malformedGraph)
        {
            var structuralModel = malformedGraph == MalformedGraph.LegacyDecisionWithHeader
                ? AQGreenGraduationStructuralModel.LegacyV1
                : AQGreenGraduationStructuralModel.PlacementV2;
            await InsertCurrentDecisionAsync(
                connection,
                transaction,
                structuralModel);

            if (malformedGraph == MalformedGraph.PlacementV2DecisionWithoutHeader)
                return;

            var nodeCount = malformedGraph == MalformedGraph.IncompleteManifest ? 30 : 31;
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."AQGreenV2GraduationEvidence" (
                    "OnyxGraduationDecisionId", "TenantId", "Cutoff",
                    "StructuralQualificationRulesVersion", "EvidenceSchemaVersion",
                    "EvaluatedStructuralCompletionLevel", "QualifyingDepth1Count",
                    "QualifyingDepth2Count", "EvidenceNodeCount")
                VALUES (
                    @decisionId, 1, @cutoff, @structuralVersion, @schemaVersion,
                    2, 5, 25, 31);
                """,
                new NpgsqlParameter("decisionId", DecisionId),
                new NpgsqlParameter("cutoff", Cutoff),
                new NpgsqlParameter(
                    "structuralVersion",
                    AQGreenStructuralQualificationRules.CurrentVersion),
                new NpgsqlParameter(
                    "schemaVersion",
                    AQGreenV2GraduationEvidenceSchema.CurrentVersion));

            for (var ordinal = 0; ordinal < nodeCount; ordinal++)
            {
                var sourceNumber = ordinal + 1;
                if (malformedGraph == MalformedGraph.DuplicateSourcePlacement && ordinal == 30)
                    sourceNumber = 30;
                if (malformedGraph == MalformedGraph.PlacementAfterCutoff && ordinal == 30)
                    sourceNumber = 32;
                var persistedOrdinal =
                    malformedGraph == MalformedGraph.DuplicateOrdinal && ordinal == 30
                        ? 29
                        : ordinal;
                var tenantId =
                    malformedGraph == MalformedGraph.CrossTenantNode && ordinal == 30
                        ? 2
                        : 1;
                await InsertEvidenceNodeAsync(
                    connection,
                    transaction,
                    tenantId,
                    persistedOrdinal,
                    sourceNumber);
            }
        }

        private static Task InsertCurrentDecisionAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            AQGreenGraduationStructuralModel structuralModel,
            Guid? requestedDecisionId = null,
            string graduationRulesVersion = OnyxGraduationRules.CurrentVersion,
            string evaluatedLoanTermsVersion = "accepted-loan-v1") =>
            ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."OnyxGraduationDecisions" (
                    "Id", "TenantId", "CustomerId", "EntryParticipationId",
                    "LoanAgreementId", "OnyxParticipationId", "AdministratorUserId",
                    "DecidedAt", "Justification", "StructuralModel",
                    "GraduationRulesVersion", "EvaluatedNetworkLevel",
                    "AQGreenWasActive", "LoanWasActive", "LoanWasAccepted",
                    "LoanWasAdministratorApproved", "EvaluatedFundingAmount",
                    "EvaluatedFundingCurrency", "EvaluatedLoanTermsVersion",
                    "CreationTime", "CreatorUserId")
                VALUES (
                    @decisionId, 1, 1, @entryParticipationId,
                    @loanId, @onyxParticipationId, 3001,
                    @decidedAt, 'B5.2 PostgreSQL evidence test', @structuralModel,
                    @graduationRulesVersion, @evaluatedNetworkLevel,
                    TRUE, TRUE, TRUE, TRUE, 6120, 'ZAR', @evaluatedLoanTermsVersion,
                    @decidedAt, 3001);
                """,
                new NpgsqlParameter("decisionId", requestedDecisionId ?? DecisionId),
                new NpgsqlParameter("entryParticipationId", Participant(1)),
                new NpgsqlParameter("loanId", LoanId),
                new NpgsqlParameter("onyxParticipationId", OnyxParticipationId),
                new NpgsqlParameter("decidedAt", Cutoff),
                new NpgsqlParameter(
                    "structuralModel",
                    (int)structuralModel),
                new NpgsqlParameter(
                    "graduationRulesVersion",
                    (object)graduationRulesVersion ?? DBNull.Value),
                new NpgsqlParameter(
                    "evaluatedLoanTermsVersion",
                    (object)evaluatedLoanTermsVersion ?? DBNull.Value),
                new NpgsqlParameter(
                    "evaluatedNetworkLevel",
                    structuralModel == AQGreenGraduationStructuralModel.LegacyV1
                        ? 2
                        : DBNull.Value));

        private static Task InsertLegacyDecisionAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int evaluatedNetworkLevel = 2) =>
            ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."OnyxGraduationDecisions" (
                    "Id", "TenantId", "CustomerId", "EntryParticipationId",
                    "LoanAgreementId", "OnyxParticipationId", "AdministratorUserId",
                    "DecidedAt", "Justification", "EvaluatedNetworkLevel",
                    "AQGreenWasActive", "LoanWasActive", "LoanWasAccepted",
                    "LoanWasAdministratorApproved", "EvaluatedFundingAmount",
                    "EvaluatedFundingCurrency", "CreationTime", "CreatorUserId")
                VALUES (
                    @decisionId, 1, 1, @entryParticipationId,
                    @loanId, @onyxParticipationId, 3001,
                    @decidedAt, 'Historical V1 graduation', @evaluatedNetworkLevel,
                    TRUE, TRUE, TRUE, TRUE, 6120, 'ZAR', @decidedAt, 3001);
                """,
                new NpgsqlParameter("decisionId", DecisionId),
                new NpgsqlParameter("entryParticipationId", Participant(1)),
                new NpgsqlParameter("loanId", LoanId),
                new NpgsqlParameter("onyxParticipationId", OnyxParticipationId),
                new NpgsqlParameter("decidedAt", Cutoff),
                new NpgsqlParameter("evaluatedNetworkLevel", evaluatedNetworkLevel));

        private static Task InsertEvidenceNodeAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int tenantId,
            int ordinal,
            int sourceNumber) =>
            ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."AQGreenV2GraduationEvidenceNodes" (
                    "EvidenceId", "CanonicalOrdinal", "TenantId", "SourcePlacementId",
                    "ParticipationStatusObserved", "ParticipationActivatedAtObserved",
                    "ParticipationIsDeletedObserved", "CustomerIdObserved",
                    "CustomerTenantMatchedObserved", "CustomerIsActiveObserved",
                    "CustomerIsDeletedObserved", "UserIdObserved",
                    "UserTenantMatchedObserved", "UserIsActiveObserved",
                    "UserIsDeletedObserved")
                VALUES (
                    @evidenceId, @ordinal, @tenantId, @sourcePlacementId,
                    2, @activatedAt, FALSE, @customerId,
                    TRUE, TRUE, FALSE, @userId, TRUE, TRUE, FALSE);
                """,
                new NpgsqlParameter("evidenceId", DecisionId),
                new NpgsqlParameter("ordinal", ordinal),
                new NpgsqlParameter("tenantId", tenantId),
                new NpgsqlParameter("sourcePlacementId", Placement(sourceNumber)),
                new NpgsqlParameter("activatedAt", PlacedAt.AddDays(-1)),
                new NpgsqlParameter("customerId", sourceNumber),
                new NpgsqlParameter("userId", (long)(3000 + sourceNumber)));

        private static async Task AssertDirectMutationRejectedAsync(
            string connectionString,
            string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await Should.ThrowAsync<PostgresException>(async () =>
            {
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
            });
        }

        private static async Task ExecuteAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql,
            params NpgsqlParameter[] parameters)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            if (parameters.Length > 0)
                command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        private static Guid Participant(int number) =>
            AQGreenPlacementAllocatorPostgreSqlFixture.Participant(1, number);

        private static Guid Placement(int number) =>
            Guid.Parse($"b5000001-0000-0000-0000-{number:D12}");

        public enum MalformedGraph
        {
            None,
            DuplicateSourcePlacement,
            DuplicateOrdinal,
            CrossTenantNode,
            IncompleteManifest,
            PlacementAfterCutoff,
            LegacyDecisionWithHeader,
            PlacementV2DecisionWithoutHeader
        }

        public enum UnversionedLegacyWrite
        {
            NewInsert,
            CurrentRowUpdate
        }
    }
}
