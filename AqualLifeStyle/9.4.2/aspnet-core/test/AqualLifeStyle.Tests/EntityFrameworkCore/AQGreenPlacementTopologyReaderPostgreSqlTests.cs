using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abp.Domain.Entities;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
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
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AQGreenPlacementTopologyPostgreSqlCollection
        : ICollectionFixture<AQGreenPlacementTopologyPostgreSqlFixture>
    {
        public const string Name = "AQGreen placement topology PostgreSQL";
    }

    [Collection(AQGreenPlacementTopologyPostgreSqlCollection.Name)]
    public sealed class AQGreenPlacementTopologyReaderPostgreSqlTests
    {
        private readonly AQGreenPlacementTopologyPostgreSqlFixture _fixture;

        public AQGreenPlacementTopologyReaderPostgreSqlTests(
            AQGreenPlacementTopologyPostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetPlacement_ResolvesOnlyTopologyFacts()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 3);

                var placement = await reader.GetPlacementAsync(1, scopeId, P(2));

                placement.ParticipantId.ShouldBe(P(2));
                placement.PlacementTreeScopeId.ShouldBe(scopeId);
                placement.PlacementParentParticipantId.ShouldBe(P(1));
                placement.PlacementSlot.ShouldBe(3);
                placement.RelativeDepth.ShouldBe(0);
                typeof(AQGreenPlacementTopologyNode).GetProperties()
                    .Select(property => property.Name)
                    .ShouldBe(new[]
                    {
                        nameof(AQGreenPlacementTopologyNode.ParticipantId),
                        nameof(AQGreenPlacementTopologyNode.PlacementTreeScopeId),
                        nameof(AQGreenPlacementTopologyNode.PlacementParentParticipantId),
                        nameof(AQGreenPlacementTopologyNode.PlacementSlot),
                        nameof(AQGreenPlacementTopologyNode.RelativeDepth)
                    });
            });
        }

        [Fact]
        public async Task GetPlacement_EnforcesTenantAndScopeIsolation()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                (await reader.GetPlacementAsync(1, scopeId, P(2)))
                    .ParticipantId.ShouldBe(P(2));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetPlacementAsync(2, scopeId, P(2)));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetPlacementAsync(1, Guid.NewGuid(), P(2)));
            });
        }

        [Fact]
        public async Task Subtree_RootOnly_ReturnsRootAtDepthZero()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));

                var placement = await reader.GetPlacementAsync(1, scopeId, P(1));
                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                placement.PlacementParentParticipantId.ShouldBeNull();
                placement.PlacementSlot.ShouldBeNull();
                AssertSequence(nodes, new[] { P(1) }, new[] { 0 });
            });
        }

        [Fact]
        public async Task Children_FiveOccupiedSlots_ReturnsSlotOrder()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(6), 5);
                await topology.AddChildAsync(P(1), P(3), 2);
                await topology.AddChildAsync(P(1), P(5), 4);
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(1), P(4), 3);

                var children = await reader.GetChildrenAsync(1, scopeId, P(1));

                children.Select(node => node.ParticipantId)
                    .ShouldBe(new[] { P(2), P(3), P(4), P(5), P(6) });
                children.Select(node => node.PlacementSlot)
                    .ShouldBe(new int?[] { 1, 2, 3, 4, 5 });
                children.All(node => node.RelativeDepth == 1).ShouldBeTrue();
            });
        }

        [Fact]
        public async Task Children_SparseSlots_ReturnsOnlyPersistedChildrenInSlotOrder()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(6), 5);
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(1), P(4), 3);

                var children = await reader.GetChildrenAsync(1, scopeId, P(1));

                children.Select(node => node.ParticipantId)
                    .ShouldBe(new[] { P(2), P(4), P(6) });
                children.Select(node => node.PlacementSlot)
                    .ShouldBe(new int?[] { 1, 3, 5 });
            });
        }

        [Fact]
        public async Task GetChildren_EnforcesTenantAndScopeIsolation()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                (await reader.GetChildrenAsync(1, scopeId, P(1)))
                    .Select(node => node.ParticipantId)
                    .ShouldBe(new[] { P(2) });
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetChildrenAsync(2, scopeId, P(1)));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetChildrenAsync(1, Guid.NewGuid(), P(1)));
            });
        }

        [Fact]
        public async Task Subtree_MultipleGenerations_ReturnsBreadthFirstNotDepthFirst()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(1), P(3), 2);
                await topology.AddChildAsync(P(2), P(4), 1);
                await topology.AddChildAsync(P(3), P(5), 1);
                await topology.AddChildAsync(P(4), P(6), 1);

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                AssertSequence(
                    nodes,
                    new[] { P(1), P(2), P(3), P(4), P(5), P(6) },
                    new[] { 0, 1, 1, 2, 2, 3 });
            });
        }

        [Fact]
        public async Task Subtree_SameDepth_ReturnsParentMajorCanonicalOrder()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1); // A
                await topology.AddChildAsync(P(1), P(3), 2); // B
                await topology.AddChildAsync(P(1), P(4), 3); // C
                await topology.AddChildAsync(P(3), P(8), 1); // I, inserted first
                await topology.AddChildAsync(P(2), P(5), 1); // F
                await topology.AddChildAsync(P(2), P(6), 2); // G
                await topology.AddChildAsync(P(2), P(7), 3); // H
                await topology.AddChildAsync(P(6), P(9), 1); // K

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                AssertSequence(
                    nodes,
                    new[]
                    {
                        P(1), P(2), P(3), P(4),
                        P(5), P(6), P(7), P(8),
                        P(9)
                    },
                    new[] { 0, 1, 1, 1, 2, 2, 2, 2, 3 });
            });
        }

        [Fact]
        public async Task Subtree_FromNonRootSponsor_ReturnsOnlySponsorLocalNodes()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1)); // X
                await topology.AddChildAsync(P(1), P(2), 1); // A
                await topology.AddChildAsync(P(1), P(3), 2); // B
                await topology.AddChildAsync(P(2), P(4), 1); // F
                await topology.AddChildAsync(P(2), P(5), 2); // G
                await topology.AddChildAsync(P(4), P(6), 1); // K

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(2));

                AssertSequence(
                    nodes,
                    new[] { P(2), P(4), P(5), P(6) },
                    new[] { 0, 1, 1, 2 });
                nodes.ShouldNotContain(node => node.ParticipantId == P(1));
                nodes.ShouldNotContain(node => node.ParticipantId == P(3));
            });
        }

        [Fact]
        public async Task Subtree_FromNonRootSponsor_ResetsRelativeDepth()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(2), P(3), 1);
                await topology.AddChildAsync(P(3), P(4), 1);

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(2));

                AssertSequence(
                    nodes,
                    new[] { P(2), P(3), P(4) },
                    new[] { 0, 1, 2 });
            });
        }

        [Fact]
        public async Task Subtree_DeepSparseBranch_ReturnsEveryRelativeDepth()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));

                for (var number = 2; number <= 25; number++)
                {
                    await topology.AddChildAsync(
                        P(number - 1),
                        P(number),
                        (number * 3) % 5 + 1);
                }

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                nodes.Select(node => node.ParticipantId)
                    .ShouldBe(Enumerable.Range(1, 25).Select(P));
                nodes.Select(node => node.RelativeDepth)
                    .ShouldBe(Enumerable.Range(0, 25));
            });
        }

        [Fact]
        public async Task Reads_SameTenantScopeCrossAreaDescendant_RemainsVisible()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                var rootArea = await AreaIdAsync(connection, transaction, 1, P(1));
                var childArea = await AreaIdAsync(connection, transaction, 1, P(2));
                rootArea.ShouldNotBe(childArea);

                var placement = await reader.GetPlacementAsync(1, scopeId, P(2));
                var children = await reader.GetChildrenAsync(1, scopeId, P(1));
                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                placement.ParticipantId.ShouldBe(P(2));
                children.Select(node => node.ParticipantId).ShouldBe(new[] { P(2) });
                nodes.Select(node => node.ParticipantId)
                    .ShouldBe(new[] { P(1), P(2) });
            });
        }

        [Fact]
        public async Task Subtree_DifferentPlacementTreeScope_IsExcluded()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var firstScopeId = Guid.NewGuid();
                var first = new TopologyBuilder(connection, transaction, 1, firstScopeId);
                await first.AddRootAsync(P(1));
                await first.AddChildAsync(P(1), P(2), 1);

                var secondScopeId = Guid.NewGuid();
                var second = new TopologyBuilder(connection, transaction, 1, secondScopeId);
                await second.AddRootAsync(P(10));
                await second.AddChildAsync(P(10), P(11), 1);

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, firstScopeId, P(1));

                nodes.Select(node => node.ParticipantId)
                    .ShouldBe(new[] { P(1), P(2) });
                nodes.ShouldNotContain(node => node.PlacementTreeScopeId == secondScopeId);
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(1, secondScopeId, P(1)));
            });
        }

        [Fact]
        public async Task Subtree_DifferentTenant_IsExcluded()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var tenantOneScopeId = Guid.NewGuid();
                var tenantOne = new TopologyBuilder(
                    connection,
                    transaction,
                    1,
                    tenantOneScopeId);
                await tenantOne.AddRootAsync(P(1));
                await tenantOne.AddChildAsync(P(1), P(2), 1);

                var tenantTwoScopeId = Guid.NewGuid();
                var tenantTwo = new TopologyBuilder(
                    connection,
                    transaction,
                    2,
                    tenantTwoScopeId);
                await tenantTwo.AddRootAsync(P2(1));
                await tenantTwo.AddChildAsync(P2(1), P2(2), 1);

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(
                    1,
                    tenantOneScopeId,
                    P(1));

                nodes.Select(node => node.ParticipantId)
                    .ShouldBe(new[] { P(1), P(2) });
                nodes.ShouldNotContain(node => node.ParticipantId == P2(1));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(
                        2,
                        tenantOneScopeId,
                        P(1)));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(1, tenantTwoScopeId, P2(1)));
            });
        }

        [Fact]
        public async Task MissingPlacement_FailsExplicitlyForEveryReadUseCase()
        {
            await InTransactionAsync(async (_, _, reader) =>
            {
                var missingParticipant = P(48);

                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetPlacementAsync(1, Guid.NewGuid(), missingParticipant));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetChildrenAsync(1, Guid.NewGuid(), missingParticipant));
                await Should.ThrowAsync<EntityNotFoundException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(
                        1,
                        Guid.NewGuid(),
                        missingParticipant));
            });
        }

        [Fact]
        public async Task Subtree_ValidLeafSponsor_ReturnsExactlyItself()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(2));

                AssertSequence(nodes, new[] { P(2) }, new[] { 0 });
                (await reader.GetChildrenAsync(1, scopeId, P(2))).ShouldBeEmpty();
            });
        }

        [Fact]
        public async Task Subtree_RepeatedRead_ReturnsIdenticalOrderedFacts()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(3), 2);
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(2), P(5), 3);
                await topology.AddChildAsync(P(2), P(4), 1);

                var first = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));
                var second = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                Project(first).ShouldBe(Project(second));
            });
        }

        [Fact]
        public async Task Subtree_ExecutesExplicitCCollationAndOrdersCanonicalPaths()
        {
            var logs = new List<string>();
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1); // 1
                await topology.AddChildAsync(P(1), P(3), 2); // 2
                await topology.AddChildAsync(P(2), P(4), 1); // 11
                await topology.AddChildAsync(P(2), P(5), 2); // 12
                await topology.AddChildAsync(P(3), P(6), 1); // 21

                var nodes = await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                AssertSequence(
                    nodes,
                    new[] { P(1), P(2), P(3), P(4), P(5), P(6) },
                    new[] { 0, 1, 1, 2, 2, 2 });
            }, logs.Add);

            logs.Any(message => message.Contains("COLLATE \"C\"", StringComparison.Ordinal))
                .ShouldBeTrue("the executed traversal must not rely on database-default collation");
        }

        [Fact]
        public async Task Reads_DoNotChangeOrTrackPersistedTopology()
        {
            await InTransactionAsync(async (connection, transaction, reader, context) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(2), P(3), 1);
                var before = await TopologyFingerprintAsync(connection, transaction, scopeId);

                await reader.GetPlacementAsync(1, scopeId, P(2));
                await reader.GetChildrenAsync(1, scopeId, P(1));
                await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1));

                var after = await TopologyFingerprintAsync(connection, transaction, scopeId);
                after.ShouldBe(before);
                context.ChangeTracker.Entries().ShouldBeEmpty();
            });
        }

        [Fact]
        public async Task GetPlacement_CorruptAnchorPath_FailsVisibly()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "CanonicalPath" = '5'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("participantId", P(2)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetPlacementAsync(1, scopeId, P(2)));
            });
        }

        [Fact]
        public async Task GetPlacement_CorruptTimestampAncestry_FailsVisibly()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "PlacedAt" = TIMESTAMPTZ '2026-08-25 09:59:59+00'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("participantId", P(2)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetPlacementAsync(1, scopeId, P(2)));
            });
        }

        [Fact]
        public async Task GetPlacement_AncestryCycle_FailsVisibly()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(2), P(3), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "PlacementParentParticipantId" = @parentParticipantId,
                        "PlacementSlot" = 1,
                        "CanonicalPath" = '111'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("parentParticipantId", P(3)),
                    new NpgsqlParameter("participantId", P(1)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetPlacementAsync(1, scopeId, P(1)));
            });
        }

        [Fact]
        public async Task GetChildren_CorruptAnchorAncestry_FailsVisibly()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(2), P(3), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "CanonicalPath" = CASE
                        WHEN "ParticipantId" = @anchorParticipantId THEN '2'
                        ELSE '21'
                    END
                    WHERE "TenantId" = 1
                      AND "ParticipantId" IN (@anchorParticipantId, @childParticipantId);
                    """,
                    new NpgsqlParameter("anchorParticipantId", P(2)),
                    new NpgsqlParameter("childParticipantId", P(3)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetChildrenAsync(1, scopeId, P(2)));
            });
        }

        [Fact]
        public async Task GetChildren_CorruptDirectChildEdge_FailsVisibly()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "PlacedAt" = TIMESTAMPTZ '2026-08-25 09:59:59+00'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("participantId", P(2)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetChildrenAsync(1, scopeId, P(1)));
            });
        }

        [Fact]
        public async Task NullSlotChild_FailsChildrenAndSubtreeInsteadOfLookingAbsent()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                await ExecuteAsync(
                    connection,
                    transaction,
                    "ALTER TABLE public.\"AQGreenNetworkPlacements\" DISABLE TRIGGER ALL;");
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    ALTER TABLE public."AQGreenNetworkPlacements"
                    DROP CONSTRAINT "CK_AQGreenNetworkPlacements_RootOrNonRootShape";
                    """);
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "PlacementSlot" = NULL
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("participantId", P(2)));
                await ExecuteAsync(
                    connection,
                    transaction,
                    "ALTER TABLE public.\"AQGreenNetworkPlacements\" ENABLE TRIGGER ALL;");

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetChildrenAsync(1, scopeId, P(1)));
                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1)));
            });
        }

        [Fact]
        public async Task Subtree_DescendantCycle_FailsInsteadOfReturningTruncatedTree()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);
                await topology.AddChildAsync(P(2), P(3), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "PlacementParentParticipantId" = @parentParticipantId,
                        "PlacementSlot" = 1,
                        "CanonicalPath" = '111'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("parentParticipantId", P(3)),
                    new NpgsqlParameter("participantId", P(1)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1)));
            });
        }

        [Fact]
        public async Task OrphanedNonRoot_FailsVisiblyForEveryReadUseCase()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "PlacementParentParticipantId" = @missingParentParticipantId
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("missingParentParticipantId", P(48)),
                    new NpgsqlParameter("participantId", P(2)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetPlacementAsync(1, scopeId, P(2)));
                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetChildrenAsync(1, scopeId, P(2)));
                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(2)));
            });
        }

        [Fact]
        public async Task Subtree_CorruptDescendantCanonicalPath_FailsVisibly()
        {
            await InTransactionAsync(async (connection, transaction, reader) =>
            {
                var scopeId = Guid.NewGuid();
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                await topology.AddChildAsync(P(1), P(2), 1);

                await CorruptAsync(
                    connection,
                    transaction,
                    """
                    UPDATE public."AQGreenNetworkPlacements"
                    SET "CanonicalPath" = '5'
                    WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                    """,
                    new NpgsqlParameter("participantId", P(2)));

                await Should.ThrowAsync<AQGreenPlacementTopologyIntegrityException>(() =>
                    reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1)));
            });
        }

        [Fact]
        public async Task TransactionRollback_LeavesNoImmutableTopologyPollution()
        {
            var scopeId = Guid.NewGuid();
            await using (var connection = await _fixture.OpenConnectionAsync())
            await using (var transaction = await connection.BeginTransactionAsync())
            await using (var context = _fixture.CreateDbContext(connection))
            {
                await context.Database.UseTransactionAsync(transaction);
                var topology = new TopologyBuilder(connection, transaction, 1, scopeId);
                await topology.AddRootAsync(P(1));
                var reader = CreateReader(context);

                (await reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, P(1)))
                    .Count.ShouldBe(1);
                await transaction.RollbackAsync();
            }

            (await _fixture.ScopePlacementCountAsync(scopeId)).ShouldBe(0);
            (await _fixture.ScopeCountAsync(scopeId)).ShouldBe(0);
        }

        private Task InTransactionAsync(
            Func<NpgsqlConnection, NpgsqlTransaction, AQGreenPlacementTopologyReader, Task> action,
            Action<string> logger = null) =>
            InTransactionAsync(
                (connection, transaction, reader, _) =>
                    action(connection, transaction, reader),
                logger);

        private async Task InTransactionAsync(
            Func<NpgsqlConnection, NpgsqlTransaction, AQGreenPlacementTopologyReader,
                AqualLifeStyleDbContext, Task> action,
            Action<string> logger = null)
        {
            await using var connection = await _fixture.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var context = _fixture.CreateDbContext(connection, logger);
            await context.Database.UseTransactionAsync(transaction);
            var reader = CreateReader(context);

            try
            {
                await action(connection, transaction, reader, context);
            }
            finally
            {
                if (transaction.Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        private static AQGreenPlacementTopologyReader CreateReader(
            AqualLifeStyleDbContext context)
        {
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            return new AQGreenPlacementTopologyReader(provider);
        }

        private static void AssertSequence(
            IReadOnlyList<AQGreenPlacementTopologyNode> actual,
            IEnumerable<Guid> participantIds,
            IEnumerable<int> relativeDepths)
        {
            actual.Select(node => node.ParticipantId).ShouldBe(participantIds);
            actual.Select(node => node.RelativeDepth).ShouldBe(relativeDepths);
        }

        private static IEnumerable<string> Project(
            IEnumerable<AQGreenPlacementTopologyNode> nodes) =>
            nodes.Select(node =>
                $"{node.ParticipantId:N}|{node.PlacementTreeScopeId:N}|" +
                $"{node.PlacementParentParticipantId:N}|{node.PlacementSlot}|" +
                $"{node.RelativeDepth}");

        private static async Task<Guid> AreaIdAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int tenantId,
            Guid participantId)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT c."AreaId"
                FROM public."EntryParticipations" ep
                JOIN public."Customers" c
                  ON c."TenantId" = ep."TenantId" AND c."Id" = ep."CustomerId"
                WHERE ep."TenantId" = @tenantId AND ep."Id" = @participantId;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("tenantId", tenantId);
            command.Parameters.AddWithValue("participantId", participantId);
            return (Guid)await command.ExecuteScalarAsync();
        }

        private static async Task<string> TopologyFingerprintAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid scopeId)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT md5(COALESCE(string_agg(
                    "Id"::text || '|' || "TenantId"::text || '|' ||
                    "PlacementTreeScopeId"::text || '|' || "ParticipantId"::text || '|' ||
                    COALESCE("PlacementParentParticipantId"::text, '') || '|' ||
                    COALESCE("PlacementSlot"::text, '') || '|' || "CanonicalPath" || '|' ||
                    "PlacedAt"::text || '|' || "RulesVersion",
                    ',' ORDER BY "Id"), ''))
                FROM public."AQGreenNetworkPlacements"
                WHERE "PlacementTreeScopeId" = @scopeId;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("scopeId", scopeId);
            return Convert.ToString(await command.ExecuteScalarAsync());
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

        private static async Task CorruptAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql,
            params NpgsqlParameter[] parameters)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE public.\"AQGreenNetworkPlacements\" DISABLE TRIGGER ALL;");
            await ExecuteAsync(connection, transaction, sql, parameters);
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE public.\"AQGreenNetworkPlacements\" ENABLE TRIGGER ALL;");
        }

        private static Guid P(int number) =>
            AQGreenPlacementTopologyPostgreSqlFixture.Participant(1, number);

        private static Guid P2(int number) =>
            AQGreenPlacementTopologyPostgreSqlFixture.Participant(2, number);

        private sealed class TopologyBuilder
        {
            private static readonly DateTime PlacedAt =
                new(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            private readonly NpgsqlConnection _connection;
            private readonly NpgsqlTransaction _transaction;
            private readonly int _tenantId;
            private readonly Guid _scopeId;
            private readonly Dictionary<Guid, string> _canonicalPaths = new();

            public TopologyBuilder(
                NpgsqlConnection connection,
                NpgsqlTransaction transaction,
                int tenantId,
                Guid scopeId)
            {
                _connection = connection;
                _transaction = transaction;
                _tenantId = tenantId;
                _scopeId = scopeId;
            }

            public async Task AddRootAsync(Guid participantId)
            {
                await ExecuteAsync(
                    """
                    INSERT INTO public."AQGreenPlacementTreeScopes" ("Id", "TenantId")
                    VALUES (@scopeId, @tenantId);
                    """,
                    new NpgsqlParameter("scopeId", _scopeId),
                    new NpgsqlParameter("tenantId", _tenantId));
                await ExecutePlacementAsync(participantId, null, null, string.Empty);
                _canonicalPaths.Add(participantId, string.Empty);
            }

            public async Task AddChildAsync(
                Guid parentParticipantId,
                Guid participantId,
                int slot)
            {
                var canonicalPath = _canonicalPaths[parentParticipantId] + slot;
                await ExecutePlacementAsync(
                    participantId,
                    parentParticipantId,
                    slot,
                    canonicalPath);
                _canonicalPaths.Add(participantId, canonicalPath);
            }

            private Task ExecutePlacementAsync(
                Guid participantId,
                Guid? parentParticipantId,
                int? slot,
                string canonicalPath) =>
                ExecuteAsync(
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
                    new NpgsqlParameter("scopeId", _scopeId),
                    new NpgsqlParameter("participantId", participantId),
                    new NpgsqlParameter("parentParticipantId",
                        parentParticipantId.HasValue
                            ? parentParticipantId.Value
                            : DBNull.Value),
                    new NpgsqlParameter("slot", slot.HasValue ? slot.Value : DBNull.Value),
                    new NpgsqlParameter("canonicalPath", canonicalPath),
                    new NpgsqlParameter("placedAt", PlacedAt),
                    new NpgsqlParameter("rulesVersion", AQGreenPlacementRules.CurrentVersion));

            private async Task ExecuteAsync(
                string sql,
                params NpgsqlParameter[] parameters)
            {
                await using var command = new NpgsqlCommand(sql, _connection, _transaction);
                command.Parameters.AddRange(parameters);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public sealed class AQGreenPlacementTopologyReaderDependencyInjectionTests
        : AqualLifeStyleTestBase
    {
        [Fact]
        public void Reader_IsResolvableThroughAbpConventionRegistration()
        {
            Resolve<IAQGreenPlacementTopologyReader>()
                .ShouldBeOfType<AQGreenPlacementTopologyReader>();
        }
    }

    public sealed class AQGreenPlacementTopologyReaderProviderGuardTests
    {
        [Fact]
        public async Task NonNpgsqlProvider_IsRejectedBeforeEveryReadOperation()
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseSqlite("DataSource=:memory:")
                    .Options);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            var reader = new AQGreenPlacementTopologyReader(provider);
            var scopeId = Guid.NewGuid();
            var participantId = Guid.NewGuid();

            var placementException = await Should.ThrowAsync<NotSupportedException>(() =>
                reader.GetPlacementAsync(1, scopeId, participantId));
            var childrenException = await Should.ThrowAsync<NotSupportedException>(() =>
                reader.GetChildrenAsync(1, scopeId, participantId));
            var subtreeException = await Should.ThrowAsync<NotSupportedException>(() =>
                reader.GetSubtreeInCanonicalOrderAsync(1, scopeId, participantId));

            placementException.Message.ShouldContain("requires PostgreSQL");
            childrenException.Message.ShouldBe(placementException.Message);
            subtreeException.Message.ShouldBe(placementException.Message);
        }
    }

    public sealed class AQGreenPlacementTopologyPostgreSqlFixture : IAsyncLifetime
    {
        private readonly string _containerName =
            $"aqgreen-topology-reader-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName =
            $"aqgreen_topology_reader_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public AQGreenPlacementTopologyPostgreSqlFixture()
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
            await MigrateAsync();
            await SeedParticipantsAsync();
        }

        public Task DisposeAsync() =>
            RunDockerAsync($"rm -fv {_containerName}", throwOnFailure: false);

        public async Task<NpgsqlConnection> OpenConnectionAsync()
        {
            var connection = new NpgsqlConnection(TestConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        public AqualLifeStyleDbContext CreateDbContext(
            NpgsqlConnection connection,
            Action<string> logger = null)
        {
            var options = new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseNpgsql(connection);
            if (logger != null)
            {
                options.LogTo(logger);
            }

            return new AqualLifeStyleDbContext(options.Options);
        }

        public async Task<long> ScopePlacementCountAsync(Guid scopeId)
        {
            await using var connection = await OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM public."AQGreenNetworkPlacements"
                WHERE "PlacementTreeScopeId" = @scopeId;
                """,
                connection);
            command.Parameters.AddWithValue("scopeId", scopeId);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        public async Task<long> ScopeCountAsync(Guid scopeId)
        {
            await using var connection = await OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM public."AQGreenPlacementTreeScopes"
                WHERE "Id" = @scopeId;
                """,
                connection);
            command.Parameters.AddWithValue("scopeId", scopeId);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        public static Guid Participant(int tenantId, int number) =>
            Guid.Parse($"{tenantId:D8}-0000-0000-0000-{number:D12}");

        private async Task MigrateAsync()
        {
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(TestConnectionString)
                    .Options);
            await context.GetService<IMigrator>().MigrateAsync();
        }

        private Task SeedParticipantsAsync() =>
            ExecuteAsync(
                TestConnectionString,
                """
                INSERT INTO "AbpTenants" (
                    "Id", "TenancyName", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES
                    (1, 'topology-one', 'Topology One', TRUE, NOW(), FALSE),
                    (2, 'topology-two', 'Topology Two', TRUE, NOW(), FALSE);

                INSERT INTO "Areas" (
                    "Id", "TenantId", "Code", "Name", "IsActive", "CreationTime", "IsDeleted")
                VALUES
                    ('a0000000-0000-0000-0000-000000000001', 1, 'EAST', 'Area East', TRUE, NOW(), FALSE),
                    ('a0000000-0000-0000-0000-000000000002', 1, 'WEST', 'Area West', TRUE, NOW(), FALSE),
                    ('a0000000-0000-0000-0000-000000000003', 2, 'OTHER', 'Other Area', TRUE, NOW(), FALSE);

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id + 1000, 1, 'topology-user-' || id,
                    'topology-user-' || id || '@example.test', 'Topology', id::text,
                    'TOPOLOGY-USER-' || id,
                    'TOPOLOGY-USER-' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(1, 48) id;

                INSERT INTO "AbpUsers" (
                    "Id", "TenantId", "UserName", "EmailAddress", "Name", "Surname",
                    "NormalizedUserName", "NormalizedEmailAddress", "Password", "Role",
                    "IsEmailConfirmed", "IsActive", "CreationTime", "IsDeleted",
                    "AccessFailedCount", "IsLockoutEnabled", "IsPhoneNumberConfirmed",
                    "IsTwoFactorEnabled")
                SELECT
                    id + 2000, 2, 'topology-user-two-' || id,
                    'topology-user-two-' || id || '@example.test', 'Topology Two', id::text,
                    'TOPOLOGY-USER-TWO-' || id,
                    'TOPOLOGY-USER-TWO-' || id || '@EXAMPLE.TEST', 'test-password', 3,
                    TRUE, TRUE, NOW(), FALSE, 0, FALSE, FALSE, FALSE
                FROM generate_series(1, 16) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id, 1, 'Topology Customer ' || id,
                    'topology-customer-' || id || '@example.test',
                    CASE WHEN id % 2 = 1
                        THEN 'a0000000-0000-0000-0000-000000000001'::uuid
                        ELSE 'a0000000-0000-0000-0000-000000000002'::uuid END,
                    TRUE, NOW(), 'T1-CLB-' || id, id + 1000, FALSE
                FROM generate_series(1, 48) id;

                INSERT INTO "Customers" (
                    "Id", "TenantId", "Name", "Email", "AreaId", "IsActive",
                    "CreationTime", "ClubMemberNumber", "UserId", "IsDeleted")
                SELECT
                    id + 100, 2, 'Topology Tenant Two ' || id,
                    'topology-tenant-two-' || id || '@example.test',
                    'a0000000-0000-0000-0000-000000000003'::uuid,
                    TRUE, NOW(), 'T2-CLB-' || id, id + 2000, FALSE
                FROM generate_series(1, 16) id;

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    ('00000001-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
                    1, id, 2, TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    'entry-terms-v1', TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 48) id;

                INSERT INTO "EntryParticipations" (
                    "Id", "TenantId", "CustomerId", "Status", "StartedAt",
                    "TermsVersion", "TermsEffectiveFrom", "JoiningPaymentAmount",
                    "JoiningInstallmentAmount", "RegistrationPaymentAmount",
                    "ActivationPaymentAmount", "MonthlyCommitmentAmount",
                    "GracePeriodDays", "Currency", "CreationTime", "IsDeleted")
                SELECT
                    ('00000002-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
                    2, id + 100, 2, TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    'entry-terms-v1', TIMESTAMPTZ '2026-08-01 00:00:00+00',
                    1200, 600, 600, 600, 600, 7, 'ZAR', NOW(), FALSE
                FROM generate_series(1, 16) id;
                """);

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
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

        private string BuildConnectionString(string database) =>
            $"Host=localhost;Port={_hostPort};Database={database};" +
            "Username=aqualifestyle;Password=aqualifestyle";

        private string AdminConnectionString => BuildConnectionString("postgres");
        private string TestConnectionString => BuildConnectionString(_databaseName);
    }
}
