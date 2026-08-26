using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenPlacementAllocatorPostgreSqlTests
    {
        private static readonly DateTime SeedPlacedAt =
            new(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc);
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;

        public AQGreenPlacementAllocatorPostgreSqlTests(
            AQGreenPlacementAllocatorPostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CanonicalAllocation_UsesSlotsOneToFiveThenParentMajorOverflow()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            var participants = Enumerable.Range(20, 11)
                .Select(number => P(number))
                .ToArray();
            await SeedSponsoredAttributionsAsync(database, 1, P(1), participants);

            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            var allocator = CreateAllocator(context);
            var results = new List<PlacementSnapshot>();
            foreach (var participantId in participants)
            {
                results.Add(Snapshot(
                    await allocator.AllocateAsync(1, participantId)));
            }
            await transaction.CommitAsync();

            var expectedParent = P(1);
            results.Take(5).Select(result => result.ParentParticipantId)
                .ShouldAllBe(parent => parent == expectedParent);
            results.Take(5).Select(result => result.PlacementSlot)
                .ShouldBe(new int?[] { 1, 2, 3, 4, 5 });
            results.Take(5).Select(result => result.CanonicalPath)
                .ShouldBe(new[] { "1", "2", "3", "4", "5" });
            results[5].ShouldMatch(P(20), 1, "11");
            results[6].ShouldMatch(P(20), 2, "12");
            results[9].ShouldMatch(P(20), 5, "15");
            results[10].ShouldMatch(P(21), 1, "21");
            results.ShouldAllBe(result => result.PlacementSlot.HasValue && result.PlacementSlot >= 1 && result.PlacementSlot <= 5);
            results.ShouldAllBe(result =>
                result.RulesVersion == AQGreenPlacementRules.CurrentVersion &&
                result.PlacedAt.Kind == DateTimeKind.Utc &&
                result.PlacedAt >= SeedPlacedAt &&
                !result.WasAlreadyPlaced);
        }

        [Fact]
        public async Task SponsorLocalAllocation_DoesNotEscapeToEarlierGlobalVacancies()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedChildAsync(database, 1, scopeId, P(1), P(2), 1, "1");
            await SeedSponsoredAttributionsAsync(database, 1, P(2), P(20));

            var result = await AllocateAndCommitAsync(database, 1, P(20));

            result.ShouldMatch(P(2), 1, "11");
            result.PlacementTreeScopeId.ShouldBe(scopeId);
        }

        [Fact]
        public async Task SameTenantCrossAreaAllocation_SucceedsWithoutAreaPredicate()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            var result = await AllocateAndCommitAsync(database, 1, P(20));

            result.ShouldMatch(P(1), 1, "1");
            (await ScalarAsync<long>(
                    database,
                    $$"""
                    SELECT COUNT(DISTINCT customer."AreaId")
                    FROM public."EntryParticipations" participation
                    JOIN public."Customers" customer
                      ON customer."TenantId" = participation."TenantId"
                     AND customer."Id" = participation."CustomerId"
                    WHERE participation."Id" IN ('{{P(1)}}', '{{P(20)}}');
                    """))
                .ShouldBe(2);
        }

        [Fact]
        public async Task AuthorisedRootAttribution_IsRejectedByNormalAllocator()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await SeedRootAttributionAsync(database, 1, P(20));

            var exception = await InTransactionExpectAsync<
                AQGreenPlacementUnsupportedAttributionException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));

            exception.AttributionKind.ShouldBe(
                AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot);
        }

        [Fact]
        public async Task UnconfirmedAttribution_IsRejectedAfterScopeLock()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(
                database,
                1,
                P(1),
                confirmed: false,
                P(20));

            await InTransactionExpectAsync<AQGreenPlacementAttributionNotConfirmedException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));
        }

        [Fact]
        public async Task MissingSponsorPlacement_IsRejected()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            var exception = await InTransactionExpectAsync<
                AQGreenPlacementAllocationNotFoundException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));

            exception.MissingFact.ShouldBe(AQGreenPlacementMissingFact.SponsorPlacement);
        }

        [Fact]
        public async Task CrossTenantSponsorEvidence_IsRejected()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 2, scopeId, P(1, 2));
            await SeedCrossTenantAttributionAsync(database, P(20), P(1, 2));

            await InTransactionExpectAsync<AQGreenPlacementConflictException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));
        }

        [Fact]
        public async Task ExistingPlacementOutsideSponsorSubtree_FailsReconciliation()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var sponsorScope = Guid.NewGuid();
            var conflictingScope = Guid.NewGuid();
            await SeedScopeAsync(database, 1, sponsorScope, P(1));
            await SeedScopeAsync(database, 1, conflictingScope, P(2));
            await SeedChildAsync(
                database,
                1,
                conflictingScope,
                P(2),
                P(20),
                1,
                "1");
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            await InTransactionExpectAsync<AQGreenPlacementConflictException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));
        }

        [Fact]
        public async Task CommitBeforeAcknowledgementRetry_ReturnsExactExistingPlacement()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            var committed = await AllocateAndCommitAsync(database, 1, P(20));
            var retry = await AllocateAndCommitAsync(database, 1, P(20));

            retry.WasAlreadyPlaced.ShouldBeTrue();
            retry.Id.ShouldBe(committed.Id);
            retry.PlacementTreeScopeId.ShouldBe(committed.PlacementTreeScopeId);
            retry.ParentParticipantId.ShouldBe(committed.ParentParticipantId);
            retry.PlacementSlot.ShouldBe(committed.PlacementSlot);
            retry.CanonicalPath.ShouldBe(committed.CanonicalPath);
            retry.PlacedAt.ShouldBe(committed.PlacedAt);
            retry.RulesVersion.ShouldBe(committed.RulesVersion);
            (await PlacementCountAsync(database, P(20))).ShouldBe(1);
        }

        [Fact]
        public async Task ConcurrentSameParticipantUniqueViolation_ReconcilesExactPlacement()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            var clock = new BlockingPlacementClock();

            var allocation = CreateAllocator(context, clock).AllocateAsync(1, P(20));
            await clock.WaitUntilRequestedAsync();
            await SeedChildAsync(database, 1, scopeId, P(1), P(20), 2, "2");
            clock.Release();

            var result = Snapshot(await allocation.WaitAsync(TimeSpan.FromSeconds(10)));
            result.WasAlreadyPlaced.ShouldBeTrue();
            result.ShouldMatch(P(1), 2, "2");
            await transaction.CommitAsync();
            (await PlacementCountAsync(database, P(20))).ShouldBe(1);
        }

        [Fact]
        public async Task ConcurrentParentSlotUniqueViolation_FailsClosedAndLeavesNoPlacement()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            var clock = new BlockingPlacementClock();

            var allocation = CreateAllocator(context, clock).AllocateAsync(1, P(20));
            await clock.WaitUntilRequestedAsync();
            await SeedChildAsync(database, 1, scopeId, P(1), P(2), 1, "1");
            clock.Release();

            var exception = await Should.ThrowAsync<AQGreenPlacementConflictException>(
                () => allocation);
            exception.Message.ShouldContain("without using the required scope lock");
            await transaction.RollbackAsync();
            (await PlacementCountAsync(database, P(20))).ShouldBe(0);
        }

        [Fact]
        public async Task Retry_PreservesPersistedRulesVersionInsteadOfReallocating()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedChildAsync(
                database,
                1,
                scopeId,
                P(1),
                P(20),
                1,
                "1",
                "AQGreenPlacementV2Previous");
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            var retry = await AllocateAndCommitAsync(database, 1, P(20));

            retry.WasAlreadyPlaced.ShouldBeTrue();
            retry.ShouldMatch(P(1), 1, "1");
            retry.RulesVersion.ShouldBe("AQGreenPlacementV2Previous");
            (await PlacementCountAsync(database, P(20))).ShouldBe(1);
        }

        [Fact]
        public async Task AllocationWithoutCallerOwnedTransaction_FailsClosed()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);

            await Should.ThrowAsync<InvalidOperationException>(() =>
                CreateAllocator(context).AllocateAsync(1, P(20)));
        }

        [Fact]
        public async Task SerializableCallerTransaction_IsRejectedBeforeScopeResolution()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                CreateAllocator(context).AllocateAsync(1, P(20)));

            exception.Message.ShouldContain("READ COMMITTED");
        }

        [Fact]
        public async Task UnsupportedProvider_FailsBeforePretendingLockSafety()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseSqlite(connection)
                    .Options);

            await Should.ThrowAsync<NotSupportedException>(() =>
                CreateAllocator(context).AllocateAsync(1, P(20)));
        }

        [Fact]
        public async Task SameScopeRace_AllocatesExactNextTwoCanonicalVacancies()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            for (var slot = 1; slot <= 4; slot++)
            {
                await SeedChildAsync(
                    database,
                    1,
                    scopeId,
                    P(1),
                    P(slot + 1),
                    slot,
                    slot.ToString());
            }
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20), P(21));

            await using var blocker = await AcquireScopeBlockerAsync(database, scopeId);
            var first = AllocateAndCommitAsync(database, 1, P(20), "same-scope-first");
            var second = AllocateAndCommitAsync(database, 1, P(21), "same-scope-second");
            await _fixture.WaitForAdvisoryWaitersAsync(
                database,
                2,
                "same-scope-first",
                "same-scope-second");

            await blocker.CommitAsync();
            var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));

            results.Select(result => (result.ParentParticipantId, result.PlacementSlot))
                .ShouldBe(new[] { (P(1) as Guid?, 5 as int?), (P(2) as Guid?, 1 as int?) },
                    ignoreOrder: true);
            results.Select(result => result.CanonicalPath)
                .ShouldBe(new[] { "5", "11" }, ignoreOrder: true);
            (await DistinctParentSlotCountAsync(database, scopeId)).ShouldBe(6);
            (await MaximumSlotAsync(database, scopeId)).ShouldBe(5);
        }

        [Fact]
        public async Task AncestorAndDescendantSponsors_RacingForOneVacancyAreScopeSerialized()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            for (var slot = 1; slot <= 5; slot++)
            {
                await SeedChildAsync(
                    database,
                    1,
                    scopeId,
                    P(1),
                    P(slot + 1),
                    slot,
                    slot.ToString());
            }
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await SeedSponsoredAttributionsAsync(database, 1, P(2), P(21));

            await using var blocker = await AcquireScopeBlockerAsync(database, scopeId);
            var ancestorWriter = AllocateAndCommitAsync(
                database,
                1,
                P(20),
                "ancestor-writer");
            var descendantWriter = AllocateAndCommitAsync(
                database,
                1,
                P(21),
                "descendant-writer");
            await _fixture.WaitForAdvisoryWaitersAsync(
                database,
                2,
                "ancestor-writer",
                "descendant-writer");

            await blocker.CommitAsync();
            var results = await Task.WhenAll(ancestorWriter, descendantWriter)
                .WaitAsync(TimeSpan.FromSeconds(15));

            var expectedParent = P(2);
            results.Select(result => result.ParentParticipantId)
                .ShouldAllBe(parent => parent == expectedParent);
            results.Select(result => result.CanonicalPath)
                .ShouldBe(new[] { "11", "12" }, ignoreOrder: true);
            results.Select(result => result.PlacementSlot)
                .ShouldBe(new int?[] { 1, 2 }, ignoreOrder: true);
        }

        [Fact]
        public async Task AuthoritativeConfirmationCommittedWhileWaiting_IsReadAfterScopeLock()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(
                database,
                1,
                P(1),
                confirmed: false,
                P(20));

            await using var blocker = await AcquireScopeBlockerAsync(database, scopeId);
            var writer = AllocateAndCommitAsync(
                database,
                1,
                P(20),
                "post-lock-reread-writer");
            await _fixture.WaitForAdvisoryWaitersAsync(
                database,
                1,
                "post-lock-reread-writer");

            await ConfirmAttributionAsync(database, 1, P(20));
            await blocker.CommitAsync();

            var result = await writer.WaitAsync(TimeSpan.FromSeconds(10));
            result.ShouldMatch(P(1), 1, "1");
        }

        [Fact]
        public async Task SameParticipantRace_ReturnsOneExactPlacementToBothCallers()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            await using var blocker = await AcquireScopeBlockerAsync(database, scopeId);
            var first = AllocateAndCommitAsync(database, 1, P(20), "same-participant-first");
            var second = AllocateAndCommitAsync(database, 1, P(20), "same-participant-second");
            await _fixture.WaitForAdvisoryWaitersAsync(
                database,
                2,
                "same-participant-first",
                "same-participant-second");

            await blocker.CommitAsync();
            var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));

            results[0].Id.ShouldBe(results[1].Id);
            results[0].ParentParticipantId.ShouldBe(results[1].ParentParticipantId);
            results[0].PlacementSlot.ShouldBe(results[1].PlacementSlot);
            results[0].CanonicalPath.ShouldBe(results[1].CanonicalPath);
            results[0].PlacedAt.ShouldBe(results[1].PlacedAt);
            results.Count(result => result.WasAlreadyPlaced).ShouldBe(1);
            (await PlacementCountAsync(database, P(20))).ShouldBe(1);
        }

        [Fact]
        public async Task DifferentScopes_DoNotShareLogicalLock()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var firstScope = Guid.NewGuid();
            var secondScope = Guid.NewGuid();
            await SeedScopeAsync(database, 1, firstScope, P(1));
            await SeedScopeAsync(database, 1, secondScope, P(2));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await SeedSponsoredAttributionsAsync(database, 1, P(2), P(21));

            await using var blocker = await AcquireScopeBlockerAsync(database, firstScope);
            var blocked = AllocateAndCommitAsync(database, 1, P(20), "scope-one-writer");
            await _fixture.WaitForAdvisoryWaitersAsync(
                database,
                1,
                "scope-one-writer");

            var independent = await AllocateAndCommitAsync(
                    database,
                    1,
                    P(21),
                    "scope-two-writer")
                .WaitAsync(TimeSpan.FromSeconds(5));
            independent.PlacementTreeScopeId.ShouldBe(secondScope);
            independent.ShouldMatch(P(2), 1, "1");

            await blocker.CommitAsync();
            (await blocked.WaitAsync(TimeSpan.FromSeconds(10)))
                .PlacementTreeScopeId.ShouldBe(firstScope);
        }

        [Fact]
        public async Task ManyConcurrentWriters_CreateExactCanonicalPrefixWithoutDuplicates()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            var participants = Enumerable.Range(20, 16)
                .Select(number => P(number))
                .ToArray();
            await SeedSponsoredAttributionsAsync(database, 1, P(1), participants);

            await using var blocker = await AcquireScopeBlockerAsync(database, scopeId);
            var writers = participants
                .Select((participant, index) => AllocateAndCommitAsync(
                    database,
                    1,
                    participant,
                    $"many-writer-{index}"))
                .ToArray();
            await _fixture.WaitForAdvisoryWaitersAsync(
                database,
                writers.Length,
                Enumerable.Range(0, writers.Length)
                    .Select(index => $"many-writer-{index}")
                    .ToArray());

            await blocker.CommitAsync();
            var results = await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(30));

            results.Select(result => result.CanonicalPath)
                .ShouldBe(
                    new[]
                    {
                        "1", "2", "3", "4", "5",
                        "11", "12", "13", "14", "15",
                        "21", "22", "23", "24", "25",
                        "31"
                    },
                    ignoreOrder: true);
            results.Select(result => result.Id).Distinct().Count().ShouldBe(16);
            (await PlacementCountAsync(database, participants)).ShouldBe(16);
            (await DistinctParentSlotCountAsync(database, scopeId)).ShouldBe(16);
            (await MaximumSlotAsync(database, scopeId)).ShouldBe(5);
        }

        [Fact]
        public async Task RolledBackAllocation_ReleasesLockAndDoesNotConsumeVacancy()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            await using var firstConnection =
                new NpgsqlConnection(database.ConnectionString("rollback-first"));
            await firstConnection.OpenAsync();
            await using var firstContext = _fixture.CreateDbContext(firstConnection);
            await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
            var tentative = Snapshot(await CreateAllocator(firstContext).AllocateAsync(1, P(20)));
            tentative.ShouldMatch(P(1), 1, "1");

            var retry = AllocateAndCommitAsync(database, 1, P(20), "rollback-retry");
            await _fixture.WaitForAdvisoryWaitersAsync(database, 1, "rollback-retry");
            await firstTransaction.RollbackAsync();

            var committed = await retry.WaitAsync(TimeSpan.FromSeconds(10));
            committed.ShouldMatch(P(1), 1, "1");
            committed.Id.ShouldNotBe(tentative.Id);
            (await PlacementCountAsync(database, P(20))).ShouldBe(1);
        }

        [Fact]
        public async Task CancelledLockWait_RollsBackAndCanBeRetried()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));

            await using var blocker = await AcquireScopeBlockerAsync(database, scopeId);
            using var cancellation = new CancellationTokenSource();
            var cancelled = AllocateAndCommitAsync(
                database,
                1,
                P(20),
                "cancelled-writer",
                cancellation.Token);
            await _fixture.WaitForAdvisoryWaitersAsync(database, 1, "cancelled-writer");
            cancellation.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(() => cancelled);
            (await PlacementCountAsync(database, P(20))).ShouldBe(0);
            await blocker.RollbackAsync();

            var retry = await AllocateAndCommitAsync(database, 1, P(20));
            retry.ShouldMatch(P(1), 1, "1");
        }

        [Theory]
        [InlineData("path")]
        [InlineData("orphan")]
        [InlineData("cycle")]
        public async Task CorruptTopology_PropagatesIntegrityFailure(string corruption)
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedChildAsync(database, 1, scopeId, P(1), P(2), 1, "1");
            var sponsorId = corruption == "orphan" ? P(2) : P(1);
            if (corruption == "cycle")
                await SeedChildAsync(database, 1, scopeId, P(2), P(3), 1, "11");
            await SeedSponsoredAttributionsAsync(database, 1, sponsorId, P(20));
            await CorruptTopologyAsync(database, corruption);

            await InTransactionExpectAsync<AQGreenPlacementTopologyIntegrityException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));
            (await PlacementCountAsync(database, P(20))).ShouldBe(0);
        }

        [Fact]
        public async Task DeletedSponsor_FailsClosedWithoutInventingTerminalDisposition()
        {
            await using var database = await _fixture.CreateDatabaseAsync();
            var scopeId = Guid.NewGuid();
            await SeedScopeAsync(database, 1, scopeId, P(1));
            await SeedSponsoredAttributionsAsync(database, 1, P(1), P(20));
            await ExecuteAsync(
                database.ConnectionString(),
                $$"""
                UPDATE public."EntryParticipations"
                SET "IsDeleted" = TRUE,
                    "DeletionTime" = TIMESTAMPTZ '2026-08-26 09:00:00+00'
                WHERE "Id" = '{{P(1)}}';
                """);

            await InTransactionExpectAsync<AQGreenPlacementConflictException>(
                database,
                allocator => allocator.AllocateAsync(1, P(20)));
        }

        private async Task<PlacementSnapshot> AllocateAndCommitAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid participantId,
            string applicationName = null,
            CancellationToken cancellationToken = default)
        {
            await using var connection =
                new NpgsqlConnection(database.ConnectionString(applicationName));
            await connection.OpenAsync(cancellationToken);
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);
            var result = await CreateAllocator(context).AllocateAsync(
                tenantId,
                participantId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Snapshot(result);
        }

        private async Task<TException> InTransactionExpectAsync<TException>(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Func<AQGreenPlacementAllocator, Task> action)
            where TException : Exception
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            return await Should.ThrowAsync<TException>(() => action(CreateAllocator(context)));
        }

        private static AQGreenPlacementAllocator CreateAllocator(
            AqualLifeStyleDbContext context,
            IAQGreenPlacementClock clock = null)
        {
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            return new AQGreenPlacementAllocator(
                provider,
                new AQGreenPlacementTreeLock(provider),
                new AQGreenPlacementTopologyReader(provider),
                clock ?? new AQGreenPlacementClock(provider));
        }

        private static async Task<ScopeBlocker> AcquireScopeBlockerAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Guid scopeId)
        {
            var connection = new NpgsqlConnection(database.ConnectionString("scope-blocker"));
            await connection.OpenAsync();
            var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@resource, 0));",
                connection,
                transaction);
            command.Parameters.AddWithValue(
                "resource",
                "aqgreen-placement-v2:" + scopeId.ToString("N"));
            await command.ExecuteNonQueryAsync();
            return new ScopeBlocker(connection, transaction);
        }

        private static async Task SeedScopeAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid scopeId,
            Guid rootParticipantId)
        {
            await ExecuteAsync(
                database.ConnectionString(),
                $$"""
                INSERT INTO public."AQGreenPlacementTreeScopes" ("Id", "TenantId")
                VALUES ('{{scopeId}}', {{tenantId}});

                INSERT INTO public."AQGreenNetworkPlacements" (
                    "Id", "TenantId", "PlacementTreeScopeId", "ParticipantId",
                    "PlacementParentParticipantId", "PlacementSlot", "CanonicalPath",
                    "PlacedAt", "RulesVersion")
                VALUES (
                    '{{Guid.NewGuid()}}', {{tenantId}}, '{{scopeId}}', '{{rootParticipantId}}',
                    NULL, NULL, '', TIMESTAMPTZ '2026-08-26 08:00:00+00',
                    '{{AQGreenPlacementRules.CurrentVersion}}');
                """);
        }

        private static Task SeedChildAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid scopeId,
            Guid parentParticipantId,
            Guid participantId,
            int slot,
            string canonicalPath,
            string rulesVersion = AQGreenPlacementRules.CurrentVersion) =>
            ExecuteAsync(
                database.ConnectionString(),
                $$"""
                INSERT INTO public."AQGreenNetworkPlacements" (
                    "Id", "TenantId", "PlacementTreeScopeId", "ParticipantId",
                    "PlacementParentParticipantId", "PlacementSlot", "CanonicalPath",
                    "PlacedAt", "RulesVersion")
                VALUES (
                    '{{Guid.NewGuid()}}', {{tenantId}}, '{{scopeId}}', '{{participantId}}',
                    '{{parentParticipantId}}', {{slot}}, '{{canonicalPath}}',
                    TIMESTAMPTZ '2026-08-26 08:01:00+00',
                    '{{rulesVersion}}');
                """);

        private static async Task SeedSponsoredAttributionsAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid sponsorParticipantId,
            params Guid[] participantIds) =>
            await SeedSponsoredAttributionsAsync(
                database,
                tenantId,
                sponsorParticipantId,
                confirmed: true,
                participantIds);

        private static async Task SeedSponsoredAttributionsAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid sponsorParticipantId,
            bool confirmed,
            params Guid[] participantIds)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            foreach (var participantId in participantIds)
            {
                var attributionId = Guid.NewGuid();
                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO public."AQGreenRecruitmentAttributions" (
                        "Id", "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                        "AttributionKind", "AcquisitionSource", "SourceReferenceId",
                        "AttributedAt", "AttributedByUserId", "AssignmentReason", "RulesVersion")
                    VALUES (
                        @attributionId, @tenantId, @participantId, @sponsorParticipantId,
                        1, 1, @sponsorParticipantId,
                        TIMESTAMPTZ '2026-08-26 08:10:00+00', NULL, NULL,
                        'AQGreenRecruitmentAttributionV1');
                    """ +
                    (confirmed
                        ? """

                          INSERT INTO public."AQGreenRecruitmentAttributionConfirmations" (
                              "Id", "TenantId", "AttributionId", "ConfirmedAt",
                              "ConfirmedByUserId", "ConfirmationMethod",
                              "EvidenceReferenceId", "RulesVersion")
                          VALUES (
                              @confirmationId, @tenantId, @attributionId,
                              TIMESTAMPTZ '2026-08-26 08:11:00+00', NULL, 1,
                              @evidenceReferenceId, 'AQGreenRecruitmentAttributionV1');
                          """
                        : string.Empty),
                    connection,
                    transaction);
                command.Parameters.AddWithValue("attributionId", attributionId);
                command.Parameters.AddWithValue("tenantId", tenantId);
                command.Parameters.AddWithValue("participantId", participantId);
                command.Parameters.AddWithValue("sponsorParticipantId", sponsorParticipantId);
                if (confirmed)
                {
                    command.Parameters.AddWithValue("confirmationId", Guid.NewGuid());
                    command.Parameters.AddWithValue("evidenceReferenceId", Guid.NewGuid());
                }
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        private static Task ConfirmAttributionAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid participantId) =>
            ExecuteAsync(
                database.ConnectionString(),
                $$"""
                INSERT INTO public."AQGreenRecruitmentAttributionConfirmations" (
                    "Id", "TenantId", "AttributionId", "ConfirmedAt",
                    "ConfirmedByUserId", "ConfirmationMethod", "EvidenceReferenceId",
                    "RulesVersion")
                SELECT
                    '{{Guid.NewGuid()}}', {{tenantId}}, attribution."Id",
                    TIMESTAMPTZ '2026-08-26 08:11:00+00', NULL, 1,
                    '{{Guid.NewGuid()}}', 'AQGreenRecruitmentAttributionV1'
                FROM public."AQGreenRecruitmentAttributions" attribution
                WHERE attribution."TenantId" = {{tenantId}}
                  AND attribution."ParticipantId" = '{{participantId}}';
                """);

        private static Task SeedRootAttributionAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            int tenantId,
            Guid participantId) =>
            ExecuteAsync(
                database.ConnectionString(),
                $$"""
                INSERT INTO public."AQGreenRecruitmentAttributions" (
                    "Id", "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                    "AttributionKind", "AcquisitionSource", "SourceReferenceId",
                    "AttributedAt", "AttributedByUserId", "AssignmentReason", "RulesVersion")
                VALUES (
                    '{{Guid.NewGuid()}}', {{tenantId}}, '{{participantId}}', NULL,
                    2, 2, '{{Guid.NewGuid()}}', TIMESTAMPTZ '2026-08-26 08:10:00+00',
                    3001, 'Authorised root evidence', 'AQGreenRecruitmentAttributionV1');
                """);

        private static async Task SeedCrossTenantAttributionAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Guid participantId,
            Guid sponsorParticipantId)
        {
            var attributionId = Guid.NewGuid();
            await ExecuteAsync(
                database.ConnectionString(),
                $$"""
                ALTER TABLE public."AQGreenRecruitmentAttributions" DISABLE TRIGGER ALL;
                INSERT INTO public."AQGreenRecruitmentAttributions" (
                    "Id", "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                    "AttributionKind", "AcquisitionSource", "SourceReferenceId",
                    "AttributedAt", "AttributedByUserId", "AssignmentReason", "RulesVersion")
                VALUES (
                    '{{attributionId}}', 1, '{{participantId}}', '{{sponsorParticipantId}}',
                    1, 1, '{{sponsorParticipantId}}', TIMESTAMPTZ '2026-08-26 08:10:00+00',
                    NULL, NULL, 'AQGreenRecruitmentAttributionV1');
                ALTER TABLE public."AQGreenRecruitmentAttributions" ENABLE TRIGGER ALL;

                INSERT INTO public."AQGreenRecruitmentAttributionConfirmations" (
                    "Id", "TenantId", "AttributionId", "ConfirmedAt",
                    "ConfirmedByUserId", "ConfirmationMethod", "EvidenceReferenceId",
                    "RulesVersion")
                VALUES (
                    '{{Guid.NewGuid()}}', 1, '{{attributionId}}',
                    TIMESTAMPTZ '2026-08-26 08:11:00+00', NULL, 1,
                    '{{Guid.NewGuid()}}', 'AQGreenRecruitmentAttributionV1');
                """);
        }

        private static async Task CorruptTopologyAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            string corruption)
        {
            var mutation = corruption switch
            {
                "path" => $$"""
                          UPDATE public."AQGreenNetworkPlacements"
                          SET "CanonicalPath" = '5'
                          WHERE "ParticipantId" = '{{P(2)}}';
                          """,
                "orphan" => $$"""
                            UPDATE public."AQGreenNetworkPlacements"
                            SET "PlacementParentParticipantId" = '{{P(48)}}'
                            WHERE "ParticipantId" = '{{P(2)}}';
                            """,
                "cycle" => $$"""
                           UPDATE public."AQGreenNetworkPlacements"
                           SET "PlacementParentParticipantId" = '{{P(3)}}',
                               "PlacementSlot" = 1,
                               "CanonicalPath" = '111'
                           WHERE "ParticipantId" = '{{P(1)}}';
                           """,
                _ => throw new ArgumentOutOfRangeException(nameof(corruption))
            };
            await ExecuteAsync(
                database.ConnectionString(),
                "ALTER TABLE public.\"AQGreenNetworkPlacements\" DISABLE TRIGGER ALL;\n" +
                mutation +
                "\nALTER TABLE public.\"AQGreenNetworkPlacements\" ENABLE TRIGGER ALL;");
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<T> ScalarAsync<T>(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            string sql)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T));
        }

        private static Task<long> PlacementCountAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            params Guid[] participantIds)
        {
            var ids = string.Join(",", participantIds.Select(id => $"'{id}'::uuid"));
            return ScalarAsync<long>(
                database,
                $"SELECT COUNT(*) FROM public.\"AQGreenNetworkPlacements\" WHERE \"ParticipantId\" IN ({ids});");
        }

        private static Task<long> DistinctParentSlotCountAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Guid scopeId) =>
            ScalarAsync<long>(
                database,
                $$"""
                SELECT COUNT(*)
                FROM (
                    SELECT "PlacementParentParticipantId", "PlacementSlot"
                    FROM public."AQGreenNetworkPlacements"
                    WHERE "PlacementTreeScopeId" = '{{scopeId}}'
                      AND "PlacementParentParticipantId" IS NOT NULL
                      AND "PlacementSlot" IS NOT NULL
                    GROUP BY "PlacementParentParticipantId", "PlacementSlot") distinct_slots;
                """);

        private static Task<int> MaximumSlotAsync(
            AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease database,
            Guid scopeId) =>
            ScalarAsync<int>(
                database,
                $$"""
                SELECT MAX("PlacementSlot")
                FROM public."AQGreenNetworkPlacements"
                WHERE "PlacementTreeScopeId" = '{{scopeId}}';
                """);

        private static Guid P(int number, int tenantId = 1) =>
            AQGreenPlacementAllocatorPostgreSqlFixture.Participant(tenantId, number);

        private static PlacementSnapshot Snapshot(AQGreenPlacementAllocationResult result) =>
            new(
                result.Placement.Id,
                result.Placement.PlacementTreeScopeId,
                result.Placement.ParticipantId,
                result.Placement.PlacementParentParticipantId,
                result.Placement.PlacementSlot,
                result.Placement.CanonicalPath,
                result.Placement.PlacedAt,
                result.Placement.RulesVersion,
                result.WasAlreadyPlaced);

        private sealed class PlacementSnapshot
        {
            public PlacementSnapshot(
                Guid id,
                Guid placementTreeScopeId,
                Guid participantId,
                Guid? parentParticipantId,
                int? placementSlot,
                string canonicalPath,
                DateTime placedAt,
                string rulesVersion,
                bool wasAlreadyPlaced)
            {
                Id = id;
                PlacementTreeScopeId = placementTreeScopeId;
                ParticipantId = participantId;
                ParentParticipantId = parentParticipantId;
                PlacementSlot = placementSlot;
                CanonicalPath = canonicalPath;
                PlacedAt = placedAt;
                RulesVersion = rulesVersion;
                WasAlreadyPlaced = wasAlreadyPlaced;
            }

            public Guid Id { get; }
            public Guid PlacementTreeScopeId { get; }
            public Guid ParticipantId { get; }
            public Guid? ParentParticipantId { get; }
            public int? PlacementSlot { get; }
            public string CanonicalPath { get; }
            public DateTime PlacedAt { get; }
            public string RulesVersion { get; }
            public bool WasAlreadyPlaced { get; }

            public void ShouldMatch(
                Guid parentParticipantId,
                int placementSlot,
                string canonicalPath)
            {
                ParentParticipantId.ShouldBe(parentParticipantId);
                PlacementSlot.ShouldBe(placementSlot);
                CanonicalPath.ShouldBe(canonicalPath);
            }
        }

        private sealed class ScopeBlocker : IAsyncDisposable
        {
            private readonly NpgsqlConnection _connection;
            private readonly NpgsqlTransaction _transaction;
            private bool _completed;

            public ScopeBlocker(
                NpgsqlConnection connection,
                NpgsqlTransaction transaction)
            {
                _connection = connection;
                _transaction = transaction;
            }

            public async Task CommitAsync()
            {
                await _transaction.CommitAsync();
                _completed = true;
            }

            public async Task RollbackAsync()
            {
                await _transaction.RollbackAsync();
                _completed = true;
            }

            public async ValueTask DisposeAsync()
            {
                if (!_completed)
                    await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                await _connection.DisposeAsync();
            }
        }

        private sealed class BlockingPlacementClock : IAQGreenPlacementClock
        {
            private readonly TaskCompletionSource<bool> _requested =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _released =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task<DateTime> GetUtcNowAsync(
                CancellationToken cancellationToken = default)
            {
                _requested.TrySetResult(true);
                await _released.Task.WaitAsync(cancellationToken);
                return new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
            }

            public Task WaitUntilRequestedAsync() =>
                _requested.Task.WaitAsync(TimeSpan.FromSeconds(10));

            public void Release() => _released.TrySetResult(true);
        }
    }
}
