using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abp;
using Abp.Authorization.Users;
using Abp.Events.Bus;
using Abp.Events.Bus.Entities;
using Abp.EntityFrameworkCore;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using Abp.TestBase;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.EntityFrameworkCore.Seed.Host;
using AqualLifeStyle.EntityFrameworkCore.Seed.Tenants;
using AqualLifeStyle.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class WeeklyCommissionCalculationPostgreSqlTests : IAsyncLifetime
    {
        private const string PostgresImage = "postgres:16-alpine";
        private const string PreviousMigration =
            "20260830204240_AddAQGreenWeeklySalesEligibility";
        private readonly string _containerName =
            $"weekly-commission-calculation-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName = $"weekly_commission_test_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public WeeklyCommissionCalculationPostgreSqlTests()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            await StartPostgreSqlContainerAsync();
            await CreateTestDatabaseAsync();
            await MigrateToLatestAsync();
        }

        public Task DisposeAsync()
        {
            return StopPostgreSqlContainerAsync();
        }

        [Fact]
        public async Task ClosedWeek_CalculationRoundTrips_AndDuplicatePeriodIsRejected()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync(suffix);
            var participationId = await SeedQualifiedEntryParticipationAsync(customerId, suffix);
            var periodStart = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = new DateTime(2026, 8, 13, 23, 59, 59, 999, DateTimeKind.Utc);

            await using (var context = CreateDbContext())
            {
                var participation = await context.EntryParticipations.SingleAsync(p => p.Id == participationId);
                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"test-entry-commission-2026-08",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m, 250m, 1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);

                var areaRecord = AreaActivationStateRecord.Record(
                    Guid.NewGuid(), 1, true,
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    null, "PostgreSQL weekly commission test baseline",
                    AreaActivationStateRecordKind.ObservedBaseline);
                context.AreaActivationStateRecords.Add(areaRecord);
                await context.SaveChangesAsync();

                var terms = termsVersion.ToTerms();
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1, periodStart, periodEnd, "Africa/Johannesburg",
                    periodEnd.AddMinutes(1), terms);
                context.EntryCommissionPeriods.Add(period);

                var network = await context.EntryParticipations
                    .Include(p => p.RecruiterCorrections)
                    .Where(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active)
                    .ToListAsync();
                CompleteInMemoryNetworkToLevelThree(
                    participation,
                    network,
                    suffix);
                var obligations = await context.EntryMonthlyObligations
                    .Where(o => network.Select(n => n.Id).Contains(o.EntryParticipationId))
                    .ToListAsync();

                var calculator = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator());
                var commission = calculator.Calculate(
                    participation, period, terms, network, obligations);

                context.EntryWeeklyCommissions.Add(commission);
                await context.SaveChangesAsync();
            }

            var firstPeriodCount = await CountAsync("EntryCommissionPeriods");
            var firstCommissionCount = await CountAsync("EntryWeeklyCommissions");
            firstPeriodCount.ShouldBe(1);
            firstCommissionCount.ShouldBe(1);

            await using (var context = CreateDbContext())
            {
                var existingPeriod = await context.EntryCommissionPeriods
                    .FirstOrDefaultAsync(p =>
                        p.TenantId == 1 &&
                        p.PeriodStart == periodStart &&
                        p.PeriodEnd == periodEnd);
                existingPeriod.ShouldNotBeNull();

                var persistedCommission = await context.EntryWeeklyCommissions
                    .Include(commission => commission.Components)
                    .SingleAsync();
                persistedCommission.HighestQualifiedNetworkLevel.ShouldBe(3);
                persistedCommission.HighestCommissionedLevel.ShouldBe(3);
                persistedCommission.StructuralModel.ShouldBe(
                    AQGreenCommissionStructuralModel.LegacyV1);
                persistedCommission.CommissionDecisionRulesVersion.ShouldBeNull();
                persistedCommission.TotalAmount.ShouldBe(1650m);
                persistedCommission.Components
                    .Select(component => component.Level)
                    .OrderBy(level => level)
                    .ShouldBe(new[] { 1, 2, 3 });
            }

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await CalculateAndPersistDuplicateRunAsync(periodStart, periodEnd);
            });

            var secondPeriodCount = await CountAsync("EntryCommissionPeriods");
            var secondCommissionCount = await CountAsync("EntryWeeklyCommissions");
            secondPeriodCount.ShouldBe(firstPeriodCount);
            secondCommissionCount.ShouldBe(firstCommissionCount);
        }

        [Fact]
        public async Task TenantScopedQuery_ExcludesCrossTenantFifthRecruitAndLedger()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync(suffix);
            var participationId = await SeedQualifiedEntryParticipationAsync(
                customerId,
                suffix,
                directRecruitCount: EntryNetworkQualificationEvaluator.BranchSize - 1);
            var rootCustomerId = await GetParticipationCustomerIdAsync(participationId);
            var crossTenantParticipationId = await SeedCrossTenantRecruitAsync(
                rootCustomerId,
                suffix);
            var periodStart = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = new DateTime(2026, 8, 13, 23, 59, 59, 999, DateTimeKind.Utc);

            await using (var context = CreateDbContext())
            {
                var network = await context.EntryParticipations
                    .Include(participation => participation.RecruiterCorrections)
                    .Where(participation =>
                        participation.TenantId == 1 &&
                        participation.Status == EntryParticipationStatus.Active)
                    .ToListAsync();
                network.Count.ShouldBe(5);
                network.ShouldNotContain(participation =>
                    participation.Id == crossTenantParticipationId);

                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"tenant-safe-{suffix}",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m,
                    250m,
                    1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    periodStart,
                    periodEnd,
                    "Africa/Johannesburg",
                    periodEnd.AddMinutes(1),
                    termsVersion.ToTerms());
                context.EntryCommissionPeriods.Add(period);

                var effectiveNetwork = EffectiveProgrammeNetwork.BuildAQGreen(
                    1,
                    network,
                    periodEnd);
                var obligations = await context.EntryMonthlyObligations
                    .Where(obligation => network
                        .Select(participation => participation.Id)
                        .Contains(obligation.EntryParticipationId))
                    .ToListAsync();
                var calculator = new EntryWeeklyCommissionCalculator(
                    new EntryNetworkQualificationEvaluator());
                var commissions = network
                    .Select(participation => calculator.Calculate(
                        participation,
                        period,
                        termsVersion.ToTerms(),
                        effectiveNetwork,
                        obligations))
                    .ToList();
                context.EntryWeeklyCommissions.AddRange(commissions);
                await context.SaveChangesAsync();
            }

            await using (var context = CreateDbContext())
            {
                var period = await context.EntryCommissionPeriods
                    .SingleAsync(item =>
                        item.TenantId == 1 &&
                        item.PeriodStart == periodStart &&
                        item.PeriodEnd == periodEnd);
                var commissions = await context.EntryWeeklyCommissions
                    .Where(item => item.CommissionPeriodId == period.Id)
                    .ToListAsync();
                commissions.Count.ShouldBe(5);
                commissions.All(item => item.TenantId == 1).ShouldBeTrue();
                commissions.ShouldNotContain(item =>
                    item.EntryParticipationId == crossTenantParticipationId);
                commissions.Single(item =>
                        item.EntryParticipationId == participationId)
                    .HighestQualifiedNetworkLevel.ShouldBe(0);
            }
        }

        [Fact]
        public async Task PlacementV2DecisionGraph_RoundTrips_AllowsPayoutLifecycle_AndRejectsDirectMutation()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync(suffix);
            var rootId = await SeedQualifiedEntryParticipationAsync(customerId, suffix);
            var week = AQGreenCommissionWeek.FromStartUtc(
                new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc));
            Guid commissionId;
            Guid periodId;
            int unrelatedCustomerId;

            await using (var context = CreateDbContext())
            {
                var participations = await context.EntryParticipations
                    .Where(item => item.Id == rootId ||
                                   item.RecruiterCustomerId == customerId)
                    .OrderBy(item => item.Id == rootId ? 0 : 1)
                    .ThenBy(item => item.Id)
                    .ToListAsync();
                participations.Count.ShouldBe(6);
                unrelatedCustomerId = participations[1].CustomerId;
                var customerIds = participations.Select(item => item.CustomerId).ToList();
                var customers = await context.Customers
                    .Where(item => customerIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id);

                var scope = AQGreenPlacementTreeScope.Create(1);
                var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                    scope,
                    rootId,
                    new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    AQGreenPlacementRules.CurrentVersion);
                var placements = new List<AQGreenNetworkPlacement> { rootPlacement };
                placements.AddRange(participations.Skip(1).Select((participation, index) =>
                    AQGreenNetworkPlacement.CreateChild(
                        rootPlacement,
                        participation.Id,
                        index + 1,
                        rootPlacement.PlacedAt,
                        AQGreenPlacementRules.CurrentVersion)));
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.AddRange(placements);

                var salesDecision = AQGreenWeeklySalesEligibilityDecision.Begin(
                    1,
                    rootId,
                    week,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                context.AQGreenWeeklySalesEligibilityDecisions.Add(salesDecision);

                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"pg-v2-terms-{suffix}",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m,
                    250m,
                    1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);
                await context.SaveChangesAsync();

                salesDecision.AddManualEvidence(
                    $"pg-v2-{suffix}",
                    week.EndExclusiveUtc);
                await context.SaveChangesAsync();
                salesDecision.Confirm(
                    new AQGreenWeeklySalesQuantities(5, 5, 5),
                    1,
                    week.EndExclusiveUtc);
                await context.SaveChangesAsync();

                var terms = termsVersion.ToTerms();
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    week.StartUtc,
                    week.EndExclusiveUtc.AddTicks(-1),
                    AQGreenCommissionWeek.TimeZoneId,
                    week.EndExclusiveUtc,
                    terms);
                var calculator = new EntryWeeklyCommissionCalculator(
                    new EntryNetworkQualificationEvaluator());
                var commission = calculator.CalculatePlacementV2(
                    participations[0],
                    period,
                    terms,
                    EntryNetworkLevel.Level1,
                    EntryNetworkLevel.Level1,
                    Array.Empty<EntryMonthlyObligation>());
                var structuralEvidence = new AQGreenCommissionStructuralEvidenceResult(
                    rootId,
                    scope.Id,
                    period.PeriodEnd,
                    AQGreenStructuralCompletionLevel.Level1,
                    5,
                    0,
                    0,
                    AQGreenPlacementRules.CurrentVersion,
                    AQGreenStructuralQualificationRules.CurrentVersion,
                    placements.Select((placement, ordinal) =>
                    {
                        var participation = participations[ordinal];
                        var customer = customers[participation.CustomerId];
                        return new AQGreenCommissionStructuralEvidenceObservation
                        {
                            CanonicalOrdinal = ordinal,
                            SourcePlacementId = placement.Id,
                            ParticipationStatusObserved = participation.Status,
                            ParticipationActivatedAtObserved = participation.ActivatedAt,
                            CustomerIdObserved = customer.Id,
                            CustomerTenantMatchedObserved = true,
                            CustomerIsActiveObserved = customer.IsActive,
                            UserIdObserved = customer.UserId,
                            UserTenantMatchedObserved = true,
                            UserIsActiveObserved = true
                        };
                    }).ToList());
                var salesSnapshot = new AQGreenWeeklySalesEligibilitySnapshot(
                    salesDecision.Id,
                    1,
                    rootId,
                    week.StartUtc,
                    salesDecision.SalesEligibilityRulesVersion,
                    salesDecision.ReviewStatus,
                    salesDecision.ReviewedSprayQuantity,
                    salesDecision.ReviewedOneLitreQuantity,
                    salesDecision.ReviewedFiveLitreQuantity,
                    salesDecision.ThresholdResult,
                    salesDecision.ReviewedAt.Value,
                    salesDecision.ReviewedByUserId.Value,
                    salesDecision.RejectionReason);
                var evidence = AQGreenV2WeeklyCommissionEvidence.Capture(
                    commission,
                    period,
                    structuralEvidence,
                    salesSnapshot);
                context.EntryCommissionPeriods.Add(period);
                context.EntryWeeklyCommissions.Add(commission);
                context.AQGreenV2WeeklyCommissionEvidence.Add(evidence);
                await context.SaveChangesAsync();
                commissionId = commission.Id;
                periodId = period.Id;
            }

            await using (var context = CreateDbContext())
            {
                var commission = await context.EntryWeeklyCommissions
                    .Include(item => item.Components)
                    .SingleAsync(item => item.Id == commissionId);
                commission.StructuralModel.ShouldBe(
                    AQGreenCommissionStructuralModel.PlacementV2);
                commission.HighestQualifiedNetworkLevel.ShouldBe(1);
                commission.HighestCommissionedLevel.ShouldBe(1);
                commission.TotalAmount.ShouldBe(150m);
                (await context.AQGreenV2WeeklyCommissionEvidence
                    .Include(item => item.Nodes)
                    .SingleAsync(item => item.Id == commissionId))
                    .Nodes.Count.ShouldBe(6);

                var provider = Substitute.For<
                    IDbContextProvider<AqualLifeStyleDbContext>>();
                provider.GetDbContext().Returns(context);
                var replay = await new AQGreenV2WeeklyCommissionEvidenceReplayValidator(
                        provider)
                    .ValidateAsync(commissionId);
                replay.QualifiedStructuralLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level1);
                replay.CommissionedLevel.ShouldBe(1);
                replay.TotalAmount.ShouldBe(150m);
                replay.EvidenceNodeCount.ShouldBe(6);

                var alwaysGraphTriggerCount = await context.Database
                    .SqlQueryRaw<int>(
                        """
                        SELECT COUNT(*)::integer AS "Value"
                        FROM pg_catalog.pg_trigger
                        WHERE tgname IN (
                            'TR_EntryWeeklyCommissions_ValidateV2Evidence',
                            'TR_AQGreenV2CommissionEvidence_ValidateGraph',
                            'TR_AQGreenV2CommissionEvidenceNodes_ValidateGraph',
                            'TR_EntryCommissionComponents_ValidateV2Evidence')
                          AND tgenabled = 'A'
                        """)
                    .SingleAsync();
                alwaysGraphTriggerCount.ShouldBe(4);

                commission.ReleaseEligiblePayout(week.EndExclusiveUtc.AddHours(1));
                await context.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryWeeklyCommissions\" SET \"TotalAmount\" = 999 WHERE \"Id\" = {commissionId}");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryWeeklyCommissions\" SET \"CustomerId\" = {unrelatedCustomerId} WHERE \"Id\" = {commissionId}");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"AQGreenV2WeeklyCommissionEvidenceNodes\" WHERE \"EvidenceId\" = {commissionId} AND \"CanonicalOrdinal\" = 1");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryCommissionPeriods\" SET \"PeriodEnd\" = \"PeriodEnd\" - interval '1 second' WHERE \"Id\" = {periodId}");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryWeeklyCommissions\" SET \"CreationTime\" = \"CreationTime\" + interval '1 second' WHERE \"Id\" = {commissionId}");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryCommissionComponents\" SET \"Amount\" = 999 WHERE \"EntryWeeklyCommissionId\" = {commissionId}");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"AQGreenV2WeeklyCommissionEvidence\" SET \"Cutoff\" = \"Cutoff\" - interval '1 second' WHERE \"EntryWeeklyCommissionId\" = {commissionId}");
            });
            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.Database.ExecuteSqlRawAsync(
                    "TRUNCATE TABLE \"AQGreenV2WeeklyCommissionEvidenceNodes\"");
            });

            await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var context = CreateDbContext();
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            });
            await using (var context = CreateDbContext())
            {
                (await context.Database.GetAppliedMigrationsAsync()).ShouldContain(
                    "20260831230903_AddAQGreenV2CommissionConsumption");
                (await context.AQGreenV2WeeklyCommissionEvidence.CountAsync())
                    .ShouldBe(1);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task PlacementV2DecisionGraph_RejectsForgedQualifiedLevelWithoutDescendants(
            int forgedLevel)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync($"forged-{suffix}");
            var rootId = await SeedQualifiedEntryParticipationAsync(
                customerId,
                $"forged-{suffix}",
                directRecruitCount: 0);
            var week = AQGreenCommissionWeek.FromStartUtc(
                new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc));

            await using var context = CreateDbContext();
            var participation = await context.EntryParticipations
                .SingleAsync(item => item.Id == rootId);
            var customer = await context.Customers
                .SingleAsync(item => item.Id == customerId);
            var scope = AQGreenPlacementTreeScope.Create(1);
            var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                scope,
                rootId,
                new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                AQGreenPlacementRules.CurrentVersion);
            var salesDecision = AQGreenWeeklySalesEligibilityDecision.Begin(
                1,
                rootId,
                week,
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            var termsVersion = EntryCommissionTermsVersion.Create(
                $"pg-forged-{suffix}",
                new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                150m,
                250m,
                1250m);
            context.AQGreenPlacementTreeScopes.Add(scope);
            context.AQGreenNetworkPlacements.Add(rootPlacement);
            context.AQGreenWeeklySalesEligibilityDecisions.Add(salesDecision);
            context.EntryCommissionTermsVersions.Add(termsVersion);
            await context.SaveChangesAsync();

            salesDecision.AddManualEvidence(
                $"pg-forged-sales-{suffix}",
                week.EndExclusiveUtc);
            await context.SaveChangesAsync();
            salesDecision.Confirm(
                new AQGreenWeeklySalesQuantities(5, 5, 5),
                1,
                week.EndExclusiveUtc);
            await context.SaveChangesAsync();

            var terms = termsVersion.ToTerms();
            var period = EntryCommissionPeriod.CreateClosedPeriod(
                1,
                week.StartUtc,
                week.EndExclusiveUtc.AddTicks(-1),
                AQGreenCommissionWeek.TimeZoneId,
                week.EndExclusiveUtc,
                terms);
            var commission = new EntryWeeklyCommissionCalculator(
                    new EntryNetworkQualificationEvaluator())
                .CalculatePlacementV2(
                    participation,
                    period,
                    terms,
                    (EntryNetworkLevel)forgedLevel,
                    (EntryNetworkLevel)forgedLevel,
                    Array.Empty<EntryMonthlyObligation>());
            context.EntryCommissionPeriods.Add(period);
            await context.SaveChangesAsync();

            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "EntryWeeklyCommissions" (
                    "Id", "TenantId", "EntryParticipationId", "CustomerId",
                    "CommissionPeriodId", "HighestCompletedLevel", "TotalAmount",
                    "Currency", "RulesVersion", "CalculatedAt", "PayoutStatus",
                    "CreationTime", "CreatorUserId", "IsDeleted",
                    "StructuralModel", "CommissionDecisionRulesVersion")
                VALUES ({commission.Id}, 1, {rootId}, {customerId}, {period.Id},
                        {forgedLevel}, {commission.TotalAmount}, {commission.Currency},
                        {commission.RulesVersion}, {commission.CalculatedAt}, 1,
                        {week.EndExclusiveUtc}, {commission.CreatorUserId}, false,
                        2, {AQGreenCommissionDecisionRules.CurrentVersion})
                """);
            foreach (var component in commission.Components)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "EntryCommissionComponents" (
                        "Id", "Level", "Amount", "EntryWeeklyCommissionId")
                    VALUES ({component.Id}, {component.Level}, {component.Amount},
                            {commission.Id})
                    """);
            }
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AQGreenV2WeeklyCommissionEvidence" (
                    "EntryWeeklyCommissionId", "TenantId", "EntryParticipationId",
                    "WeeklySalesEligibilityDecisionId", "PlacementTreeScopeId",
                    "Cutoff", "PlacementRulesVersion",
                    "StructuralQualificationRulesVersion",
                    "SalesEligibilityRulesVersion", "CommissionDecisionRulesVersion",
                    "EvidenceSchemaVersion", "QualifiedStructuralLevel",
                    "CommissionedLevel", "QualifyingDepth1Count",
                    "QualifyingDepth2Count", "QualifyingDepth3Count",
                    "SalesApplicability", "SalesReviewStatus", "SalesThresholdResult",
                    "SalesReviewedAt", "SalesReviewedByUserId", "EvidenceNodeCount")
                VALUES ({commission.Id}, 1, {rootId}, {salesDecision.Id}, {scope.Id},
                        {period.PeriodEnd}, {AQGreenPlacementRules.CurrentVersion},
                        {AQGreenStructuralQualificationRules.CurrentVersion},
                        {AQGreenWeeklySalesEligibilityRules.CurrentVersion},
                        {AQGreenCommissionDecisionRules.CurrentVersion},
                        {AQGreenV2WeeklyCommissionEvidenceSchema.CurrentVersion},
                        {forgedLevel}, {forgedLevel}, 0, 0, 0, 2, 2, 1,
                        {week.EndExclusiveUtc}, 1, 1)
                """);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AQGreenV2WeeklyCommissionEvidenceNodes" (
                    "EvidenceId", "CanonicalOrdinal", "TenantId", "SourcePlacementId",
                    "ParticipationStatusObserved", "ParticipationActivatedAtObserved",
                    "ParticipationIsDeletedObserved", "CustomerIdObserved",
                    "CustomerTenantMatchedObserved", "CustomerIsActiveObserved",
                    "CustomerIsDeletedObserved", "UserIdObserved",
                    "UserTenantMatchedObserved", "UserIsActiveObserved",
                    "UserIsDeletedObserved")
                VALUES ({commission.Id}, 0, 1, {rootPlacement.Id},
                        {(int)participation.Status}, {participation.ActivatedAt}, false,
                        {customer.Id}, true, {customer.IsActive}, false,
                        {customer.UserId}, true, true, false)
                """);

            var exception = await Should.ThrowAsync<PostgresException>(
                () => transaction.CommitAsync());
            exception.Message.ShouldContain("qualified structural level conflicts");

            await using var verificationContext = CreateDbContext();
            (await verificationContext.EntryWeeklyCommissions
                .IgnoreQueryFilters()
                .AnyAsync(item => item.Id == commission.Id))
                .ShouldBeFalse();
        }

        [Fact]
        public async Task PlacementV2DecisionGraph_AcceptsLevel0WithoutSalesEvidence()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync($"level0-{suffix}");
            var rootId = await SeedQualifiedEntryParticipationAsync(
                customerId,
                $"level0-{suffix}",
                directRecruitCount: 0);
            var week = AQGreenCommissionWeek.FromStartUtc(
                new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc));

            Guid commissionId;
            await using (var context = CreateDbContext())
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == rootId);
                var customer = await context.Customers
                    .SingleAsync(item => item.Id == customerId);
                var scope = AQGreenPlacementTreeScope.Create(1);
                var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                    scope,
                    rootId,
                    new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    AQGreenPlacementRules.CurrentVersion);
                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"pg-level0-{suffix}",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m,
                    250m,
                    1250m);
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(rootPlacement);
                context.EntryCommissionTermsVersions.Add(termsVersion);
                await context.SaveChangesAsync();

                var terms = termsVersion.ToTerms();
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    week.StartUtc,
                    week.EndExclusiveUtc.AddTicks(-1),
                    AQGreenCommissionWeek.TimeZoneId,
                    week.EndExclusiveUtc,
                    terms);
                var commission = new EntryWeeklyCommissionCalculator(
                        new EntryNetworkQualificationEvaluator())
                    .CalculatePlacementV2(
                        participation,
                        period,
                        terms,
                        EntryNetworkLevel.None,
                        EntryNetworkLevel.None,
                        Array.Empty<EntryMonthlyObligation>());
                var structuralEvidence = new AQGreenCommissionStructuralEvidenceResult(
                    rootId,
                    scope.Id,
                    period.PeriodEnd,
                    AQGreenStructuralCompletionLevel.Level0,
                    0,
                    0,
                    0,
                    AQGreenPlacementRules.CurrentVersion,
                    AQGreenStructuralQualificationRules.CurrentVersion,
                    new[]
                    {
                        new AQGreenCommissionStructuralEvidenceObservation
                        {
                            CanonicalOrdinal = 0,
                            SourcePlacementId = rootPlacement.Id,
                            ParticipationStatusObserved = participation.Status,
                            ParticipationActivatedAtObserved = participation.ActivatedAt,
                            CustomerIdObserved = customer.Id,
                            CustomerTenantMatchedObserved = true,
                            CustomerIsActiveObserved = customer.IsActive,
                            UserIdObserved = customer.UserId,
                            UserTenantMatchedObserved = true,
                            UserIsActiveObserved = true
                        }
                    });
                var evidence = AQGreenV2WeeklyCommissionEvidence.Capture(
                    commission,
                    period,
                    structuralEvidence,
                    null);
                context.EntryCommissionPeriods.Add(period);
                context.EntryWeeklyCommissions.Add(commission);
                context.AQGreenV2WeeklyCommissionEvidence.Add(evidence);
                await context.SaveChangesAsync();
                commissionId = commission.Id;
            }

            await using var verificationContext = CreateDbContext();
            var persistedCommission = await verificationContext.EntryWeeklyCommissions
                .Include(item => item.Components)
                .SingleAsync(item => item.Id == commissionId);
            persistedCommission.HighestQualifiedNetworkLevel.ShouldBe(0);
            persistedCommission.HighestCommissionedLevel.ShouldBe(0);
            persistedCommission.TotalAmount.ShouldBe(0m);
            persistedCommission.PayoutStatus.ShouldBe(
                WeeklyCommissionPayoutStatus.NotEarned);
            persistedCommission.Components.ShouldBeEmpty();
            var persistedEvidence = await verificationContext
                .AQGreenV2WeeklyCommissionEvidence
                .SingleAsync(item => item.Id == commissionId);
            persistedEvidence.QualifiedStructuralLevel.ShouldBe(
                AQGreenStructuralCompletionLevel.Level0);
            persistedEvidence.CommissionedLevel.ShouldBe(0);
            persistedEvidence.SalesApplicability.ShouldBe(
                AQGreenWeeklySalesApplicability.NotApplicable);
            persistedEvidence.WeeklySalesEligibilityDecisionId.ShouldBeNull();
            persistedEvidence.SalesReviewStatus.ShouldBeNull();
            persistedEvidence.SalesThresholdResult.ShouldBeNull();
            persistedEvidence.SalesReviewedAt.ShouldBeNull();
            persistedEvidence.SalesReviewedByUserId.ShouldBeNull();
        }

        [Fact]
        public async Task Migration_PreservesLegacyRows_DefaultsOldWriters_AndRollsBackSafely()
        {
            await using (var context = CreateDbContext())
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync(suffix);
            var participationId = await SeedQualifiedEntryParticipationAsync(
                customerId,
                suffix,
                directRecruitCount: 0);
            var commissionId = Guid.NewGuid();
            await using (var context = CreateDbContext())
            {
                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"legacy-migration-{suffix}",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m,
                    250m,
                    1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 13, 21, 59, 59, 999, DateTimeKind.Utc),
                    AQGreenCommissionWeek.TimeZoneId,
                    new DateTime(2026, 8, 13, 22, 0, 0, DateTimeKind.Utc),
                    termsVersion.ToTerms());
                context.EntryCommissionPeriods.Add(period);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO public."EntryWeeklyCommissions"
                        ("Id", "TenantId", "EntryParticipationId", "CustomerId",
                         "CommissionPeriodId", "HighestCompletedLevel", "TotalAmount",
                         "Currency", "RulesVersion", "CalculatedAt", "PayoutStatus",
                         "IsDeleted", "CreationTime")
                    VALUES
                        ({commissionId}, {1}, {participationId}, {customerId},
                         {period.Id}, {0}, {0m}, {"ZAR"}, {termsVersion.Version},
                         {period.CalculatedAt}, {0}, {false}, {period.CalculatedAt})
                    """);
                await context.GetService<IMigrator>().MigrateAsync();
            }

            await using (var context = CreateDbContext())
            {
                var migrated = await context.EntryWeeklyCommissions
                    .SingleAsync(item => item.Id == commissionId);
                migrated.StructuralModel.ShouldBe(
                    AQGreenCommissionStructuralModel.LegacyV1);
                migrated.CommissionDecisionRulesVersion.ShouldBeNull();

                var secondPeriod = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    new DateTime(2026, 8, 13, 22, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 20, 21, 59, 59, 999, DateTimeKind.Utc),
                    AQGreenCommissionWeek.TimeZoneId,
                    new DateTime(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc),
                    (await context.EntryCommissionTermsVersions.SingleAsync()).ToTerms());
                context.EntryCommissionPeriods.Add(secondPeriod);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO public."EntryWeeklyCommissions"
                        ("Id", "TenantId", "EntryParticipationId", "CustomerId",
                         "CommissionPeriodId", "HighestCompletedLevel", "TotalAmount",
                         "Currency", "RulesVersion", "CalculatedAt", "PayoutStatus",
                         "IsDeleted", "CreationTime")
                    VALUES
                        ({Guid.NewGuid()}, {1}, {participationId}, {customerId},
                         {secondPeriod.Id}, {0}, {0m}, {"ZAR"},
                         {(await context.EntryCommissionTermsVersions.SingleAsync()).Version},
                         {secondPeriod.CalculatedAt}, {0}, {false},
                         {secondPeriod.CalculatedAt})
                    """);
                (await context.EntryWeeklyCommissions.CountAsync()).ShouldBe(2);
                (await context.EntryWeeklyCommissions.AllAsync(item =>
                    item.StructuralModel == AQGreenCommissionStructuralModel.LegacyV1 &&
                    item.CommissionDecisionRulesVersion == null)).ShouldBeTrue();

                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            }

            await using var connection = new NpgsqlConnection(BuildTestConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'EntryWeeklyCommissions'
                  AND column_name IN ('StructuralModel', 'CommissionDecisionRulesVersion')
                """,
                connection);
            Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(0);
        }

        private async Task StartPostgreSqlContainerAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments =
                    $"run -d --name {_containerName} -e POSTGRES_DB=postgres -e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle -p {_hostPort}:5432 {PostgresImage}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Docker process for PostgreSQL.");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Docker run failed: {error}");
            }

            TraceLine($"Started PostgreSQL container: {output.Trim()} on port {_hostPort}");

            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(BuildAdminConnectionString());
                    await connection.OpenAsync();
                    TraceLine("PostgreSQL is ready.");
                    return;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }

            throw new InvalidOperationException("PostgreSQL container did not become ready in time.");
        }

        private async Task StopPostgreSqlContainerAsync()
        {
            var stopInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"rm -f {_containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(stopInfo);
            if (process == null)
            {
                return;
            }

            await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            TraceLine($"Stopped PostgreSQL container: {_containerName}");
        }

        private string BuildAdminConnectionString() =>
            $"Host=localhost;Port={_hostPort};Database=postgres;Username=aqualifestyle;Password=aqualifestyle";

        private string BuildTestConnectionString() =>
            $"Host=localhost;Port={_hostPort};Database={_databaseName};Username=aqualifestyle;Password=aqualifestyle";

        private AqualLifeStyleDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
            optionsBuilder.UseNpgsql(BuildTestConnectionString());
            return new AqualLifeStyleDbContext(optionsBuilder.Options);
        }

        private async Task CreateTestDatabaseAsync()
        {
            await using var connection = new NpgsqlConnection(BuildAdminConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""CREATE DATABASE "{_databaseName}" WITH OWNER = aqualifestyle;""";
            await command.ExecuteNonQueryAsync();
            TraceLine($"Created test database: {_databaseName}");
        }

        private async Task MigrateToLatestAsync()
        {
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync();

            var defaultTenant = context.Tenants.IgnoreQueryFilters().FirstOrDefault(t => t.TenancyName == "Default");
            if (defaultTenant == null)
            {
                defaultTenant = new Tenant("Default", "Default");
                context.Tenants.Add(defaultTenant);
                await context.SaveChangesAsync();
            }

            TraceLine("Migrated to latest.");
        }

        private async Task<long> CountAsync(string table)
        {
            await using var connection = new NpgsqlConnection(BuildTestConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"""SELECT COUNT(*) FROM "{table}" """, connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        private async Task<int> SeedCustomerAndUserAsync(string suffix)
        {
            await using var context = CreateDbContext();
            var userName = $"wc-{suffix}";
            var user = new User
            {
                TenantId = 1,
                UserName = userName,
                EmailAddress = $"{userName}@t.test",
                Name = "Weekly",
                Surname = "Commission",
                IsEmailConfirmed = true,
                IsActive = true
            };
            user.SetNormalizedNames();
            var passwordHasher = new PasswordHasher<User>(
                new OptionsWrapper<PasswordHasherOptions>(
                    new PasswordHasherOptions()));
            user.Password = passwordHasher.HashPassword(user, User.DefaultPassword);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var customer = Customer.Create(
                1,
                user.Id,
                $"Weekly Commission Member {suffix}",
                new AqualLifeStyle.Domain.Common.EmailAddress($"{userName}@t.test"));
            context.Customers.Add(customer);

            var adminRole = context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.TenantId == 1 && r.Name == StaticRoleNames.Tenants.Admin);
            if (adminRole != null)
            {
                context.UserRoles.Add(new UserRole(1, user.Id, adminRole.Id));
            }

            await context.SaveChangesAsync();
            return customer.Id;
        }

        private async Task<Guid> SeedQualifiedEntryParticipationAsync(
            int customerId,
            string suffix,
            int directRecruitCount = EntryNetworkQualificationEvaluator.BranchSize)
        {
            await using var context = CreateDbContext();
            var terms = EntryProgrammeTerms.Create(
                $"entry-2026-08-{suffix}",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

            var mainCustomer = await context.Customers.SingleAsync(c => c.Id == customerId);
            var participation = EntryParticipation.StartIndependently(
                1,
                mainCustomer.Id,
                terms,
                new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

            var registrationPayment = MemberPayment.CreatePending(
                1,
                mainCustomer.Id,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                "Test",
                $"commission-registration-{suffix}",
                new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc));
            registrationPayment.Confirm(new DateTime(2026, 8, 1, 9, 2, 0, DateTimeKind.Utc));
            participation.ApplyConfirmedActivationPayment(registrationPayment);

            var activationPayment = MemberPayment.CreatePending(
                1,
                mainCustomer.Id,
                MemberPaymentPurpose.EntryActivation,
                600m,
                "Test",
                $"commission-activation-{suffix}",
                new DateTime(2026, 8, 1, 9, 3, 0, DateTimeKind.Utc));
            activationPayment.Confirm(new DateTime(2026, 8, 1, 9, 4, 0, DateTimeKind.Utc));
            participation.ApplyConfirmedActivationPayment(activationPayment);
            participation.ApproveByAdministrator(1, new DateTime(2026, 8, 1, 9, 5, 0, DateTimeKind.Utc));

            context.MemberPayments.AddRange(registrationPayment, activationPayment);
            context.EntryParticipations.Add(participation);
            await context.SaveChangesAsync();

            for (var index = 0; index < directRecruitCount; index++)
            {
                var recruitUserName = $"wc-r{index}-{suffix}";
                var recruitUser = new User
                {
                    TenantId = 1,
                    UserName = recruitUserName,
                    EmailAddress = $"{recruitUserName}@t.test",
                    Name = "Recruit",
                    Surname = index.ToString(),
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                recruitUser.SetNormalizedNames();
                var recruitPasswordHasher = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(
                        new PasswordHasherOptions()));
                recruitUser.Password = recruitPasswordHasher.HashPassword(recruitUser, User.DefaultPassword);
                context.Users.Add(recruitUser);
                await context.SaveChangesAsync();

                var recruitCustomer = Customer.Create(
                    1,
                    recruitUser.Id,
                    $"Weekly Commission Recruit {index}",
                    new AqualLifeStyle.Domain.Common.EmailAddress($"{recruitUserName}@t.test"));
                context.Customers.Add(recruitCustomer);
                await context.SaveChangesAsync();

                var recruit = EntryParticipation.StartUnderRecruiter(
                    1,
                    recruitCustomer.Id,
                    participation,
                    terms,
                    new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

                var recruitRegistration = MemberPayment.CreatePending(
                    1,
                    recruitCustomer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    600m,
                    "Test",
                    $"commission-recruit-registration-{index}-{suffix}",
                    new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc));
                recruitRegistration.Confirm(new DateTime(2026, 8, 1, 9, 2, 0, DateTimeKind.Utc));
                recruit.ApplyConfirmedActivationPayment(recruitRegistration);

                var recruitActivation = MemberPayment.CreatePending(
                    1,
                    recruitCustomer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    600m,
                    "Test",
                    $"commission-recruit-activation-{index}-{suffix}",
                    new DateTime(2026, 8, 1, 9, 3, 0, DateTimeKind.Utc));
                recruitActivation.Confirm(new DateTime(2026, 8, 1, 9, 4, 0, DateTimeKind.Utc));
                recruit.ApplyConfirmedActivationPayment(recruitActivation);
                recruit.ApproveByAdministrator(1, new DateTime(2026, 8, 1, 9, 5, 0, DateTimeKind.Utc));

                context.MemberPayments.AddRange(recruitRegistration, recruitActivation);
                context.EntryParticipations.Add(recruit);
                await context.SaveChangesAsync();
            }

            return participation.Id;
        }

        private async Task<int> GetParticipationCustomerIdAsync(Guid participationId)
        {
            await using var context = CreateDbContext();
            return await context.EntryParticipations
                .Where(participation => participation.Id == participationId)
                .Select(participation => participation.CustomerId)
                .SingleAsync();
        }

        private async Task<Guid> SeedCrossTenantRecruitAsync(
            int recruiterCustomerId,
            string suffix)
        {
            await using var context = CreateDbContext();
            var tenant = new Tenant($"OtherTenant{suffix}", $"Other Tenant {suffix}");
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();

            var userName = $"wc-cross-{suffix}";
            var user = new User
            {
                TenantId = tenant.Id,
                UserName = userName,
                EmailAddress = $"{userName}@t.test",
                Name = "Cross",
                Surname = "Tenant",
                IsEmailConfirmed = true,
                IsActive = true
            };
            user.SetNormalizedNames();
            var passwordHasher = new PasswordHasher<User>(
                new OptionsWrapper<PasswordHasherOptions>(
                    new PasswordHasherOptions()));
            user.Password = passwordHasher.HashPassword(user, User.DefaultPassword);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var customer = Customer.Create(
                tenant.Id,
                user.Id,
                $"Cross Tenant Recruit {suffix}",
                new AqualLifeStyle.Domain.Common.EmailAddress($"{userName}@t.test"));
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                $"entry-cross-tenant-{suffix}",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                1200m,
                600m,
                7);
            var participation = EntryParticipation.StartIndependently(
                tenant.Id,
                customer.Id,
                terms,
                new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

            // Simulate an already-corrupt stored relationship. New domain and
            // application paths reject this cross-tenant placement before it is set.
            var recruiterProperty = typeof(EntryParticipation)
                .GetProperty(nameof(EntryParticipation.RecruiterCustomerId));
            recruiterProperty.ShouldNotBeNull();
            recruiterProperty.SetValue(participation, recruiterCustomerId);

            var payment = MemberPayment.CreatePending(
                tenant.Id,
                customer.Id,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "Test",
                $"commission-cross-tenant-{suffix}",
                new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc));
            payment.Confirm(new DateTime(2026, 8, 1, 9, 2, 0, DateTimeKind.Utc));
            participation.ApplyConfirmedJoiningPayment(payment);
            participation.ApproveByAdministrator(
                1,
                new DateTime(2026, 8, 1, 9, 3, 0, DateTimeKind.Utc));

            context.MemberPayments.Add(payment);
            context.EntryParticipations.Add(participation);
            await context.SaveChangesAsync();
            return participation.Id;
        }

        private static void CompleteInMemoryNetworkToLevelThree(
            EntryParticipation root,
            List<EntryParticipation> network,
            string suffix)
        {
            var structuralTerms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                version: $"entry-level-three-{suffix}",
                effectiveFrom: new DateTime(
                    2026,
                    7,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                joiningPaymentAmount: 1200m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
            var currentLevel = network
                .Where(participation =>
                    participation.RecruiterCustomerId == root.CustomerId)
                .OrderBy(participation => participation.ActivatedAt)
                .ThenBy(participation => participation.Id)
                .Take(EntryNetworkQualificationEvaluator.BranchSize)
                .ToList();
            currentLevel.Count.ShouldBe(
                EntryNetworkQualificationEvaluator.BranchSize);
            var nextCustomerId = 100000;

            for (var depth = 2;
                 depth <= EntryNetworkQualificationEvaluator.MaximumLevel;
                 depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 0;
                         index < EntryNetworkQualificationEvaluator.BranchSize;
                         index++)
                    {
                        var startedAt = new DateTime(
                            2026,
                            8,
                            1,
                            9,
                            depth,
                            0,
                            DateTimeKind.Utc);
                        var recruit = EntryParticipation.StartUnderRecruiter(
                            1,
                            nextCustomerId,
                            recruiter,
                            structuralTerms,
                            startedAt);
                        var payment = MemberPayment.CreatePending(
                            1,
                            nextCustomerId,
                            MemberPaymentPurpose.AQGreenJoining,
                            1200m,
                            "Test",
                            $"commission-level-three-{suffix}-{nextCustomerId}",
                            startedAt.AddSeconds(1));
                        payment.Confirm(startedAt.AddSeconds(2));
                        recruit.ApplyConfirmedJoiningPayment(payment);
                        recruit.ApproveByAdministrator(
                            1,
                            startedAt.AddSeconds(3));
                        network.Add(recruit);
                        nextLevel.Add(recruit);
                        nextCustomerId++;
                    }
                }

                currentLevel = nextLevel;
            }
        }

        private async Task<Guid> CalculateAndPersistFirstRunAsync(Guid participationId)
        {
            await using var context = CreateDbContext();
            var participation = await context.EntryParticipations.SingleAsync(p => p.Id == participationId);
            var termsVersion = EntryCommissionTermsVersion.Create(
                $"test-entry-commission-2026-08",
                new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                150m,
                250m,
                1250m);
            context.EntryCommissionTermsVersions.Add(termsVersion);

            var areaRecord = AreaActivationStateRecord.Record(
                Guid.NewGuid(),
                1,
                true,
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                null,
                "PostgreSQL weekly commission test baseline",
                AreaActivationStateRecordKind.ObservedBaseline);
            context.AreaActivationStateRecords.Add(areaRecord);
            await context.SaveChangesAsync();

            var terms = termsVersion.ToTerms();
            var periodStart = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = new DateTime(2026, 8, 13, 23, 59, 59, 999, DateTimeKind.Utc);
            var period = EntryCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                terms);
            context.EntryCommissionPeriods.Add(period);

            var network = await context.EntryParticipations
                .Include(p => p.RecruiterCorrections)
                .Where(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active)
                .ToListAsync();
            var obligations = await context.EntryMonthlyObligations
                .Where(o => network.Select(n => n.Id).Contains(o.EntryParticipationId))
                .ToListAsync();

            var calculator = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator());
            var commission = calculator.Calculate(
                participation,
                period,
                terms,
                network,
                obligations);

            context.EntryWeeklyCommissions.Add(commission);
            await context.SaveChangesAsync();
            return period.Id;
        }

        private async Task CalculateAndPersistDuplicateRunAsync(DateTime periodStart, DateTime periodEnd)
        {
            await using var context = CreateDbContext();
            var participation = await context.EntryParticipations
                .FirstAsync(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active);
            var termsVersion = await context.EntryCommissionTermsVersions.SingleAsync();
            var terms = termsVersion.ToTerms();

            var duplicatePeriod = EntryCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                terms);
            context.EntryCommissionPeriods.Add(duplicatePeriod);

            var network = await context.EntryParticipations
                .Include(p => p.RecruiterCorrections)
                .Where(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active)
                .ToListAsync();
            var obligations = await context.EntryMonthlyObligations
                .Where(o => network.Select(n => n.Id).Contains(o.EntryParticipationId))
                .ToListAsync();

            var calculator = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator());
            var commission = calculator.Calculate(
                participation,
                duplicatePeriod,
                terms,
                network,
                obligations);

            context.EntryWeeklyCommissions.Add(commission);
            await context.SaveChangesAsync();
        }

        private void TraceLine(string message)
        {
            Console.WriteLine($"[WeeklyCommissionPostgreSqlTest] {message}");
        }
    }
}
