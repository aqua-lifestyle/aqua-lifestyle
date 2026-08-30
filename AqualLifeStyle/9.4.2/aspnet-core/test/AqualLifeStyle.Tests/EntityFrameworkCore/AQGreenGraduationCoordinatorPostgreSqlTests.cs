using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using Abp.TestBase;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenGraduationCoordinatorPostgreSqlTests
        : AbpIntegratedTestBase<AQGreenGraduationPostgreSqlApplicationTestModule>
    {
        private static readonly DateTime PlacedAt =
            new(2026, 8, 27, 8, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ScopeId =
            Guid.Parse("b5200000-0000-0000-0000-000000000001");
        private static readonly Guid LoanId =
            Guid.Parse("b5200000-0000-0000-0000-000000000002");
        private static readonly Guid HistoricalDecisionId =
            Guid.Parse("b5200000-0000-0000-0000-000000000003");
        private static readonly Guid HistoricalOnyxParticipationId =
            Guid.Parse("b5200000-0000-0000-0000-000000000004");
        private const string PreviousMigration =
            "20260826011850_AddAQGreenRecruitmentAttributionFoundation";
        private const int MembershipId = 920001;
        private readonly IAdminProgrammeParticipationAppService _service;

        public AQGreenGraduationCoordinatorPostgreSqlTests(
            AQGreenPlacementAllocatorPostgreSqlFixture fixture)
        {
            fixture.ShouldNotBeNull();
            AbpSession.TenantId = 1;
            AbpSession.UserId = 3001;
            AQGreenGraduationPostgreSqlFailureState.Shared.Reset();
            AQGreenGraduationPostgreSqlSelector.Shared.Model =
                AQGreenGraduationStructuralModel.PlacementV2;
            AQGreenGraduationPostgreSqlEvaluator.Shared.Reset();
            _service = Resolve<IAdminProgrammeParticipationAppService>();
        }

        [Fact]
        public async Task SerializationFailure_RetriesEntireApplicationTransaction_PostgreSQL()
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);
            AQGreenGraduationPostgreSqlFailureState.Shared.Mode =
                AQGreenGraduationPostgreSqlFailureMode.SerializationFailure;

            var result = await GraduateAsync(LoanId);

            var failure = AQGreenGraduationPostgreSqlFailureState.Shared;
            failure.ActualSqlState.ShouldBe(PostgresErrorCodes.SerializationFailure);
            failure.LockContextIds.Count.ShouldBeGreaterThanOrEqualTo(2);
            failure.SaveContextIds.Count.ShouldBeGreaterThanOrEqualTo(2);
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(2);
            await AssertCoherentGraphAsync(result.DecisionId, LoanId);
        }

        [Fact]
        public async Task KnownGraduationCollision_UsesFreshFullReconciliation_PostgreSQL()
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);
            AQGreenGraduationPostgreSqlFailureState.Shared.Mode =
                AQGreenGraduationPostgreSqlFailureMode.KnownGraduationCollision;

            var result = await GraduateAsync(LoanId);

            var failure = AQGreenGraduationPostgreSqlFailureState.Shared;
            failure.ActualSqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
            failure.ActualConstraintName.ShouldBe(
                "IX_OnyxParticipations_TenantId_CustomerId");
            failure.CommandContextIds.Count.ShouldBeGreaterThanOrEqualTo(2);
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(1);
            await AssertCoherentGraphAsync(result.DecisionId, LoanId);
        }

        [Fact]
        public async Task UnknownUniqueViolation_IsNotRetriedOrReconciled_PostgreSQL()
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);
            AQGreenGraduationPostgreSqlFailureState.Shared.Mode =
                AQGreenGraduationPostgreSqlFailureMode.UnknownUniqueViolation;

            var exception = await Should.ThrowAsync<Exception>(() => GraduateAsync(LoanId));
            var postgres = Find<PostgresException>(exception);
            postgres.ShouldNotBeNull();
            postgres.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
            postgres.ConstraintName.ShouldBe("PK_AQGreenPlacementTreeScopes");
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(1);
            await AssertNoGraduationGraphAsync();
        }

        [Fact]
        public async Task LostCommitAcknowledgement_RecoversDurableGraphWithoutDuplicate_PostgreSQL()
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);
            AQGreenGraduationPostgreSqlFailureState.Shared.Mode =
                AQGreenGraduationPostgreSqlFailureMode.CommitOutcomeUnknown;

            var result = await GraduateAsync(LoanId);

            var failure = AQGreenGraduationPostgreSqlFailureState.Shared;
            failure.CommitWasDurableBeforeInjectedFailure.ShouldBeTrue();
            failure.CommandContextIds.Count.ShouldBeGreaterThanOrEqualTo(2);
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(1);
            await AssertCoherentGraphAsync(result.DecisionId, LoanId);
        }

        [Fact]
        public async Task CanonicalPlacementV2Graduation_ImmediateRetryReconcilesSameGraph_PostgreSQL()
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);

            var result = await GraduateAsync(LoanId);
            var retry = await GraduateAsync(LoanId);

            await using (var context = new AqualLifeStyleDbContext(
                             new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                                 .UseNpgsql(ConnectionString)
                                 .Options))
            {
                var loan = await context.OnyxLoanAgreements
                    .SingleAsync(item => item.Id == LoanId);
                loan.AssessCompliance(PlacedAt.AddMonths(4));
                await context.SaveChangesAsync();
                loan.Status.ShouldBe(OnyxLoanAgreementStatus.Overdue);
            }

            var lifecycleRetry = await GraduateAsync(LoanId);

            retry.DecisionId.ShouldBe(result.DecisionId);
            retry.OnyxParticipationId.ShouldBe(result.OnyxParticipationId);
            lifecycleRetry.DecisionId.ShouldBe(result.DecisionId);
            lifecycleRetry.OnyxParticipationId.ShouldBe(result.OnyxParticipationId);
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(1);
            await AssertCoherentGraphAsync(result.DecisionId, LoanId);
        }

        [Fact]
        public async Task CanonicalLegacyV1Graduation_ImmediateRetryReconcilesSameGraph_PostgreSQL()
        {
            await SeedFixtureAsync();
            await ConfigureLegacyRecruiterNetworkAsync();
            AQGreenGraduationPostgreSqlSelector.Shared.Model =
                AQGreenGraduationStructuralModel.LegacyV1;

            var result = await GraduateAsync(LoanId);
            var retry = await GraduateAsync(LoanId);

            result.StructuralModel.ShouldBe(AQGreenGraduationStructuralModel.LegacyV1);
            retry.DecisionId.ShouldBe(result.DecisionId);
            retry.OnyxParticipationId.ShouldBe(result.OnyxParticipationId);
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(0);
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            var decision = await context.OnyxGraduationDecisions
                .SingleAsync(item => item.Id == result.DecisionId);
            decision.GraduationRulesVersion.ShouldBe(OnyxGraduationRules.CurrentVersion);
            decision.EvaluatedLoanTermsVersion.ShouldBe("accepted-b52-x");
            decision.EvaluatedNetworkLevel.ShouldBe(EntryNetworkLevel.Level2);
            (await context.OnyxParticipations.CountAsync()).ShouldBe(1);
            (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(1);
            (await context.AQGreenV2GraduationEvidence.CountAsync()).ShouldBe(0);
        }

        [Theory]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1)]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2)]
        public async Task TermsEffectiveFromDivergence_RetryRequiresReconciliation_PostgreSQL(
            AQGreenGraduationStructuralModel model)
        {
            var fixture = await SeedFixtureAsync();
            AQGreenGraduationPostgreSqlSelector.Shared.Model = model;
            if (model == AQGreenGraduationStructuralModel.LegacyV1)
                await ConfigureLegacyRecruiterNetworkAsync();
            else
                ConfigureEvaluator(fixture);
            var result = await GraduateAsync(LoanId);

            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    """
                    UPDATE public."OnyxParticipations"
                    SET "TermsEffectiveFrom" = "TermsEffectiveFrom" + INTERVAL '1 day'
                    WHERE "Id" = @onyxParticipationId;
                    """,
                    connection);
                command.Parameters.AddWithValue(
                    "onyxParticipationId",
                    result.OnyxParticipationId);
                (await command.ExecuteNonQueryAsync()).ShouldBe(1);
            }

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                GraduateAsync(LoanId));

            exception.Message.ShouldBe("Onyx graduation requires reconciliation.");
            exception.Details.ShouldContain("conflicts with its accepted agreement");
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(
                model == AQGreenGraduationStructuralModel.PlacementV2 ? 1 : 0);
            await AssertGraduationGraphCountsAsync(
                expectedEvidenceHeaders:
                    model == AQGreenGraduationStructuralModel.PlacementV2 ? 1 : 0,
                expectedEvidenceNodes:
                    model == AQGreenGraduationStructuralModel.PlacementV2 ? 31 : 0);
        }

        [Fact]
        public async Task MigratedPreB52LegacyGraph_PublicRetryReturnsDurableGraduation_PostgreSQL()
        {
            await SeedMigratedHistoricalLegacyGraphAsync();

            var retry = await GraduateAsync(LoanId);

            retry.DecisionId.ShouldBe(HistoricalDecisionId);
            retry.OnyxParticipationId.ShouldBe(HistoricalOnyxParticipationId);
            retry.StructuralModel.ShouldBe(AQGreenGraduationStructuralModel.LegacyV1);
            retry.EvaluatedNetworkLevel.ShouldBe(EntryNetworkLevel.Level2);
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(0);
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            var decision = await context.OnyxGraduationDecisions
                .SingleAsync(item => item.Id == HistoricalDecisionId);
            decision.GraduationRulesVersion.ShouldBeNull();
            decision.EvaluatedLoanTermsVersion.ShouldBeNull();
            (await context.OnyxParticipations.CountAsync()).ShouldBe(1);
            (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(1);
            (await context.AQGreenV2GraduationEvidence.CountAsync()).ShouldBe(0);
        }

        [Fact]
        public async Task MigratedPreB52LegacyGraph_CorruptTerminalLinkFailsReconciliation_PostgreSQL()
        {
            await SeedMigratedHistoricalLegacyGraphAsync();
            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    """
                    UPDATE public."OnyxParticipations"
                    SET "AdmissionRoute" = 0
                    WHERE "Id" = @onyxParticipationId;
                    """,
                    connection);
                command.Parameters.AddWithValue(
                    "onyxParticipationId",
                    HistoricalOnyxParticipationId);
                (await command.ExecuteNonQueryAsync()).ShouldBe(1);
            }

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                GraduateAsync(LoanId));

            exception.Message.ShouldBe("Onyx graduation requires reconciliation.");
            exception.Details.ShouldContain("terminal graph is inconsistent");
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(0);
            await AssertGraduationGraphCountsAsync(0, 0);
        }

        [Fact]
        public async Task UnsupportedGraduationRulesVersion_RetryFailsReconciliation_PostgreSQL()
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);
            var result = await GraduateAsync(LoanId);

            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    """
                    UPDATE public."OnyxGraduationDecisions"
                    SET "GraduationRulesVersion" = 'unsupported-graduation-version'
                    WHERE "Id" = @decisionId;
                    """,
                    connection);
                command.Parameters.AddWithValue("decisionId", result.DecisionId);
                (await command.ExecuteNonQueryAsync()).ShouldBe(1);
            }

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                GraduateAsync(LoanId));

            exception.Message.ShouldBe("Onyx graduation requires reconciliation.");
            exception.Details.ShouldContain("unsupported graduation rules version");
            AQGreenGraduationPostgreSqlEvaluator.Shared.CallCount.ShouldBe(1);
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            (await context.OnyxParticipations.CountAsync()).ShouldBe(1);
            (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(1);
            (await context.AQGreenV2GraduationEvidence.CountAsync()).ShouldBe(1);
            (await context.AQGreenV2GraduationEvidenceNodes.CountAsync()).ShouldBe(31);
        }

        [Fact]
        public async Task SecondLoanForSameAQGreen_IsRejectedByExistingPostgreSqlInvariant()
        {
            await SeedFixtureAsync();
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            var secondLoanId = Guid.Parse("b5200000-0000-0000-0000-000000000099");
            var exception = await Should.ThrowAsync<PostgresException>(async () =>
            {
                await using var command = new NpgsqlCommand(
                    LoanInsertSql,
                    connection);
                AddLoanParameters(command, secondLoanId);
                await command.ExecuteNonQueryAsync();
            });

            exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
            exception.ConstraintName.ShouldBe(
                "IX_OnyxLoanAgreements_EntryParticipationId");
            await AssertNoGraduationGraphAsync();
        }

        [Fact]
        public async Task FailureBeforePersistence_RollsBackEntireGraph_PostgreSQL()
        {
            await SeedFixtureAsync();
            AQGreenGraduationPostgreSqlEvaluator.Shared.Failure =
                new InvalidOperationException("Controlled failure before persistence.");

            await Should.ThrowAsync<InvalidOperationException>(() => GraduateAsync(LoanId));

            AQGreenGraduationPostgreSqlFailureState.Shared.SawOnyxTracked.ShouldBeFalse();
            AQGreenGraduationPostgreSqlFailureState.Shared.SawDecisionTracked.ShouldBeFalse();
            AQGreenGraduationPostgreSqlFailureState.Shared.SawEvidenceTracked.ShouldBeFalse();
            await AssertNoGraduationGraphAsync();
        }

        [Theory]
        [InlineData(AQGreenGraduationPostgreSqlFailureMode.AfterOnyxTracked)]
        [InlineData(AQGreenGraduationPostgreSqlFailureMode.AfterDecisionTracked)]
        [InlineData(AQGreenGraduationPostgreSqlFailureMode.DuringEvidenceInsert)]
        [InlineData(AQGreenGraduationPostgreSqlFailureMode.DeferredManifestValidation)]
        public async Task PersistenceStageFailure_RollsBackEntireGraph_PostgreSQL(
            AQGreenGraduationPostgreSqlFailureMode mode)
        {
            var fixture = await SeedFixtureAsync();
            ConfigureEvaluator(fixture);
            AQGreenGraduationPostgreSqlFailureState.Shared.Mode = mode;

            await Should.ThrowAsync<Exception>(() => GraduateAsync(LoanId));

            var state = AQGreenGraduationPostgreSqlFailureState.Shared;
            if (mode == AQGreenGraduationPostgreSqlFailureMode.AfterOnyxTracked)
                state.SawOnyxTracked.ShouldBeTrue();
            if (mode == AQGreenGraduationPostgreSqlFailureMode.AfterDecisionTracked)
                state.SawDecisionTracked.ShouldBeTrue();
            if (mode == AQGreenGraduationPostgreSqlFailureMode.DuringEvidenceInsert)
                state.SawEvidenceTracked.ShouldBeTrue();
            await AssertNoGraduationGraphAsync();
        }

        private string ConnectionString =>
            AQGreenGraduationPostgreSqlFailureState.Shared.ConnectionString;

        private Task<OnyxGraduationDecisionDto> GraduateAsync(Guid loanAgreementId) =>
            _service.GraduateAQGreenToOnyxAsync(new GraduateAQGreenToOnyxInput
            {
                LoanAgreementId = loanAgreementId,
                Justification = "B5.2 PostgreSQL application coordinator acceptance"
            });

        private async Task<PostgreSqlGraduationFixture> SeedFixtureAsync()
        {
            var placements = new List<Guid>();
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."AreaAdminAssignments" (
                    "Id", "TenantId", "AreaId", "UserId", "EffectiveFrom",
                    "CreationTime", "IsDeleted")
                VALUES (
                    'b5200000-0000-0000-0000-000000000010', 1,
                    'c0000000-0000-0000-0000-000000000001', 3001,
                    @placedAt, @placedAt, FALSE);

                INSERT INTO public."Memberships" (
                    "Id", "TenantId", "Name", "Description", "MembershipType",
                    "IsActive", "MonthlyObligationAmount", "ActivationDate",
                    "LastObligationMetDate")
                VALUES (
                    920001, 1, 'B5.2 PostgreSQL Onyx', NULL, 1,
                    TRUE, 0, NULL, NULL);

                INSERT INTO public."AQGreenPlacementTreeScopes" ("Id", "TenantId")
                VALUES (@scopeId, 1);
                """,
                new NpgsqlParameter("placedAt", PlacedAt),
                new NpgsqlParameter("scopeId", ScopeId));

            for (var number = 1; number <= 31; number++)
            {
                var placementId = Placement(number);
                placements.Add(placementId);
                Guid? parentParticipantId;
                int? slot;
                string path;
                if (number == 1)
                {
                    parentParticipantId = null;
                    slot = null;
                    path = string.Empty;
                }
                else if (number <= 6)
                {
                    parentParticipantId = Participant(1);
                    slot = number - 1;
                    path = slot.Value.ToString();
                }
                else
                {
                    var depthTwoIndex = number - 7;
                    var parentSlot = depthTwoIndex / 5 + 1;
                    parentParticipantId = Participant(parentSlot + 1);
                    slot = depthTwoIndex % 5 + 1;
                    path = $"{parentSlot}{slot}";
                }

                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO public."AQGreenNetworkPlacements" (
                        "Id", "TenantId", "PlacementTreeScopeId", "ParticipantId",
                        "PlacementParentParticipantId", "PlacementSlot", "CanonicalPath",
                        "PlacedAt", "RulesVersion")
                    VALUES (
                        @id, 1, @scopeId, @participantId, @parentParticipantId,
                        @slot, @path, @placedAt, @rulesVersion);
                    """,
                    new NpgsqlParameter("id", placementId),
                    new NpgsqlParameter("scopeId", ScopeId),
                    new NpgsqlParameter("participantId", Participant(number)),
                    new NpgsqlParameter(
                        "parentParticipantId",
                        parentParticipantId.HasValue
                            ? parentParticipantId.Value
                            : DBNull.Value),
                    new NpgsqlParameter(
                        "slot",
                        slot.HasValue ? slot.Value : DBNull.Value),
                    new NpgsqlParameter("path", path),
                    new NpgsqlParameter("placedAt", PlacedAt),
                    new NpgsqlParameter("rulesVersion", AQGreenPlacementRules.CurrentVersion));
            }

            await using (var loan = new NpgsqlCommand(LoanInsertSql, connection, transaction))
            {
                AddLoanParameters(loan, LoanId);
                await loan.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();

            return new PostgreSqlGraduationFixture(placements);
        }

        private static void AddLoanParameters(NpgsqlCommand command, Guid loanId)
        {
            command.Parameters.AddWithValue("loanId", loanId);
            command.Parameters.AddWithValue("entryParticipationId", Participant(1));
            command.Parameters.AddWithValue("offeredAt", PlacedAt.AddDays(-5));
            command.Parameters.AddWithValue("acceptedAt", PlacedAt.AddDays(-4));
            command.Parameters.AddWithValue("approvedAt", PlacedAt.AddDays(-3));
            command.Parameters.AddWithValue("effectiveAt", PlacedAt.AddDays(-3));
            command.Parameters.AddWithValue("repaymentDeadlineAt", PlacedAt.AddMonths(3));
            command.Parameters.AddWithValue("creationTime", PlacedAt.AddDays(-5));
        }

        private static async Task ExecuteAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql,
            params NpgsqlParameter[] parameters)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        private async Task ConfigureLegacyRecruiterNetworkAsync()
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                UPDATE public."EntryParticipations"
                SET "RecruiterCustomerId" = CASE
                        WHEN "CustomerId" BETWEEN 2 AND 6 THEN 1
                        WHEN "CustomerId" BETWEEN 7 AND 31
                            THEN (("CustomerId" - 7) / 5) + 2
                        ELSE "RecruiterCustomerId"
                    END,
                    "ActivatedAt" = @activatedAt
                WHERE "TenantId" = 1;
                """,
                connection);
            command.Parameters.AddWithValue("activatedAt", PlacedAt.AddDays(-1));
            (await command.ExecuteNonQueryAsync()).ShouldBe(64);
        }

        private async Task SeedMigratedHistoricalLegacyGraphAsync()
        {
            await MigrateAsync(PreviousMigration);
            await SeedFixtureAsync();
            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO public."OnyxParticipations" (
                        "Id", "TenantId", "CustomerId", "RecruiterCustomerId",
                        "OnyxMembershipId", "AdmissionRoute", "Status", "StartedAt",
                        "ActivatedAt", "DirectEntryPaymentId", "EntryParticipationId",
                        "LoanAgreementId", "TermsVersion", "TermsEffectiveFrom",
                        "DirectEntryAmount", "Currency", "CreationTime", "CreatorUserId",
                        "LastModificationTime", "LastModifierUserId", "IsDeleted",
                        "DeleterUserId", "DeletionTime")
                    VALUES (
                        @onyxParticipationId, 1, 1, NULL, @membershipId, 1, 1,
                        @decidedAt, @decidedAt, NULL, @entryParticipationId,
                        @loanId, 'historical-onyx-v1', @historicalTermsEffectiveFrom,
                        6120, 'ZAR', @decidedAt, 3001, NULL, NULL, FALSE, NULL, NULL);

                    INSERT INTO public."OnyxGraduationDecisions" (
                        "Id", "TenantId", "CustomerId", "EntryParticipationId",
                        "LoanAgreementId", "OnyxParticipationId", "AdministratorUserId",
                        "DecidedAt", "Justification", "EvaluatedNetworkLevel",
                        "AQGreenWasActive", "LoanWasActive", "LoanWasAccepted",
                        "LoanWasAdministratorApproved", "EvaluatedFundingAmount",
                        "EvaluatedFundingCurrency", "CreationTime", "CreatorUserId")
                    VALUES (
                        @decisionId, 1, 1, @entryParticipationId, @loanId,
                        @onyxParticipationId, 3001, @decidedAt,
                        'Pre-B5.2 historical Legacy V1 graduation', 2,
                        TRUE, TRUE, TRUE, TRUE, 6120, 'ZAR', @decidedAt, 3001);
                    """,
                    new NpgsqlParameter(
                        "onyxParticipationId",
                        HistoricalOnyxParticipationId),
                    new NpgsqlParameter("membershipId", MembershipId),
                    new NpgsqlParameter("entryParticipationId", Participant(1)),
                    new NpgsqlParameter("loanId", LoanId),
                    new NpgsqlParameter("decisionId", HistoricalDecisionId),
                    new NpgsqlParameter("decidedAt", PlacedAt),
                    new NpgsqlParameter(
                        "historicalTermsEffectiveFrom",
                        PlacedAt.AddMonths(-1)));
                await transaction.CommitAsync();
            }

            await MigrateAsync();
        }

        private async Task MigrateAsync(string targetMigration = null)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(connection)
                    .Options);
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
        }

        private static void ConfigureEvaluator(PostgreSqlGraduationFixture fixture)
        {
            AQGreenGraduationPostgreSqlEvaluator.Shared.ResultFactory =
                (_, participantId, cutoff) =>
                    new AQGreenGraduationStructuralEvidenceResult(
                        participantId,
                        ScopeId,
                        cutoff,
                        AQGreenStructuralCompletionLevel.Level2,
                        5,
                        25,
                        AQGreenStructuralQualificationRules.CurrentVersion,
                        fixture.Placements.Select((placementId, ordinal) =>
                            new AQGreenGraduationStructuralEvidenceObservation
                            {
                                CanonicalOrdinal = ordinal,
                                SourcePlacementId = placementId,
                                ParticipationStatusObserved =
                                    EntryParticipationStatus.Active,
                                ParticipationActivatedAtObserved = PlacedAt.AddDays(-1),
                                ParticipationIsDeletedObserved = false,
                                CustomerIdObserved = ordinal + 1,
                                CustomerTenantMatchedObserved = true,
                                CustomerIsActiveObserved = true,
                                CustomerIsDeletedObserved = false,
                                UserIdObserved = 3001L + ordinal,
                                UserTenantMatchedObserved = true,
                                UserIsActiveObserved = true,
                                UserIsDeletedObserved = false
                            })
                            .ToList());
        }

        private async Task AssertCoherentGraphAsync(Guid decisionId, Guid loanId)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(connection)
                    .Options);
            var decision = await context.OnyxGraduationDecisions
                .SingleAsync(item => item.Id == decisionId);
            decision.TenantId.ShouldBe(1);
            decision.CustomerId.ShouldBe(1);
            decision.EntryParticipationId.ShouldBe(Participant(1));
            decision.LoanAgreementId.ShouldBe(loanId);
            decision.StructuralModel.ShouldBe(AQGreenGraduationStructuralModel.PlacementV2);
            decision.GraduationRulesVersion.ShouldBe(OnyxGraduationRules.CurrentVersion);
            decision.EvaluatedLoanTermsVersion.ShouldBe("accepted-b52-x");
            decision.EvaluatedFundingCurrency.ShouldBe("ZAR");

            var loan = await context.OnyxLoanAgreements
                .SingleAsync(item => item.Id == loanId);
            loan.TermsVersion.ShouldBe("accepted-b52-x");
            loan.Currency.ShouldBe("ZAR");

            var onyx = await context.OnyxParticipations
                .SingleAsync(item => item.Id == decision.OnyxParticipationId);
            onyx.TenantId.ShouldBe(1);
            onyx.CustomerId.ShouldBe(1);
            onyx.AdmissionRoute.ShouldBe(OnyxAdmissionRoute.EntryGraduation);
            onyx.EntryParticipationId.ShouldBe(Participant(1));
            onyx.LoanAgreementId.ShouldBe(loanId);
            onyx.TermsVersion.ShouldBe("accepted-b52-x");
            onyx.Currency.ShouldBe("ZAR");

            var evidence = await context.AQGreenV2GraduationEvidence
                .Include(item => item.Nodes)
                .SingleAsync(item => item.Id == decision.Id);
            evidence.EvidenceNodeCount.ShouldBe(31);
            evidence.Nodes.Count.ShouldBe(31);

            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            var replay = await new AQGreenV2GraduationEvidenceReplayValidator(provider)
                .ValidateAsync(decision.Id);
            replay.StructuralCompletionLevel.ShouldBe(
                AQGreenStructuralCompletionLevel.Level2);
            replay.EvidenceNodeCount.ShouldBe(31);

            (await context.OnyxParticipations.CountAsync()).ShouldBe(1);
            (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(1);
            (await context.AQGreenV2GraduationEvidence.CountAsync()).ShouldBe(1);
        }

        private async Task AssertNoGraduationGraphAsync()
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            (await context.OnyxParticipations.CountAsync()).ShouldBe(0);
            (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(0);
            (await context.AQGreenV2GraduationEvidence.CountAsync()).ShouldBe(0);
            (await context.AQGreenV2GraduationEvidenceNodes.CountAsync()).ShouldBe(0);
        }

        private async Task AssertGraduationGraphCountsAsync(
            int expectedEvidenceHeaders,
            int expectedEvidenceNodes)
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            (await context.OnyxParticipations.CountAsync()).ShouldBe(1);
            (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(1);
            (await context.AQGreenV2GraduationEvidence.CountAsync())
                .ShouldBe(expectedEvidenceHeaders);
            (await context.AQGreenV2GraduationEvidenceNodes.CountAsync())
                .ShouldBe(expectedEvidenceNodes);
        }

        private static Guid Participant(int number) =>
            AQGreenPlacementAllocatorPostgreSqlFixture.Participant(1, number);

        private static Guid Placement(int number) =>
            Guid.Parse($"b5200001-0000-0000-0000-{number:D12}");

        private static T Find<T>(Exception exception) where T : Exception
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is T match) return match;
            return null;
        }

        private sealed record PostgreSqlGraduationFixture(IReadOnlyList<Guid> Placements);

        private const string LoanInsertSql =
            """
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
                'accepted-b52-x', 6120, 30, 7956, 7956, 'ZAR',
                3, 4, 200, @offeredAt, 3001,
                'Accepted B5.2 terms X', @acceptedAt, 3001, @approvedAt, @effectiveAt,
                @repaymentDeadlineAt, NULL, NULL,
                @creationTime, 3001, NULL, NULL, FALSE, NULL, NULL);
            """;
    }
}
