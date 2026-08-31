using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using Abp.TestBase;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenWeeklySalesEligibilityApplicationPostgreSqlTests
        : AbpIntegratedTestBase<AQGreenGraduationPostgreSqlApplicationTestModule>
    {
        private static readonly DateTime WeekStartUtc =
            new(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);
        private readonly IAdminAQGreenWeeklySalesEligibilityAppService _service;
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;

        public AQGreenWeeklySalesEligibilityApplicationPostgreSqlTests(
            AQGreenPlacementAllocatorPostgreSqlFixture fixture)
        {
            _fixture = fixture.ShouldNotBeNull();
            AbpSession.TenantId = null;
            AbpSession.UserId = 3001;
            _service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();
        }

        [Fact]
        public async Task Confirm_ExactRetryReconciles_ConflictingRetryFails()
        {
            var input = ConfirmInput(60, 5, 5, 5, "ticket:exact");

            var first = await _service.ConfirmAsync(input);
            var exactRetry = await _service.ConfirmAsync(input);

            first.Id.ShouldBe(exactRetry.Id);
            first.ReviewedAt.ShouldBe(exactRetry.ReviewedAt);
            first.ThresholdResult.ShouldBe(AQGreenWeeklySalesThresholdResult.Met);
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _service.ConfirmAsync(
                    ConfirmInput(60, 5, 4, 5, "ticket:exact")));
        }

        [Fact]
        public async Task BeginReview_PersistsOnlyHeldFacts()
        {
            var result = await _service.BeginReviewAsync(
                new BeginAQGreenWeeklySalesReviewInput
                {
                    TenantId = 1,
                    ParticipantId = P(55),
                    CommissionWeekStartUtc = WeekStartUtc
                });

            result.ReviewStatus.ShouldBe(
                AQGreenWeeklySalesReviewStatus.HeldForEvidence);
            result.ThresholdResult.ShouldBeNull();
            result.ReviewedAt.ShouldBeNull();
            result.ReviewedByUserId.ShouldBeNull();
            (await EvidenceCountAsync(55)).ShouldBe(0);
        }

        [Fact]
        public async Task Confirm_NotMet_IsFinalAndEvidenceIsPersistedAtomically()
        {
            var result = await _service.ConfirmAsync(
                ConfirmInput(56, 5, 4, 5, "ticket:not-met"));

            result.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Confirmed);
            result.ThresholdResult.ShouldBe(
                AQGreenWeeklySalesThresholdResult.NotMet);
            result.ReviewedByUserId.ShouldBe(3001);
            (await EvidenceCountAsync(56)).ShouldBe(1);
        }

        [Fact]
        public async Task ConcurrentExactConfirm_SerializesToOneDurableDecision()
        {
            var first = _service.ConfirmAsync(
                ConfirmInput(61, 5, 5, 5, "ticket:concurrent"));
            var second = _service.ConfirmAsync(
                ConfirmInput(61, 5, 5, 5, "ticket:concurrent"));

            var results = await Task.WhenAll(first, second);

            results[0].Id.ShouldBe(results[1].Id);
            (await DecisionCountAsync(61)).ShouldBe(1);
        }

        [Fact]
        public async Task ConcurrentConflictingConfirm_HasOneWinnerAndOneExplicitConflict()
        {
            var first = _service.ConfirmAsync(
                ConfirmInput(57, 5, 5, 5, "ticket:conflict"));
            var second = _service.ConfirmAsync(
                ConfirmInput(57, 5, 4, 5, "ticket:conflict"));
            var successes = 0;
            var conflicts = 0;

            foreach (var operation in new[] { first, second })
            {
                try
                {
                    await operation;
                    successes++;
                }
                catch (UserFriendlyException exception)
                {
                    exception.Details.ShouldContain("conflicting finalized decision");
                    conflicts++;
                }
            }

            successes.ShouldBe(1);
            conflicts.ShouldBe(1);
            (await DecisionCountAsync(57)).ShouldBe(1);
        }

        [Fact]
        public async Task ExactAdvisoryLockWaiter_RereadsCommittedWinnerAndReconciles()
        {
            const int participantNumber = 63;
            const string evidenceReference = "ticket:deterministic-waiter";
            var participantId = P(participantNumber);
            var input = ConfirmInput(
                participantNumber,
                5,
                5,
                5,
                evidenceReference);
            var database = AQGreenGraduationPostgreSqlApplicationTestModule
                .CurrentDatabase.ShouldNotBeNull();
            await using var blockerConnection = new NpgsqlConnection(
                database.ConnectionString("b53-weekly-sales-winner"));
            await blockerConnection.OpenAsync();
            await using var blockerTransaction =
                await blockerConnection.BeginTransactionAsync();
            await using var blockerContext = _fixture.CreateDbContext(
                blockerConnection);
            await blockerContext.Database.UseTransactionAsync(blockerTransaction);
            var provider = Substitute.For<
                IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(blockerContext);
            var exactLock = new AQGreenWeeklySalesEligibilityMutationLock(provider);
            var blockerCommitted = false;
            Task<AQGreenWeeklySalesEligibilityDecisionDto> waiter = null;

            try
            {
                await exactLock.AcquireAsync(
                    1,
                    participantId,
                    WeekStartUtc,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);

                waiter = _service.ConfirmAsync(input);
                await _fixture.WaitForAdvisoryWaitersAsync(
                    database,
                    1,
                    "b52-application");

                var winnerId = Guid.NewGuid();
                await PersistWinningGraphAsync(
                    blockerConnection,
                    blockerTransaction,
                    winnerId,
                    participantId,
                    evidenceReference);
                await blockerTransaction.CommitAsync();
                blockerCommitted = true;

                var reconciled = await waiter.WaitAsync(TimeSpan.FromSeconds(15));
                reconciled.Id.ShouldBe(winnerId);
                reconciled.ReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.Confirmed);
                reconciled.ThresholdResult.ShouldBe(
                    AQGreenWeeklySalesThresholdResult.Met);

                await using var verificationConnection = new NpgsqlConnection(
                    database.ConnectionString("b53-weekly-sales-verification"));
                await verificationConnection.OpenAsync();
                await using var verificationContext = _fixture.CreateDbContext(
                    verificationConnection);
                var decisions = await verificationContext
                    .AQGreenWeeklySalesEligibilityDecisions
                    .AsNoTracking()
                    .Include(decision => decision.EvidenceReferences)
                    .Where(decision =>
                        decision.TenantId == 1 &&
                        decision.ParticipantId == participantId &&
                        decision.CommissionWeekStartUtc == WeekStartUtc &&
                        decision.SalesEligibilityRulesVersion ==
                        AQGreenWeeklySalesEligibilityRules.CurrentVersion)
                    .ToListAsync();
                var durable = decisions.ShouldHaveSingleItem();
                durable.Id.ShouldBe(winnerId);
                durable.ReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.Confirmed);
                durable.ThresholdResult.ShouldBe(
                    AQGreenWeeklySalesThresholdResult.Met);
                durable.EvidenceReferences.ShouldHaveSingleItem()
                    .TechnicalReference.ShouldBe(evidenceReference);
            }
            finally
            {
                if (!blockerCommitted)
                    await blockerTransaction.RollbackAsync();
                if (waiter != null && !waiter.IsCompleted)
                    await waiter.WaitAsync(TimeSpan.FromSeconds(15));
            }
        }

        [Fact]
        public async Task Reject_PersistsNoQuantitiesOrThreshold()
        {
            var result = await _service.RejectAsync(
                new RejectAQGreenWeeklySalesEligibilityInput
                {
                    TenantId = 1,
                    ParticipantId = P(62),
                    CommissionWeekStartUtc = WeekStartUtc,
                    RejectionReason = "evidence could not be verified",
                    EvidenceReferences = new List<string> { "ticket:reject" }
                });

            result.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Rejected);
            result.ReviewedSprayQuantity.ShouldBeNull();
            result.ReviewedOneLitreQuantity.ShouldBeNull();
            result.ReviewedFiveLitreQuantity.ShouldBeNull();
            result.ThresholdResult.ShouldBeNull();
        }

        [Fact]
        public async Task InvalidTargetParticipantAndRejectedFinalizationRollbackDurableState()
        {
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _service.BeginReviewAsync(new BeginAQGreenWeeklySalesReviewInput
                {
                    TenantId = 0,
                    ParticipantId = P(58),
                    CommissionWeekStartUtc = WeekStartUtc
                }));
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _service.BeginReviewAsync(new BeginAQGreenWeeklySalesReviewInput
                {
                    TenantId = 2,
                    ParticipantId = P(58),
                    CommissionWeekStartUtc = WeekStartUtc
                }));
            await Should.ThrowAsync<ArgumentException>(() =>
                _service.RejectAsync(new RejectAQGreenWeeklySalesEligibilityInput
                {
                    TenantId = 1,
                    ParticipantId = P(58),
                    CommissionWeekStartUtc = WeekStartUtc,
                    RejectionReason = " ",
                    EvidenceReferences = new List<string> { "ticket:rollback" }
                }));

            (await DecisionCountAsync(58)).ShouldBe(0);
            (await EvidenceCountAsync(58)).ShouldBe(0);
        }

        private static ConfirmAQGreenWeeklySalesEligibilityInput ConfirmInput(
            int participantNumber,
            int spray,
            int oneLitre,
            int fiveLitre,
            string evidenceReference) => new()
        {
            TenantId = 1,
            ParticipantId = P(participantNumber),
            CommissionWeekStartUtc = WeekStartUtc,
            SprayQuantity = spray,
            OneLitreQuantity = oneLitre,
            FiveLitreQuantity = fiveLitre,
            EvidenceReferences = new List<string> { evidenceReference }
        };

        private static async Task<int> DecisionCountAsync(int participantNumber)
        {
            await using var connection = new NpgsqlConnection(
                AQGreenGraduationPostgreSqlFailureState.Shared.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM "AQGreenWeeklySalesEligibilityDecisions"
                WHERE "TenantId" = 1 AND "ParticipantId" = @participantId;
                """,
                connection);
            command.Parameters.AddWithValue("participantId", P(participantNumber));
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private static async Task<int> EvidenceCountAsync(int participantNumber)
        {
            await using var connection = new NpgsqlConnection(
                AQGreenGraduationPostgreSqlFailureState.Shared.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM "AQGreenWeeklySalesEvidenceReferences" evidence
                INNER JOIN "AQGreenWeeklySalesEligibilityDecisions" decision
                    ON decision."TenantId" = evidence."TenantId"
                   AND decision."Id" = evidence."DecisionId"
                WHERE decision."TenantId" = 1
                  AND decision."ParticipantId" = @participantId;
                """,
                connection);
            command.Parameters.AddWithValue("participantId", P(participantNumber));
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private static async Task PersistWinningGraphAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid decisionId,
            Guid participantId,
            string evidenceReference)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO public."AQGreenWeeklySalesEligibilityDecisions" (
                    "Id", "TenantId", "ParticipantId", "CommissionWeekStartUtc",
                    "SalesEligibilityRulesVersion", "ReviewStatus", "CreationTime")
                VALUES (
                    @decisionId, 1, @participantId, @weekStart,
                    'AQGreenWeeklySalesEligibilityV1', 1, NOW());

                INSERT INTO public."AQGreenWeeklySalesEvidenceReferences" (
                    "Id", "TenantId", "DecisionId", "Source",
                    "TechnicalReference", "RecordedAt")
                VALUES (
                    @evidenceId, 1, @decisionId, 1,
                    @evidenceReference, @reviewedAt);

                UPDATE public."AQGreenWeeklySalesEligibilityDecisions"
                SET "ReviewStatus" = 2,
                    "ReviewedSprayQuantity" = 5,
                    "ReviewedOneLitreQuantity" = 5,
                    "ReviewedFiveLitreQuantity" = 5,
                    "ThresholdResult" = 1,
                    "ReviewedAt" = @reviewedAt,
                    "ReviewedByUserId" = 3001
                WHERE "Id" = @decisionId;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("decisionId", decisionId);
            command.Parameters.AddWithValue("participantId", participantId);
            command.Parameters.AddWithValue("weekStart", WeekStartUtc);
            command.Parameters.AddWithValue("evidenceId", Guid.NewGuid());
            command.Parameters.AddWithValue(
                "evidenceReference",
                evidenceReference);
            command.Parameters.AddWithValue(
                "reviewedAt",
                WeekStartUtc.AddDays(7));
            await command.ExecuteNonQueryAsync();
        }

        private static Guid P(int number) =>
            AQGreenPlacementAllocatorPostgreSqlFixture.Participant(1, number);
    }
}
