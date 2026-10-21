using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.TestBase;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenSalesInvalidationPostgreSqlTests
        : AbpIntegratedTestBase<AQGreenGraduationPostgreSqlApplicationTestModule>
    {
        private static readonly DateTime Week = new(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;
        private IAdminAQGreenWeeklySalesEligibilityAppService _reviews;
        private readonly IAdminCommissionAppService _payouts;
        private AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease Database =>
            AQGreenGraduationPostgreSqlApplicationTestModule.CurrentDatabase;

        public AQGreenSalesInvalidationPostgreSqlTests(AQGreenPlacementAllocatorPostgreSqlFixture fixture)
        {
            _fixture = fixture;
            AbpSession.TenantId = null;
            AbpSession.UserId = 3001;
            // Synthetic financial-enabled runtime; neither demo nor pilot bypasses the payout gate.
            var configuration = new ConfigurationBuilder()
                .AddConfiguration(Resolve<IConfiguration>())
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["App:FinancialOperations:Enabled"] = "true",
                    ["AQGreenV2Demo:Enabled"] = "false"
                }).Build();
            LocalIocManager.IocContainer.Register(Component.For<IConfiguration>()
                .Instance(configuration).Named("invalidation-test-configuration").IsDefault());
            _reviews = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();
            _payouts = Resolve<IAdminCommissionAppService>();
        }

        [Theory]
        [InlineData(WeeklyCommissionPayoutStatus.Earned)]
        [InlineData(WeeklyCommissionPayoutStatus.Released)]
        [InlineData(WeeklyCommissionPayoutStatus.Paid)]
        public async Task Invalidation_HoldsAllPayoutBoundaries_WithoutRewritingOriginalHistory(WeeklyCommissionPayoutStatus status)
        {
            var graph = await SeedCommissionAsync(status);
            var original = await OriginalFactsAsync();
            var visibleReview = await _reviews.GetAsync(new EntityDto<Guid>(graph.DecisionId));
            visibleReview.Evidence.Single().Id.ShouldBe(graph.EvidenceId);
            var record = await _reviews.InvalidateEvidenceAsync(Input(graph));
            record.DecisionId.ShouldBe(graph.DecisionId);
            record.EvidenceId.ShouldBe(graph.EvidenceId);
            record.RecordedByUserId.ShouldBe(3001);
            record.RecordedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
            record.Reason.ShouldBe("Synthetic sale was invalidated.");
            var persisted = (await _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId))).ShouldHaveSingleItem();
            persisted.Id.ShouldBe(record.Id);
            (await OriginalFactsAsync()).ShouldBe(original);

            var release = await Should.ThrowAsync<UserFriendlyException>(() => _payouts.ReleaseAsync(new ReleaseWeeklyEarningInput
                { Programme = AdminCommissionProgramme.Entry, Id = graph.CommissionId, Justification = "Synthetic release attempt" }));
            release.Message.ShouldContain("reconciliation");
            var payment = await Should.ThrowAsync<UserFriendlyException>(() => _payouts.RecordPaymentAsync(new RecordWeeklyEarningPaymentInput
                { Programme = AdminCommissionProgramme.Entry, Id = graph.CommissionId,
                    Justification = "Synthetic payment attempt", PaymentReference = "synthetic-record-only" }));
            payment.Message.ShouldContain("reconciliation");
            (await OriginalFactsAsync()).ShouldBe(original);
            var list = await _payouts.GetAllAsync(new AdminCommissionListInput { TenantId = 1, Programme = AdminCommissionProgramme.Entry });
            list.Items.Single(item => item.Id == graph.CommissionId).RequiresSalesEvidenceReconciliation.ShouldBeTrue();
        }

        [Fact]
        public async Task ConcurrentExactRetries_CreateOneRecord_AndConflictingRetryCannotReplaceIt()
        {
            var graph = await SeedCommissionAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 4)
                .Select(_ => _reviews.InvalidateEvidenceAsync(Input(graph))));
            results.Select(item => item.Id).Distinct().Count().ShouldBe(1);
            var conflict = Input(graph); conflict.Reason = "Different reason cannot replace the audit fact.";
            await Should.ThrowAsync<UserFriendlyException>(() => _reviews.InvalidateEvidenceAsync(conflict));
            (await _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId))).Count.ShouldBe(1);
        }

        [Fact]
        public async Task UnauthorizedHostAndTenantOrAreaScope_CannotInvalidateOrReadTheRecord()
        {
            var graph = await SeedCommissionAsync();
            var original = await OriginalFactsAsync();
            var checker = Substitute.For<IPermissionChecker>();
            checker.IsGrantedAsync(Arg.Any<string>()).Returns(Task.FromResult(false));
            LocalIocManager.IocContainer.Register(Component.For<IPermissionChecker>()
                .Instance(checker).Named("invalidation-denial-checker").IsDefault());
            _reviews = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();
            AbpSession.UserId = null;
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reviews.InvalidateEvidenceAsync(Input(graph)));
            AbpSession.UserId = 3001;
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reviews.InvalidateEvidenceAsync(Input(graph)));
            checker.IsGrantedAsync(AquaPermissions.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility).Returns(Task.FromResult(true));
            // Review permission alone does not grant Host AllTenants authority.
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reviews.InvalidateEvidenceAsync(Input(graph)));
            AbpSession.TenantId = 2;
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reviews.InvalidateEvidenceAsync(Input(graph)));
            var hidden = await Should.ThrowAsync<UserFriendlyException>(() => _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId)));
            hidden.Details.ShouldBe("Review not found.");
            AbpSession.TenantId = 1; // Existing fixture user has no Area administrator assignment.
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reviews.InvalidateEvidenceAsync(Input(graph)));
            AbpSession.TenantId = null;
            checker.IsGrantedAsync(AquaPermissions.Admin.AllTenants).Returns(Task.FromResult(true));
            (await _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId))).Count.ShouldBe(0);
            (await OriginalFactsAsync()).ShouldBe(original);
            await using (var connection = await ConnectAsync())
            await using (var context = _fixture.CreateDbContext(connection))
            {
                var area = await context.Areas.SingleAsync(item => item.TenantId == 1 && item.Code == "EAST");
                context.AreaAdminAssignments.Add(AqualLifeStyle.Domain.Areas.AreaAdminAssignment.Assign(area, 3001, 1, Week));
                await context.SaveChangesAsync();
            }
            AbpSession.TenantId = 1;
            checker.IsGrantedAsync(AquaPermissions.Admin.AllTenants).Returns(Task.FromResult(false));
            (await _reviews.InvalidateEvidenceAsync(Input(graph))).RecordedByUserId.ShouldBe(3001);
            (await _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId))).Count.ShouldBe(1);
        }

        [Fact]
        public async Task WrongEvidenceAndNonFinalReview_AreRejected_AndUnaffectedCommissionCanStillReleaseAndRecord()
        {
            // Both reviews precede the shared period's immutable calculation time.
            await _reviews.ConfirmAsync(new ConfirmAQGreenWeeklySalesEligibilityInput
            {
                TenantId = 1, ParticipantId = P(7), CommissionWeekStartUtc = Week,
                SprayQuantity = 5, OneLitreQuantity = 5, FiveLitreQuantity = 5,
                EvidenceReferences = new List<string> { "synthetic-invalidation-7" }
            });
            var graph = await SeedCommissionAsync();
            var wrong = Input(graph); wrong.EvidenceId = Guid.NewGuid();
            await Should.ThrowAsync<UserFriendlyException>(() => _reviews.InvalidateEvidenceAsync(wrong));
            await _reviews.BeginReviewAsync(new BeginAQGreenWeeklySalesReviewInput
                { TenantId = 1, ParticipantId = P(9), CommissionWeekStartUtc = Week });
            var held = Input(graph); held.ParticipantId = P(9);
            await Should.ThrowAsync<UserFriendlyException>(() => _reviews.InvalidateEvidenceAsync(held));
            await _reviews.InvalidateEvidenceAsync(Input(graph));
            var unaffected = await SeedCommissionAsync(participantNumber: 7);
            await _payouts.ReleaseAsync(new ReleaseWeeklyEarningInput
                { Programme = AdminCommissionProgramme.Entry, Id = unaffected.CommissionId, Justification = "Synthetic unaffected control" });
            await _payouts.RecordPaymentAsync(new RecordWeeklyEarningPaymentInput
                { Programme = AdminCommissionProgramme.Entry, Id = unaffected.CommissionId,
                    Justification = "Synthetic recorded control; no provider operation", PaymentReference = "synthetic-unaffected" });
            await using var connection = await ConnectAsync();
            await using var context = _fixture.CreateDbContext(connection);
            var paid = await context.EntryWeeklyCommissions.SingleAsync(item => item.Id == unaffected.CommissionId);
            paid.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.Paid);
            paid.TotalAmount.ShouldBe(150m);
            (await context.AQGreenSalesEvidenceInvalidations.CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task KnownInvalidEvidence_CannotBeUsedForANewCalculation()
        {
            var graph = await SeedCommissionAsync();
            await _reviews.InvalidateEvidenceAsync(Input(graph));
            using var uow = Resolve<IUnitOfWorkManager>().Begin();
            await Should.ThrowAsync<AQGreenWeeklySalesEligibilityUnavailableException>(() =>
                Resolve<IAQGreenWeeklySalesEligibilityDecisionReader>().GetFinalDecisionAsync(1, P(1), Week,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion));
        }

        [Fact]
        public async Task PayoutWaitingForInvalidationLock_RechecksCommittedHoldBeforeMutation()
        {
            var graph = await SeedCommissionAsync();
            var original = await OriginalFactsAsync();
            await using var connection = await ConnectAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            var provider = Substitute.For<Abp.EntityFrameworkCore.IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext().Returns(context);
            await new AQGreenWeeklySalesEligibilityMutationLock(provider).AcquireAsync(1, P(1), Week,
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            var pending = _payouts.ReleaseAsync(new ReleaseWeeklyEarningInput
                { Programme = AdminCommissionProgramme.Entry, Id = graph.CommissionId, Justification = "Synthetic lock waiter" });
            try
            {
                await _fixture.WaitForAdvisoryWaitersAsync(Database, 1, "b52-application");
                var decision = await context.AQGreenWeeklySalesEligibilityDecisions.Include(item => item.EvidenceReferences)
                    .SingleAsync(item => item.Id == graph.DecisionId);
                context.AQGreenSalesEvidenceInvalidations.Add(AQGreenSalesEvidenceInvalidation.Record(
                    decision, graph.EvidenceId, "Synthetic sale was invalidated.", 3001, DateTime.UtcNow));
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            finally { await transaction.DisposeAsync(); }
            var error = await Should.ThrowAsync<UserFriendlyException>(() => pending);
            error.Message.ShouldContain("reconciliation");
            (await OriginalFactsAsync()).ShouldBe(original);
        }

        [Fact]
        public async Task DatabaseConstraints_PreserveAuditFactsAndRefuseDestructiveDowngrade()
        {
            var graph = await SeedCommissionAsync();
            var record = await _reviews.InvalidateEvidenceAsync(Input(graph));
            foreach (var sql in new[]
            {
                "UPDATE public.\"AQGreenSalesEvidenceInvalidations\" SET \"Reason\" = 'changed'",
                "DELETE FROM public.\"AQGreenSalesEvidenceInvalidations\"",
                "TRUNCATE public.\"AQGreenSalesEvidenceInvalidations\"",
                "INSERT INTO public.\"AQGreenSalesEvidenceInvalidations\" SELECT gen_random_uuid(), \"TenantId\", \"DecisionId\", \"EvidenceId\", \"Reason\", \"RecordedByUserId\", \"RecordedAtUtc\" FROM public.\"AQGreenSalesEvidenceInvalidations\"",
                "INSERT INTO public.\"AQGreenSalesEvidenceInvalidations\" SELECT gen_random_uuid(), 2, \"DecisionId\", \"EvidenceId\", \"Reason\", \"RecordedByUserId\", \"RecordedAtUtc\" FROM public.\"AQGreenSalesEvidenceInvalidations\""
            })
            {
                await using var connection = await ConnectAsync();
                await using var command = new NpgsqlCommand(sql, connection);
                await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            }
            await using var downgradeConnection = await ConnectAsync();
            await using var context = _fixture.CreateDbContext(downgradeConnection);
            await Should.ThrowAsync<PostgresException>(() => context.GetService<IMigrator>()
                .MigrateAsync("20261005194901_AddIsolatedAQGreenPilotEvidence"));
            (await _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId))).Single().Id.ShouldBe(record.Id);
        }

        [Fact]
        public async Task EmptyAdditiveMigration_CanDowngradeAndReapplyWithoutChangingExistingFacts()
        {
            var graph = await SeedCommissionAsync();
            var original = await OriginalFactsAsync();
            await using var connection = await ConnectAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await context.GetService<IMigrator>().MigrateAsync("20261005194901_AddIsolatedAQGreenPilotEvidence");
            await context.GetService<IMigrator>().MigrateAsync();
            (await OriginalFactsAsync()).ShouldBe(original);
            (await _reviews.GetInvalidationsAsync(new EntityDto<Guid>(graph.DecisionId))).Count.ShouldBe(0);
        }

        private async Task<Graph> SeedCommissionAsync(WeeklyCommissionPayoutStatus status = WeeklyCommissionPayoutStatus.Earned,
            int participantNumber = 1)
        {
            // Existing synthetic allocator fixtures supply users, customers and Active participants.
            await _reviews.ConfirmAsync(new ConfirmAQGreenWeeklySalesEligibilityInput
            {
                TenantId = 1, ParticipantId = P(participantNumber), CommissionWeekStartUtc = Week,
                SprayQuantity = 5, OneLitreQuantity = 5, FiveLitreQuantity = 5,
                EvidenceReferences = new List<string> { "synthetic-invalidation-" + participantNumber }
            });
            await using var connection = await ConnectAsync();
            await using var context = _fixture.CreateDbContext(connection);
            await using var transaction = await context.Database.BeginTransactionAsync();
            var decision = await context.AQGreenWeeklySalesEligibilityDecisions.Include(item => item.EvidenceReferences)
                .SingleAsync(item => item.ParticipantId == P(participantNumber));
            // Complete the existing Active test fixture's explicit activation-time facts.
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.\"EntryParticipations\" SET \"ActivatedAt\" = {Week.AddDays(-2)} WHERE \"TenantId\" = 1 AND \"CustomerId\" >= {participantNumber} AND \"CustomerId\" < {participantNumber + 6}");
            var participants = await context.EntryParticipations.Where(item => item.TenantId == 1 &&
                item.CustomerId >= participantNumber && item.CustomerId < participantNumber + 6)
                .OrderBy(item => item.CustomerId).ToListAsync();
            var termsVersion = await context.EntryCommissionTermsVersions.SingleOrDefaultAsync();
            if (termsVersion == null)
            {
                termsVersion = EntryCommissionTermsVersion.Create("invalidation-test-rates", Week.AddDays(-7), 150m, 250m, 1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);
            }
            var terms = termsVersion.ToTerms();
            var period = await context.EntryCommissionPeriods.SingleOrDefaultAsync();
            if (period == null)
            {
                period = EntryCommissionPeriod.CreateClosedPeriod(1, Week, Week.AddDays(7).AddTicks(-1),
                    AQGreenCommissionWeek.TimeZoneId, decision.ReviewedAt.Value, terms);
                context.EntryCommissionPeriods.Add(period);
            }
            var scope = AQGreenPlacementTreeScope.Create(1);
            var root = AQGreenNetworkPlacement.CreateRoot(scope, participants[0].Id, Week.AddDays(-1), AQGreenPlacementRules.CurrentVersion);
            var placements = new List<AQGreenNetworkPlacement> { root };
            placements.AddRange(participants.Skip(1).Select((item, index) => AQGreenNetworkPlacement.CreateChild(
                root, item.Id, index + 1, root.PlacedAt, AQGreenPlacementRules.CurrentVersion)));
            context.AQGreenPlacementTreeScopes.Add(scope);
            context.AQGreenNetworkPlacements.AddRange(placements);
            var commission = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator()).CalculatePlacementV2(
                participants[0], period, terms, EntryNetworkLevel.Level1, EntryNetworkLevel.Level1, Array.Empty<EntryMonthlyObligation>());
            var structure = new AQGreenCommissionStructuralEvidenceResult(participants[0].Id, scope.Id, period.PeriodEnd,
                AQGreenStructuralCompletionLevel.Level1, 5, 0, 0, AQGreenPlacementRules.CurrentVersion,
                AQGreenStructuralQualificationRules.CurrentVersion, placements.Select((placement, ordinal) =>
                    new AQGreenCommissionStructuralEvidenceObservation
                    {
                        CanonicalOrdinal = ordinal, SourcePlacementId = placement.Id,
                        ParticipationStatusObserved = EntryParticipationStatus.Active,
                        ParticipationActivatedAtObserved = participants[ordinal].ActivatedAt,
                        CustomerIdObserved = participants[ordinal].CustomerId, CustomerTenantMatchedObserved = true,
                        CustomerIsActiveObserved = true, UserIdObserved = participants[ordinal].CustomerId + 3000,
                        UserTenantMatchedObserved = true, UserIsActiveObserved = true
                    }).ToArray());
            var snapshot = new AQGreenWeeklySalesEligibilitySnapshot(decision.Id, 1, participants[0].Id, Week,
                decision.SalesEligibilityRulesVersion, decision.ReviewStatus, 5, 5, 5, decision.ThresholdResult,
                decision.ReviewedAt.Value, decision.ReviewedByUserId.Value, null);
            if (status == WeeklyCommissionPayoutStatus.Released || status == WeeklyCommissionPayoutStatus.Paid)
                commission.ReleaseEligiblePayout(period.CalculatedAt.AddMinutes(1));
            if (status == WeeklyCommissionPayoutStatus.Paid)
                commission.MarkPaid(period.CalculatedAt.AddMinutes(2), "synthetic-settled");
            context.EntryWeeklyCommissions.Add(commission);
            context.AQGreenV2WeeklyCommissionEvidence.Add(AQGreenV2WeeklyCommissionEvidence.Capture(commission, period, structure, snapshot));
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new Graph(commission.Id, decision.Id, decision.EvidenceReferences.Single().Id, participants[0].Id);
        }

        private async Task<string> OriginalFactsAsync()
        {
            await using var connection = await ConnectAsync();
            // Includes ALL original review, evidence, component, calculation and payout facts, excludes the new sidecar.
            var tables = new[] { "AQGreenWeeklySalesEligibilityDecisions", "AQGreenWeeklySalesEvidenceReferences",
                "EntryWeeklyCommissions", "EntryCommissionComponents", "AQGreenV2WeeklyCommissionEvidence", "AQGreenV2WeeklyCommissionEvidenceNodes" };
            var values = new List<string>();
            foreach (var table in tables)
            {
                await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(row_data ORDER BY row_data::text)::text, '[]') FROM (SELECT to_jsonb(t) AS row_data FROM public.\"{table}\" t) facts", connection);
                values.Add((string)await command.ExecuteScalarAsync());
            }
            return string.Join("\n", values);
        }
        private async Task<NpgsqlConnection> ConnectAsync()
        {
            var connection = new NpgsqlConnection(Database.ConnectionString("invalidation-observer"));
            await connection.OpenAsync(); return connection;
        }
        private static Guid P(int n) => AQGreenPlacementAllocatorPostgreSqlFixture.Participant(1, n);
        private static InvalidateAQGreenSalesEvidenceInput Input(Graph graph) => new()
        {
            TenantId = 1, ParticipantId = graph.ParticipantId, CommissionWeekStartUtc = Week,
            EvidenceId = graph.EvidenceId, Reason = "Synthetic sale was invalidated."
        };
        private sealed record Graph(Guid CommissionId, Guid DecisionId, Guid EvidenceId, Guid ParticipantId);
    }
}
