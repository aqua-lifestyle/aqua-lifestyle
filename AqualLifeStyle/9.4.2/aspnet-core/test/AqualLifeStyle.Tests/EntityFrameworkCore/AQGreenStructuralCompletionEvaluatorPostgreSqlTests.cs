using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementTopologyPostgreSqlCollection.Name)]
    public sealed class AQGreenStructuralCompletionEvaluatorPostgreSqlTests
    {
        private static readonly DateTime PlacedAt =
            new(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Cutoff =
            new(2026, 8, 27, 8, 0, 0, DateTimeKind.Utc);
        private readonly AQGreenPlacementTopologyPostgreSqlFixture _fixture;

        public AQGreenStructuralCompletionEvaluatorPostgreSqlTests(
            AQGreenPlacementTopologyPostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task RootOnly_IsLevelZero_AndMissingPlacementIsExplicit()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));

                var result = await evaluator.EvaluateAsync(1, P(1), Cutoff);

                result.ParticipantId.ShouldBe(P(1));
                result.PlacementTreeScopeId.ShouldBe(topology.ScopeId);
                result.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level0);
                result.QualifyingDepth1Count.ShouldBe(0);
                result.QualifyingDepth2Count.ShouldBe(0);
                result.QualifyingDepth3Count.ShouldBe(0);
                result.RulesVersion.ShouldBe(AQGreenPlacementRules.CurrentVersion);
                await Should.ThrowAsync<AQGreenStructuralEvaluationNotPlacedException>(() =>
                    evaluator.EvaluateAsync(1, P(200), Cutoff));
            });
        }

        [Fact]
        public async Task CutoffExcludesLaterPlacements_ThenIncludesThem()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                var laterPlacement = Cutoff.AddHours(1);
                for (var slot = 1; slot <= 5; slot++)
                {
                    await topology.AddChildAsync(
                        P(1),
                        P(slot + 1),
                        slot,
                        laterPlacement);
                }

                var before = await evaluator.EvaluateAsync(1, P(1), Cutoff);
                before.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level0);
                before.QualifyingDepth1Count.ShouldBe(0);
                var after = await evaluator.EvaluateAsync(
                    1,
                    P(1),
                    laterPlacement.AddMinutes(1));
                after.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level1);
                after.QualifyingDepth1Count.ShouldBe(5);
            });
        }

        [Fact]
        public async Task CompleteThreeGenerations_IsLevelThree_AndDeeperNodesDoNotAddALevel()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                await topology.AddCompleteGenerationsAsync(P(1), 3, 2);
                await topology.AddChildAsync(P(32), P(157), 1);

                var result = await evaluator.EvaluateAsync(1, P(1), Cutoff);

                result.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level3);
                result.QualifyingDepth1Count.ShouldBe(5);
                result.QualifyingDepth2Count.ShouldBe(25);
                result.QualifyingDepth3Count.ShouldBe(125);
            });
        }

        [Fact]
        public async Task IncompleteDepthTwo_RemainsLevelOne()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                var depthOne = await topology.AddGenerationAsync(
                    new[] { P(1) },
                    firstParticipantNumber: 2,
                    childCount: 5);
                await topology.AddGenerationAsync(
                    depthOne,
                    firstParticipantNumber: 7,
                    childCount: 24);

                var result = await evaluator.EvaluateAsync(1, P(1), Cutoff);
                result.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level1);
                result.QualifyingDepth1Count.ShouldBe(5);
                result.QualifyingDepth2Count.ShouldBe(24);
            });
        }

        [Fact]
        public async Task IncompleteDepthThree_RemainsLevelTwo()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                var depthOne = await topology.AddGenerationAsync(
                    new[] { P(1) },
                    firstParticipantNumber: 2,
                    childCount: 5);
                var depthTwo = await topology.AddGenerationAsync(
                    depthOne,
                    firstParticipantNumber: 7,
                    childCount: 25);
                await topology.AddGenerationAsync(
                    depthTwo,
                    firstParticipantNumber: 32,
                    childCount: 124);

                var result = await evaluator.EvaluateAsync(1, P(1), Cutoff);
                result.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level2);
                result.QualifyingDepth1Count.ShouldBe(5);
                result.QualifyingDepth2Count.ShouldBe(25);
                result.QualifyingDepth3Count.ShouldBe(124);
            });
        }

        [Fact]
        public async Task DeeperOccupancyCannotCompensateForIncompleteDepthOne()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                for (var slot = 1; slot <= 4; slot++)
                {
                    await topology.AddChildAsync(P(1), P(slot + 1), slot);
                }
                for (var slot = 1; slot <= 5; slot++)
                {
                    await topology.AddChildAsync(P(2), P(slot + 6), slot);
                    await topology.AddChildAsync(P(7), P(slot + 11), slot);
                }

                var root = await evaluator.EvaluateAsync(1, P(1), Cutoff);
                root.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level0);
                root.QualifyingDepth1Count.ShouldBe(4);
                root.QualifyingDepth2Count.ShouldBe(5);
                root.QualifyingDepth3Count.ShouldBe(5);
                (await evaluator.EvaluateAsync(1, P(2), Cutoff))
                    .StructuralCompletionLevel.ShouldBe(
                        AQGreenStructuralCompletionLevel.Level1);
            });
        }

        [Fact]
        public async Task PlacementSpilloverCountsRelatively_WithoutFollowingCreditedSponsorOrArea()
        {
            await InTransactionAsync(async (connection, transaction, evaluator, context) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                await topology.AddCompleteGenerationsAsync(P(1), 2, 2);
                await AddSponsoredAttributionAsync(
                    connection,
                    transaction,
                    participantId: P(7),
                    creditedSponsorParticipantId: P(1));

                var placementParent = await context.EntryParticipations
                    .AsNoTracking()
                    .SingleAsync(row => row.Id == P(2, 1));
                var spillover = await context.EntryParticipations
                    .AsNoTracking()
                    .SingleAsync(row => row.Id == P(7, 1));
                placementParent.RecruiterCustomerId.ShouldBeNull();
                spillover.RecruiterCustomerId.ShouldBeNull();
                var areas = await context.Customers
                    .AsNoTracking()
                    .Where(customer =>
                        customer.Id == placementParent.CustomerId ||
                        customer.Id == spillover.CustomerId)
                    .Select(customer => customer.AreaId)
                    .ToListAsync();
                areas.Distinct().Count().ShouldBe(2);

                var duePolicy = EntryMonthlyObligationDuePolicy.Create(
                    "structural-test-policy",
                    1,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(
                        2026,
                        8));
                context.EntryMonthlyObligationDuePolicies.Add(duePolicy);
                await context.SaveChangesAsync();
                var overdueObligation = EntryMonthlyObligation.Create(
                    placementParent,
                    2026,
                    8,
                    Cutoff.AddDays(-20),
                    "structural-test-policy");
                overdueObligation.AssessStatus(Cutoff);
                overdueObligation.IsOwnPayoutEligible.ShouldBeFalse();
                context.EntryMonthlyObligations.Add(overdueObligation);
                await context.SaveChangesAsync();

                var relative = await evaluator.EvaluateAsync(1, P(2), Cutoff);
                relative.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level1);
                relative.QualifyingDepth1Count.ShouldBe(5);
                var root = await evaluator.EvaluateAsync(1, P(1), Cutoff);
                root.StructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level2);
                root.QualifyingDepth1Count.ShouldBe(5);
                root.QualifyingDepth2Count.ShouldBe(25);
            });
        }

        [Fact]
        public async Task OtherScopeAndTenantCannotLeakIntoEvaluation()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                await CreateTopologyAsync(connection, transaction, 1, P(1));
                var otherScope = await CreateTopologyAsync(
                    connection,
                    transaction,
                    1,
                    P(20));
                await otherScope.AddCompleteGenerationsAsync(P(20), 1, 21);
                var otherTenant = await CreateTopologyAsync(
                    connection,
                    transaction,
                    2,
                    P(1, 2));
                await otherTenant.AddCompleteGenerationsAsync(P(1, 2), 1, 2);

                (await evaluator.EvaluateAsync(1, P(1), Cutoff))
                    .StructuralCompletionLevel.ShouldBe(
                        AQGreenStructuralCompletionLevel.Level0);
                (await evaluator.EvaluateAsync(2, P(1, 2), Cutoff))
                    .StructuralCompletionLevel.ShouldBe(
                        AQGreenStructuralCompletionLevel.Level1);
            });
        }

        [Fact]
        public async Task CorruptTopologyPropagatesIntegrityFailure()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await ExecuteAsync(
                    connection,
                    transaction,
                    "ALTER TABLE public.\"AQGreenNetworkPlacements\" DISABLE TRIGGER ALL;");
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "CanonicalPath" = '5'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("participantId", P(2)));
                await ExecuteAsync(
                    connection,
                    transaction,
                    "ALTER TABLE public.\"AQGreenNetworkPlacements\" ENABLE TRIGGER ALL;");

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    evaluator.EvaluateAsync(1, P(1), Cutoff));
            });
        }

        [Theory]
        [InlineData("participation-deleted-after-cutoff")]
        [InlineData("customer-inactive")]
        [InlineData("user-disabled")]
        public async Task CurrentUnresolvedD08StateFailsClosedAtRequestedCutoff(
            string state)
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await ApplyLifecycleStateAsync(connection, transaction, state, P(2));

                var exception = await Should.ThrowAsync<
                    AQGreenStructuralContributionPolicyRequiredException>(() =>
                    evaluator.EvaluateAsync(1, P(1), Cutoff));
                exception.ParticipantId.ShouldBe(P(2));
                exception.Message.ShouldContain("AQG-V2-D08");
            });
        }

        [Fact]
        public async Task NonActivePlacedParticipationIsIntegrityFailure_NotLevelZero()
        {
            await InTransactionAsync(async (connection, transaction, evaluator) =>
            {
                var topology = await CreateTopologyAsync(connection, transaction, 1, P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."EntryParticipations"
                    SET "Status" = @status
                    WHERE "Id" = @participantId;
                    """,
                    new NpgsqlParameter("status", (int)EntryParticipationStatus.Rejected),
                    new NpgsqlParameter("participantId", P(2)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    evaluator.EvaluateAsync(1, P(1), Cutoff));
            });
        }

        private async Task InTransactionAsync(
            Func<NpgsqlConnection, NpgsqlTransaction,
                AQGreenStructuralCompletionEvaluator, Task> action) =>
            await InTransactionAsync((connection, transaction, evaluator, _) =>
                action(connection, transaction, evaluator));

        private async Task InTransactionAsync(
            Func<NpgsqlConnection, NpgsqlTransaction,
                AQGreenStructuralCompletionEvaluator, AqualLifeStyleDbContext, Task> action)
        {
            await using var connection = await _fixture.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await context.Database.UseTransactionAsync(transaction);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            var evaluator = new AQGreenStructuralCompletionEvaluator(
                provider,
                new AQGreenPlacementTopologyReader(provider));

            await action(connection, transaction, evaluator, context);
            await transaction.RollbackAsync();
        }

        private static async Task<TestTopologyBuilder> CreateTopologyAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int tenantId,
            Guid rootParticipantId)
        {
            var topology = new TestTopologyBuilder(
                connection,
                transaction,
                tenantId,
                Guid.NewGuid());
            await topology.AddRootAsync(rootParticipantId);
            return topology;
        }

        private static async Task AddSponsoredAttributionAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid participantId,
            Guid creditedSponsorParticipantId)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO public."AQGreenRecruitmentAttributions" (
                    "Id", "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                    "AttributionKind", "AcquisitionSource", "SourceReferenceId",
                    "AttributedAt", "AttributedByUserId", "AssignmentReason", "RulesVersion")
                VALUES (
                    @id, 1, @participantId, @creditedSponsorParticipantId,
                    1, 1, @creditedSponsorParticipantId,
                    TIMESTAMPTZ '2026-08-26 07:00:00+00', NULL, NULL,
                    'AQGreenRecruitmentAttributionV1');
                """,
                new NpgsqlParameter("id", Guid.NewGuid()),
                new NpgsqlParameter("participantId", participantId),
                new NpgsqlParameter(
                    "creditedSponsorParticipantId",
                    creditedSponsorParticipantId));
        }

        private static Task ApplyLifecycleStateAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string state,
            Guid participantId)
        {
            var sql = state switch
            {
                "participation-deleted-after-cutoff" =>
                    """
                    UPDATE public."EntryParticipations"
                    SET "IsDeleted" = TRUE,
                        "DeletionTime" = TIMESTAMPTZ '2026-08-28 08:00:00+00'
                    WHERE "Id" = @participantId;
                    """,
                "customer-inactive" =>
                    """
                    UPDATE public."Customers" customer
                    SET "IsActive" = FALSE
                    FROM public."EntryParticipations" participation
                    WHERE participation."Id" = @participantId
                      AND customer."Id" = participation."CustomerId";
                    """,
                "user-disabled" =>
                    """
                    UPDATE public."AbpUsers" app_user
                    SET "IsActive" = FALSE
                    FROM public."Customers" customer
                    JOIN public."EntryParticipations" participation
                      ON participation."CustomerId" = customer."Id"
                    WHERE participation."Id" = @participantId
                      AND app_user."Id" = customer."UserId";
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(state))
            };
            return ExecuteAsync(
                connection,
                transaction,
                sql,
                new NpgsqlParameter("participantId", participantId));
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

        private static Guid P(int number, int tenantId = 1) =>
            AQGreenPlacementTopologyPostgreSqlFixture.Participant(tenantId, number);

        private sealed class TestTopologyBuilder
        {
            private readonly NpgsqlConnection _connection;
            private readonly NpgsqlTransaction _transaction;
            private readonly int _tenantId;
            private readonly Dictionary<Guid, string> _canonicalPaths = new();

            public TestTopologyBuilder(
                NpgsqlConnection connection,
                NpgsqlTransaction transaction,
                int tenantId,
                Guid scopeId)
            {
                _connection = connection;
                _transaction = transaction;
                _tenantId = tenantId;
                ScopeId = scopeId;
            }

            public Guid ScopeId { get; }

            public async Task AddRootAsync(Guid participantId)
            {
                await ExecuteAsync(
                    _connection,
                    _transaction,
                    """
                    INSERT INTO public."AQGreenPlacementTreeScopes" ("Id", "TenantId")
                    VALUES (@scopeId, @tenantId);
                    """,
                    new NpgsqlParameter("scopeId", ScopeId),
                    new NpgsqlParameter("tenantId", _tenantId));
                await AddPlacementAsync(
                    participantId,
                    null,
                    null,
                    string.Empty,
                    PlacedAt);
                _canonicalPaths.Add(participantId, string.Empty);
            }

            public async Task AddChildAsync(
                Guid parentParticipantId,
                Guid participantId,
                int slot,
                DateTime? placedAt = null)
            {
                var canonicalPath = _canonicalPaths[parentParticipantId] + slot;
                await AddPlacementAsync(
                    participantId,
                    parentParticipantId,
                    slot,
                    canonicalPath,
                    placedAt ?? PlacedAt);
                _canonicalPaths.Add(participantId, canonicalPath);
            }

            public async Task AddCompleteGenerationsAsync(
                Guid rootParticipantId,
                int maximumDepth,
                int firstParticipantNumber)
            {
                IReadOnlyList<Guid> currentGeneration =
                    new List<Guid> { rootParticipantId };
                var nextParticipantNumber = firstParticipantNumber;
                for (var depth = 1; depth <= maximumDepth; depth++)
                {
                    var childCount = currentGeneration.Count *
                        AQGreenStructuralCompletionCalculator.BranchSize;
                    currentGeneration = await AddGenerationAsync(
                        currentGeneration,
                        nextParticipantNumber,
                        childCount);
                    nextParticipantNumber += childCount;
                }
            }

            public async Task<IReadOnlyList<Guid>> AddGenerationAsync(
                IReadOnlyList<Guid> parentParticipantIds,
                int firstParticipantNumber,
                int childCount)
            {
                if (parentParticipantIds == null || parentParticipantIds.Count == 0)
                    throw new ArgumentException(
                        "At least one placement parent is required.",
                        nameof(parentParticipantIds));
                if (childCount < 0 ||
                    childCount > parentParticipantIds.Count *
                    AQGreenStructuralCompletionCalculator.BranchSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(childCount));
                }

                var children = new List<Guid>();
                for (var index = 0; index < childCount; index++)
                {
                    var parentIndex = index /
                        AQGreenStructuralCompletionCalculator.BranchSize;
                    var slot = index %
                        AQGreenStructuralCompletionCalculator.BranchSize + 1;
                    var participantId = P(firstParticipantNumber + index, _tenantId);
                    await AddChildAsync(
                        parentParticipantIds[parentIndex],
                        participantId,
                        slot);
                    children.Add(participantId);
                }

                return children;
            }

            private Task AddPlacementAsync(
                Guid participantId,
                Guid? parentParticipantId,
                int? slot,
                string canonicalPath,
                DateTime placedAt) =>
                ExecuteAsync(
                    _connection,
                    _transaction,
                    """
                    INSERT INTO public."AQGreenNetworkPlacements" (
                        "Id", "TenantId", "PlacementTreeScopeId", "ParticipantId",
                        "PlacementParentParticipantId", "PlacementSlot", "CanonicalPath",
                        "PlacedAt", "RulesVersion")
                    VALUES (
                        @id, @tenantId, @scopeId, @participantId,
                        @parentParticipantId, @slot, @canonicalPath,
                        @placedAt, @rulesVersion);
                    """,
                    new NpgsqlParameter("id", Guid.NewGuid()),
                    new NpgsqlParameter("tenantId", _tenantId),
                    new NpgsqlParameter("scopeId", ScopeId),
                    new NpgsqlParameter("participantId", participantId),
                    new NpgsqlParameter(
                        "parentParticipantId",
                        parentParticipantId.HasValue
                            ? parentParticipantId.Value
                            : DBNull.Value),
                    new NpgsqlParameter(
                        "slot",
                        slot.HasValue ? slot.Value : DBNull.Value),
                    new NpgsqlParameter("canonicalPath", canonicalPath),
                    new NpgsqlParameter("placedAt", placedAt),
                    new NpgsqlParameter(
                        "rulesVersion",
                        AQGreenPlacementRules.CurrentVersion));
        }
    }

    public sealed class AQGreenStructuralCompletionEvaluatorDependencyInjectionTests
        : AqualLifeStyleTestBase
    {
        [Fact]
        public void Evaluator_IsResolvableThroughAbpConventionRegistration()
        {
            Resolve<IAQGreenStructuralCompletionEvaluator>()
                .ShouldBeOfType<AQGreenStructuralCompletionEvaluator>();
        }
    }
}
