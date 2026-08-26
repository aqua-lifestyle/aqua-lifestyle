using System;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementTopologyPostgreSqlCollection.Name)]
    public sealed class AQGreenRecruitmentAttributionPostgreSqlTests
    {
        private static readonly DateTime AttributedAt =
            new(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc);
        private readonly AQGreenPlacementTopologyPostgreSqlFixture _fixture;

        public AQGreenRecruitmentAttributionPostgreSqlTests(
            AQGreenPlacementTopologyPostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task EfMapping_PersistsConfirmedCrossAreaAttributionWithoutPlacement()
        {
            await using var connection = await _fixture.OpenConnectionAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            var attribution = MemberAttribution(P(2), P(1));
            var confirmation = Confirmation(attribution);

            context.AddRange(attribution, confirmation);
            await context.SaveChangesAsync();

            context.ChangeTracker.Clear();
            var persistedAttribution = await context.AQGreenRecruitmentAttributions
                .AsNoTracking()
                .SingleAsync(row => row.Id == attribution.Id);
            var persistedConfirmation = await context.AQGreenRecruitmentAttributionConfirmations
                .AsNoTracking()
                .SingleAsync(row => row.Id == confirmation.Id);

            persistedAttribution.TenantId.ShouldBe(attribution.TenantId);
            persistedAttribution.ParticipantId.ShouldBe(attribution.ParticipantId);
            persistedAttribution.CreditedSponsorParticipantId.ShouldBe(
                attribution.CreditedSponsorParticipantId);
            persistedAttribution.AttributionKind.ShouldBe(attribution.AttributionKind);
            persistedAttribution.AcquisitionSource.ShouldBe(attribution.AcquisitionSource);
            persistedAttribution.SourceReferenceId.ShouldBe(attribution.SourceReferenceId);
            persistedAttribution.AttributedAt.ShouldBe(attribution.AttributedAt);
            persistedAttribution.AttributedByUserId.ShouldBeNull();
            persistedAttribution.AssignmentReason.ShouldBeNull();
            persistedAttribution.RulesVersion.ShouldBe(attribution.RulesVersion);
            persistedConfirmation.TenantId.ShouldBe(confirmation.TenantId);
            persistedConfirmation.AttributionId.ShouldBe(confirmation.AttributionId);
            persistedConfirmation.ConfirmedAt.ShouldBe(confirmation.ConfirmedAt);
            persistedConfirmation.ConfirmedByUserId.ShouldBeNull();
            persistedConfirmation.ConfirmationMethod.ShouldBe(
                confirmation.ConfirmationMethod);
            persistedConfirmation.EvidenceReferenceId.ShouldBe(
                confirmation.EvidenceReferenceId);
            persistedConfirmation.RulesVersion.ShouldBe(confirmation.RulesVersion);

            (await context.AQGreenRecruitmentAttributions
                    .AsNoTracking()
                    .CountAsync(row => row.Id == attribution.Id))
                .ShouldBe(1);
            (await context.AQGreenRecruitmentAttributionConfirmations
                    .AsNoTracking()
                    .CountAsync(row => row.AttributionId == attribution.Id))
                .ShouldBe(1);
            (await ScalarAsync(
                    connection,
                    transaction.GetDbTransaction() as NpgsqlTransaction,
                    $$"""
                    SELECT COUNT(DISTINCT customer."AreaId")
                    FROM public."EntryParticipations" participation
                    JOIN public."Customers" customer
                      ON customer."TenantId" = participation."TenantId"
                     AND customer."Id" = participation."CustomerId"
                    WHERE participation."TenantId" = 1
                      AND participation."Id" IN ('{{P(1)}}', '{{P(2)}}');
                    """))
                .ShouldBe(2);
            (await ScalarAsync(
                    connection,
                    transaction.GetDbTransaction() as NpgsqlTransaction,
                    $$"""
                    SELECT COUNT(*)
                    FROM public."AQGreenNetworkPlacements"
                    WHERE "TenantId" = 1 AND "ParticipantId" = '{{P(2)}}';
                    """))
                .ShouldBe(0);

            await transaction.RollbackAsync();
        }

        [Fact]
        public async Task RootAttribution_PersistsExplicitNullSponsorShapeWithoutPlacement()
        {
            await InTransactionAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection,
                    transaction,
                    participantId: P(3),
                    sponsorId: null,
                    source: AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    kind: AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    actorId: 1001,
                    reason: "Authorised prospective root attribution");

                (await ScalarAsync(
                        connection,
                        transaction,
                        $$"""
                        SELECT COUNT(*)
                        FROM public."AQGreenRecruitmentAttributions"
                        WHERE "Id" = '{{attributionId}}'
                          AND "CreditedSponsorParticipantId" IS NULL;
                        """))
                    .ShouldBe(1);
                (await ScalarAsync(
                        connection,
                        transaction,
                        $$"""
                        SELECT COUNT(*)
                        FROM public."AQGreenNetworkPlacements"
                        WHERE "TenantId" = 1 AND "ParticipantId" = '{{P(3)}}';
                        """))
                    .ShouldBe(0);
            });
        }

        [Fact]
        public async Task Attribution_RejectsSecondSponsorFactForParticipant()
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                await InsertAttributionAsync(connection, transaction, P(4), P(1));
                await InsertAttributionAsync(connection, transaction, P(4), P(2));
            }, PostgresErrorCodes.UniqueViolation);
        }

        [Fact]
        public async Task Attribution_RejectsCrossTenantSponsor()
        {
            await ExpectRejectedAsync(
                (connection, transaction) =>
                    InsertAttributionAsync(connection, transaction, P(5), P(1, 2)),
                PostgresErrorCodes.RaiseException);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Attribution_RejectsMissingCrossTenantOrWrongSponsorInvitation(
            int invalid)
        {
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection,
                    transaction,
                    P(5),
                    P(1),
                    sourceReferenceId: invalid switch
                    {
                        1 => Guid.NewGuid(),
                        2 => P(1, 2),
                        _ => P(2)
                    }),
                PostgresErrorCodes.RaiseException);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Attribution_RejectsDeletedOrNonAQGreenInvitation(
            bool deleted)
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    deleted
                        ? $$"""
                          UPDATE public."ProgrammeInvitations"
                          SET "IsDeleted" = TRUE
                          WHERE "Id" = '{{P(1)}}';
                          """
                        : $$"""
                          UPDATE public."ProgrammeInvitations"
                          SET "ProgrammeKey" = 'ONYX'
                          WHERE "Id" = '{{P(1)}}';
                          """);
                await InsertAttributionAsync(
                    connection,
                    transaction,
                    P(5),
                    P(1));
            }, PostgresErrorCodes.RaiseException);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Attribution_PreventsHardDeletionOrRebindingInvitationEvidence(
            bool delete)
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                await InsertAttributionAsync(connection, transaction, P(5), P(1));
                await ExecuteAsync(
                    connection,
                    transaction,
                    delete
                        ? $$"""
                          DELETE FROM public."ProgrammeInvitations"
                          WHERE "Id" = '{{P(1)}}';
                          """
                        : $$"""
                          UPDATE public."ProgrammeInvitations"
                          SET "ProgrammeParticipationId" = '{{P(99)}}'
                          WHERE "Id" = '{{P(1)}}';
                          """);
            }, PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task Attribution_PreventsInvitationProgrammeRebinding()
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                await InsertAttributionAsync(connection, transaction, P(5), P(1));
                await ExecuteAsync(
                    connection,
                    transaction,
                    $$"""
                    UPDATE public."ProgrammeInvitations"
                    SET "ProgrammeKey" = 'ONYX'
                    WHERE "Id" = '{{P(1)}}';
                    """);
            }, PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task InvitationEvidence_RejectsReplicationRoleHardDeleteBypass()
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                await InsertAttributionAsync(connection, transaction, P(5), P(1));
                await ExecuteAsync(
                    connection,
                    transaction,
                    "SET LOCAL session_replication_role = replica;");
                await ExecuteAsync(
                    connection,
                    transaction,
                    $$"""
                    DELETE FROM public."ProgrammeInvitations"
                    WHERE "Id" = '{{P(1)}}';
                    """);
            }, PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task InvitationEvidence_SerializesConcurrentProgrammeRebinding()
        {
            await using var attributionConnection = await _fixture.OpenConnectionAsync();
            await using var attributionTransaction =
                await attributionConnection.BeginTransactionAsync();
            await InsertAttributionAsync(
                attributionConnection,
                attributionTransaction,
                P(20),
                P(19));

            await using var mutationConnection = await _fixture.OpenConnectionAsync();
            await using var mutationTransaction = await mutationConnection.BeginTransactionAsync();
            var mutationTask = ExecuteAsync(
                mutationConnection,
                mutationTransaction,
                $$"""
                UPDATE public."ProgrammeInvitations"
                SET "ProgrammeKey" = 'ONYX'
                WHERE "Id" = '{{P(19)}}';
                """);

            await Task.Delay(250);
            mutationTask.IsCompleted.ShouldBeFalse();

            await attributionTransaction.CommitAsync();
            var exception = await Should.ThrowAsync<PostgresException>(() =>
                mutationTask.WaitAsync(TimeSpan.FromSeconds(5)));
            exception.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
            await mutationTransaction.RollbackAsync();
        }

        [Fact]
        public async Task InvitationSoftDelete_AfterConfirmation_PreservesHistoricalEvidence()
        {
            await InTransactionAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection,
                    transaction,
                    P(17),
                    P(1));
                await InsertConfirmationAsync(connection, transaction, attributionId);

                await ExecuteAsync(
                    connection,
                    transaction,
                    $$"""
                    UPDATE public."ProgrammeInvitations"
                    SET "IsDeleted" = TRUE,
                        "DeletionTime" = TIMESTAMPTZ '2026-08-26 08:02:00+00',
                        "DeleterUserId" = 1001
                    WHERE "Id" = '{{P(1)}}';
                    """);

                (await ScalarAsync(
                        connection,
                        transaction,
                        $$"""
                        SELECT COUNT(*)
                        FROM public."ProgrammeInvitations"
                        WHERE "Id" = '{{P(1)}}'
                          AND "IsDeleted" = TRUE;
                        """))
                    .ShouldBe(1);
                (await ScalarAsync(
                        connection,
                        transaction,
                        $$"""
                        SELECT COUNT(*)
                        FROM public."AQGreenRecruitmentAttributions" attribution
                        JOIN public."AQGreenRecruitmentAttributionConfirmations" confirmation
                          ON confirmation."TenantId" = attribution."TenantId"
                         AND confirmation."AttributionId" = attribution."Id"
                        WHERE attribution."Id" = '{{attributionId}}'
                          AND attribution."SourceReferenceId" = '{{P(1)}}'
                          AND attribution."CreditedSponsorParticipantId" = '{{P(1)}}';
                        """))
                    .ShouldBe(1);
            });
        }

        [Fact]
        public async Task MemberAttribution_PersistsInvitationIdentityWithoutCode()
        {
            await InTransactionAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection,
                    transaction,
                    P(18),
                    P(1));

                (await ScalarAsync(
                        connection,
                        transaction,
                        $$"""
                        SELECT COUNT(*)
                        FROM public."AQGreenRecruitmentAttributions"
                        WHERE "Id" = '{{attributionId}}'
                          AND "SourceReferenceId" = '{{P(1)}}';
                        """))
                    .ShouldBe(1);
                (await ScalarAsync(
                        connection,
                        transaction,
                        """
                        SELECT COUNT(*)
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name IN (
                              'AQGreenRecruitmentAttributions',
                              'AQGreenRecruitmentAttributionConfirmations')
                          AND lower(column_name) LIKE '%code%';
                        """))
                    .ShouldBe(0);
            });
        }

        [Fact]
        public async Task Attribution_RejectsSelfSponsorship()
        {
            await ExpectRejectedAsync(
                (connection, transaction) =>
                    InsertAttributionAsync(connection, transaction, P(6), P(6)),
                PostgresErrorCodes.CheckViolation);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Attribution_RejectsMissingParticipantOrSponsor(bool participantMissing)
        {
            var missing = Guid.NewGuid();
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection,
                    transaction,
                    participantMissing ? missing : P(7),
                    participantMissing ? P(1) : missing),
                participantMissing
                    ? PostgresErrorCodes.ForeignKeyViolation
                    : PostgresErrorCodes.RaiseException);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public async Task Attribution_RejectsUndefinedAcquisitionSources(int source)
        {
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection,
                    transaction,
                    P(8),
                    P(1),
                    (AQGreenAcquisitionSource)source),
                PostgresErrorCodes.CheckViolation);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public async Task Attribution_RejectsUndefinedAttributionKinds(int kind)
        {
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection,
                    transaction,
                    P(8),
                    P(1),
                    kind: (AQGreenRecruitmentAttributionKind)kind),
                PostgresErrorCodes.CheckViolation);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public async Task Attribution_RejectsInvalidSourceShapes(int shape)
        {
            await ExpectRejectedAsync(
                (connection, transaction) => shape switch
                {
                    1 => InsertAttributionAsync(
                        connection, transaction, P(9), null,
                        AQGreenAcquisitionSource.MemberInvitation),
                    2 => InsertAttributionAsync(
                        connection, transaction, P(9), P(1),
                        AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                        AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                        actorId: 1001, reason: "root"),
                    3 => InsertAttributionAsync(
                        connection, transaction, P(9), null,
                        AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                        AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot),
                    4 => InsertAttributionAsync(
                        connection, transaction, P(9), null,
                        AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                        AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                        actorId: 1001),
                    5 => InsertAttributionAsync(
                        connection, transaction, P(9), P(1),
                        AQGreenAcquisitionSource.AuthorisedDirectAdmission),
                    _ => InsertAttributionAsync(
                        connection, transaction, P(9), null,
                        AQGreenAcquisitionSource.MemberInvitation,
                        AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                        actorId: 1001, reason: "root")
                },
                PostgresErrorCodes.CheckViolation);
        }

        [Fact]
        public async Task Attribution_RejectsWhitespaceRulesAndRootReason()
        {
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection, transaction, P(10), P(1),
                    rulesVersion: "\t\n"),
                PostgresErrorCodes.RaiseException);
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection, transaction, P(10), null,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    actorId: 1001, reason: "\t\n"),
                PostgresErrorCodes.RaiseException);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task Attribution_RejectsInvalidTenantActorReferenceOrMemberReason(
            int invalid)
        {
            await ExpectRejectedAsync(
                (connection, transaction) => InsertAttributionAsync(
                    connection,
                    transaction,
                    P(10),
                    P(1),
                    tenantId: invalid == 1 ? 0 : 1,
                    actorId: invalid == 2 ? 0 : null,
                    reason: invalid == 4 ? "not valid for member invitation" : null,
                    sourceReferenceId: invalid == 3 ? Guid.Empty : null),
                PostgresErrorCodes.CheckViolation);
        }

        [Fact]
        public async Task Confirmation_RejectsDuplicateFact()
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection, transaction, P(11), P(1));
                await InsertConfirmationAsync(connection, transaction, attributionId);
                await InsertConfirmationAsync(connection, transaction, attributionId);
            }, PostgresErrorCodes.UniqueViolation);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Confirmation_RejectsCrossTenantOrMissingAttribution(bool crossTenant)
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                var attributionId = crossTenant
                    ? await InsertAttributionAsync(connection, transaction, P(12), P(1))
                    : Guid.NewGuid();
                await InsertConfirmationAsync(
                    connection,
                    transaction,
                    attributionId,
                    tenantId: crossTenant ? 2 : 1);
            }, PostgresErrorCodes.RaiseException);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public async Task Confirmation_RejectsInvalidMethodEvidenceRulesOrChronology(int invalid)
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection, transaction, P(13), P(1));
                await InsertConfirmationAsync(
                    connection,
                    transaction,
                    attributionId,
                    tenantId: invalid == 6 ? 0 : 1,
                    method: invalid == 1 ? 3 : 1,
                    evidenceReferenceId: invalid == 2 ? Guid.Empty : Guid.NewGuid(),
                    rulesVersion: invalid == 3 ? "\t" :
                        AQGreenRecruitmentAttributionRules.CurrentVersion,
                    confirmedAt: invalid == 4 ? AttributedAt.AddTicks(-1) :
                        AttributedAt.AddMinutes(1),
                    confirmedByUserId: invalid == 5 ? 0 : null);
            }, invalid == 1 || invalid == 2 || invalid == 5
                ? PostgresErrorCodes.CheckViolation
                : PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task Confirmation_RequiresMethodMatchingAttributionSource()
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection, transaction, P(14), null,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    actorId: 1001, reason: "root");
                await InsertConfirmationAsync(connection, transaction, attributionId);
            }, PostgresErrorCodes.RaiseException);

            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection, transaction, P(14), P(1));
                await InsertConfirmationAsync(
                    connection,
                    transaction,
                    attributionId,
                    method: (int)AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation);
            }, PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task Confirmation_CanConfirmProspectiveRootWithoutCreatingPlacement()
        {
            await InTransactionAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection, transaction, P(14), null,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    actorId: 1001, reason: "prospective root evidence");
                await InsertConfirmationAsync(
                    connection,
                    transaction,
                    attributionId,
                    method: (int)AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation);

                (await ScalarAsync(
                        connection,
                        transaction,
                        $$"""
                        SELECT COUNT(*)
                        FROM public."AQGreenNetworkPlacements"
                        WHERE "TenantId" = 1 AND "ParticipantId" = '{{P(14)}}';
                        """))
                    .ShouldBe(0);
            });
        }

        [Theory]
        [InlineData("attribution", "update")]
        [InlineData("attribution", "delete")]
        [InlineData("attribution", "truncate")]
        [InlineData("confirmation", "update")]
        [InlineData("confirmation", "delete")]
        [InlineData("confirmation", "truncate")]
        public async Task AttributionEvidence_RejectsDirectMutation(
            string record,
            string operation)
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                var attributionId = await InsertAttributionAsync(
                    connection, transaction, P(15), P(1));
                var confirmationId = record == "confirmation"
                    ? await InsertConfirmationAsync(connection, transaction, attributionId)
                    : Guid.Empty;
                var sql = (record, operation) switch
                {
                    ("attribution", "update") =>
                        $"UPDATE public.\"AQGreenRecruitmentAttributions\" SET \"RulesVersion\" = 'changed' WHERE \"Id\" = '{attributionId}';",
                    ("attribution", "delete") =>
                        $"DELETE FROM public.\"AQGreenRecruitmentAttributions\" WHERE \"Id\" = '{attributionId}';",
                    ("attribution", _) =>
                        "TRUNCATE public.\"AQGreenRecruitmentAttributions\" CASCADE;",
                    ("confirmation", "update") =>
                        $"UPDATE public.\"AQGreenRecruitmentAttributionConfirmations\" SET \"RulesVersion\" = 'changed' WHERE \"Id\" = '{confirmationId}';",
                    ("confirmation", "delete") =>
                        $"DELETE FROM public.\"AQGreenRecruitmentAttributionConfirmations\" WHERE \"Id\" = '{confirmationId}';",
                    _ => "TRUNCATE public.\"AQGreenRecruitmentAttributionConfirmations\";"
                };
                await ExecuteAsync(connection, transaction, sql);
            }, PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task AttributionEvidence_RejectsReplicationRoleTriggerBypass()
        {
            await ExpectRejectedAsync(async (connection, transaction) =>
            {
                await InsertAttributionAsync(connection, transaction, P(16), P(1));
                await ExecuteAsync(
                    connection,
                    transaction,
                    "SET LOCAL session_replication_role = replica;");
                await ExecuteAsync(
                    connection,
                    transaction,
                    "TRUNCATE public.\"AQGreenRecruitmentAttributions\" CASCADE;");
            }, PostgresErrorCodes.RaiseException);
        }

        [Fact]
        public async Task RolledBackAttribution_LeavesNoPersistentEvidence()
        {
            var attributionId = Guid.NewGuid();
            await using (var connection = await _fixture.OpenConnectionAsync())
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await InsertAttributionAsync(
                    connection,
                    transaction,
                    P(16),
                    P(1),
                    attributionId: attributionId);
                await transaction.RollbackAsync();
            }

            await using var verification = await _fixture.OpenConnectionAsync();
            (await ScalarAsync(
                    verification,
                    null,
                    $"SELECT COUNT(*) FROM public.\"AQGreenRecruitmentAttributions\" WHERE \"Id\" = '{attributionId}';"))
                .ShouldBe(0);
        }

        private async Task InTransactionAsync(
            Func<NpgsqlConnection, NpgsqlTransaction, Task> action)
        {
            await using var connection = await _fixture.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await action(connection, transaction);
            }
            finally
            {
                await transaction.RollbackAsync();
            }
        }

        private async Task ExpectRejectedAsync(
            Func<NpgsqlConnection, NpgsqlTransaction, Task> action,
            string expectedSqlState)
        {
            var exception = await Should.ThrowAsync<PostgresException>(async () =>
            {
                await using var connection = await _fixture.OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await action(connection, transaction);
                await transaction.CommitAsync();
            });
            exception.SqlState.ShouldBe(expectedSqlState);
        }

        private static async Task<Guid> InsertAttributionAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid participantId,
            Guid? sponsorId,
            AQGreenAcquisitionSource source = AQGreenAcquisitionSource.MemberInvitation,
            AQGreenRecruitmentAttributionKind kind =
                AQGreenRecruitmentAttributionKind.SponsoredParticipant,
            int tenantId = 1,
            long? actorId = null,
            string reason = null,
            string rulesVersion = AQGreenRecruitmentAttributionRules.CurrentVersion,
            Guid? attributionId = null,
            Guid? sourceReferenceId = null)
        {
            var id = attributionId ?? Guid.NewGuid();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO public."AQGreenRecruitmentAttributions" (
                    "Id", "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                    "AttributionKind", "AcquisitionSource", "SourceReferenceId", "AttributedAt",
                    "AttributedByUserId", "AssignmentReason", "RulesVersion")
                VALUES (
                    @id, @tenantId, @participantId, @sponsorId,
                    @kind, @source, @sourceReferenceId, @attributedAt,
                    @actorId, @reason, @rulesVersion);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("tenantId", tenantId);
            command.Parameters.AddWithValue("participantId", participantId);
            command.Parameters.AddWithValue("sponsorId", sponsorId.HasValue
                ? sponsorId.Value
                : DBNull.Value);
            command.Parameters.AddWithValue("kind", (int)kind);
            command.Parameters.AddWithValue("source", (int)source);
            command.Parameters.AddWithValue(
                "sourceReferenceId",
                sourceReferenceId ??
                (source == AQGreenAcquisitionSource.MemberInvitation && sponsorId.HasValue
                    ? sponsorId.Value
                    : Guid.NewGuid()));
            command.Parameters.AddWithValue("attributedAt", AttributedAt);
            command.Parameters.AddWithValue("actorId", actorId.HasValue
                ? actorId.Value
                : DBNull.Value);
            command.Parameters.AddWithValue("reason", reason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("rulesVersion", rulesVersion);
            await command.ExecuteNonQueryAsync();
            return id;
        }

        private static async Task<Guid> InsertConfirmationAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid attributionId,
            int tenantId = 1,
            int method = 1,
            Guid? evidenceReferenceId = null,
            string rulesVersion = AQGreenRecruitmentAttributionRules.CurrentVersion,
            DateTime? confirmedAt = null,
            long? confirmedByUserId = null)
        {
            var id = Guid.NewGuid();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO public."AQGreenRecruitmentAttributionConfirmations" (
                    "Id", "TenantId", "AttributionId", "ConfirmedAt",
                    "ConfirmedByUserId", "ConfirmationMethod",
                    "EvidenceReferenceId", "RulesVersion")
                VALUES (
                    @id, @tenantId, @attributionId, @confirmedAt,
                    @confirmedByUserId, @method, @evidenceReferenceId, @rulesVersion);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("tenantId", tenantId);
            command.Parameters.AddWithValue("attributionId", attributionId);
            command.Parameters.AddWithValue(
                "confirmedAt",
                confirmedAt ?? AttributedAt.AddMinutes(1));
            command.Parameters.AddWithValue(
                "confirmedByUserId",
                confirmedByUserId.HasValue
                    ? confirmedByUserId.Value
                    : DBNull.Value);
            command.Parameters.AddWithValue("method", method);
            command.Parameters.AddWithValue(
                "evidenceReferenceId",
                evidenceReferenceId ?? Guid.NewGuid());
            command.Parameters.AddWithValue("rulesVersion", rulesVersion);
            await command.ExecuteNonQueryAsync();
            return id;
        }

        private static async Task ExecuteAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<long> ScalarAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        private static AQGreenRecruitmentAttribution MemberAttribution(
            Guid participantId,
            Guid sponsorId) =>
            AQGreenRecruitmentAttribution.Create(
                1,
                participantId,
                sponsorId,
                AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                AQGreenAcquisitionSource.MemberInvitation,
                sponsorId,
                AttributedAt,
                null,
                null,
                AQGreenRecruitmentAttributionRules.CurrentVersion);

        private static AQGreenRecruitmentAttributionConfirmation Confirmation(
            AQGreenRecruitmentAttribution attribution) =>
            AQGreenRecruitmentAttributionConfirmation.Confirm(
                attribution,
                AttributedAt.AddMinutes(1),
                null,
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                Guid.NewGuid(),
                AQGreenRecruitmentAttributionRules.CurrentVersion);

        private static Guid P(int number, int tenantId = 1) =>
            AQGreenPlacementTopologyPostgreSqlFixture.Participant(tenantId, number);
    }
}
