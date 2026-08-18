using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp;
using Abp.Domain.Repositories;
using Abp.Threading.Timers;
using Abp.Domain.Uow;
using Abp.Threading;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Web.Host.Commissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    /// <summary>
    /// Production-like Weekly Commission E2E against real PostgreSQL: a
    /// qualified AQGreen Level 1 network (root + five direct recruits, each
    /// with confirmed joining payment and Area Administrator approval) is
    /// calculated by the REAL worker execution path (tenant enumeration,
    /// activation-state resolution, advisory lock, calculator, per-programme
    /// transactions) for the latest closed Friday-to-Thursday cycle. It proves
    /// qualification, eligibility prerequisites, the Tenant + Programme
    /// boundary, cycle selection, terms selection, the exact R150 Level 1
    /// amount, persistence, and idempotent replay without duplicates.
    ///
    /// Guarded by REPRO_PG=true (real PostgreSQL via REPRO_PG_CONNECTION) with
    /// a provenance marker, so a silent short-circuit to the default SQLite
    /// harness fails the CI verification step.
    ///
    /// Self-cleaning for ledger and participation data (hard-deleted before and
    /// after the run) so a fresh CI database serves both tests in any order.
    /// Terms and area-activation records are append-only (database triggers);
    /// terms therefore require a fresh database per run, like the pinned
    /// application-path regression, while per-run-unique activation baselines
    /// accumulate harmlessly. Both classes share a sequential collection
    /// because they seed the same immutable terms version slot on one CI
    /// database.
    /// </summary>
    [Collection("WeeklyCommissionPostgreSqlRegression")]
    public class WeeklyCommissionProductionLikeE2ETests
        : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task QualifiedLevelOneNetwork_WorkerCalculatesLatestClosedWeek_ExactlyOnce()
        {
            if (!IsPostgreSqlRegressionMode())
            {
                return;
            }

            WriteProvenanceMarker();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var closedWeek = IocManager
                .Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var periodStart = closedWeek.PeriodStartUtc;
            var periodEnd = closedWeek.PeriodEndUtc;
            var missedPeriodStart = periodStart.AddDays(-7);
            var missedPeriodEnd = missedPeriodStart.AddDays(7).AddTicks(-1);

            await CleanupTargetPeriodAsync(periodStart, periodEnd, missedPeriodStart);
            await SeedLevelOneNetworkAsync(
                suffix,
                periodStart,
                periodEnd,
                missedPeriodStart);
            var worker = CreateRealWorker();

            await worker.RunOnceAsync();
            await AssertLedgerAsync(
                periodStart,
                periodEnd,
                missedPeriodStart,
                missedPeriodEnd,
                expectedPeriods: 2,
                expectedEntryCommissions: 6,
                expectedOnyxCommissions: 0);

            await worker.RunOnceAsync();
            await AssertLedgerAsync(
                periodStart,
                periodEnd,
                missedPeriodStart,
                missedPeriodEnd,
                expectedPeriods: 2,
                expectedEntryCommissions: 6,
                expectedOnyxCommissions: 0);

            await CleanupTargetPeriodAsync(periodStart, periodEnd, missedPeriodStart);
        }

        private TestableWeeklyCommissionCalculationWorker CreateRealWorker()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>(
                        "App:WeeklyCommissions:Enabled",
                        "true"),
                    new KeyValuePair<string, string>(
                        "App:WeeklyCommissions:IntervalMinutes",
                        "1440")
                })
                .Build();
            return new TestableWeeklyCommissionCalculationWorker(
                new AbpAsyncTimer(),
                configuration,
                IocManager.Resolve<IUnitOfWorkManager>(),
                IocManager.Resolve<IRepository<Tenant, int>>(),
                IocManager.Resolve<LatestClosedCommissionWeekResolver>(),
                IocManager.Resolve<IWeeklyCommissionCalculator>(),
                IocManager.Resolve<IOnyxTravelBenefitSynchronizer>(),
                IocManager.Resolve<IAreaActivationStateResolver>(),
                IocManager.Resolve<IWeeklyCommissionCalculationLock>(),
                IocManager.Resolve<ILogger<WeeklyCommissionCalculationWorker>>());
        }

        private async Task SeedLevelOneNetworkAsync(
            string suffix,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime missedPeriodStart)
        {
            await WithTenantFiltersDisabledAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue(
                    "the Weekly Commission E2E must run against real PostgreSQL");
                var activationEffectiveAt = periodStart
                    .AddDays(-2)
                    .AddMinutes(-(Convert.ToInt32(
                        suffix.Substring(0, 4),
                        16) % 1200));
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        1,
                        true,
                        activationEffectiveAt,
                        activationEffectiveAt,
                        null,
                        "Weekly Commission E2E baseline",
                        AreaActivationStateRecordKind.ObservedBaseline));

                var termsResidue = await context.EntryCommissionTermsVersions
                    .Where(version => version.EffectiveAt == periodStart)
                    .ToListAsync();
                termsResidue.ShouldBeEmpty(
                    "a fresh PostgreSQL database is required: an immutable terms version (append-only trigger) already governs the resolved cycle boundary, so this test cannot deterministically seed a baseline on a reused database");
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        $"e2e-entry-terms-{suffix}",
                        periodStart,
                        150m,
                        250m,
                        1250m));
                context.OnyxCommissionTermsVersions.Add(
                    OnyxCommissionTermsVersion.Create(
                        $"e2e-onyx-terms-{suffix}",
                        periodStart,
                        50m,
                        20m,
                        12.62m,
                        5m,
                        4m));
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        $"e2e-entry-terms-missed-{suffix}",
                        missedPeriodStart,
                        150m,
                        250m,
                        1250m));
                context.OnyxCommissionTermsVersions.Add(
                    OnyxCommissionTermsVersion.Create(
                        $"e2e-onyx-terms-missed-{suffix}",
                        missedPeriodStart,
                        50m,
                        20m,
                        12.62m,
                        5m,
                        4m));

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    $"e2e-programme-{suffix}",
                    periodStart.AddMonths(-1),
                    1200m,
                    600m,
                    7);
                var root = await CreateActiveParticipationAsync(
                    context,
                    terms,
                    null,
                    $"root-{suffix}",
                    0,
                    periodEnd);

                for (var index = 1; index <= 5; index++)
                {
                    await CreateActiveParticipationAsync(
                        context,
                        terms,
                        root,
                        $"recruit-{index}-{suffix}",
                        index,
                        periodEnd);
                }

                await context.SaveChangesAsync();
            });
        }

        private static async Task<EntryParticipation> CreateActiveParticipationAsync(
            AqualLifeStyleDbContext context,
            EntryProgrammeTerms terms,
            EntryParticipation recruiter,
            string suffix,
            int minuteOffset,
            DateTime periodEnd)
        {
            var userName = $"e2e-wc-{suffix}";
            var user = new User
            {
                TenantId = 1,
                UserName = userName,
                EmailAddress = $"{userName}@example.test",
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
                $"Weekly Commission {suffix}",
                new EmailAddress(user.EmailAddress));
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var startedAt = periodEnd.AddDays(-1).AddMinutes(minuteOffset);
            var participation = recruiter == null
                ? EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    startedAt)
                : EntryParticipation.StartUnderRecruiter(
                    1,
                    customer.Id,
                    recruiter,
                    terms,
                    startedAt);
            var payment = MemberPayment.CreatePending(
                1,
                customer.Id,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "Test",
                $"e2e-wc-payment-{suffix}",
                startedAt.AddMinutes(1));
            payment.Confirm(startedAt.AddMinutes(2));
            participation.ApplyConfirmedJoiningPayment(payment);
            participation.ApproveByAdministrator(
                1,
                startedAt.AddMinutes(3));
            context.MemberPayments.Add(payment);
            context.EntryParticipations.Add(participation);
            return participation;
        }

        private async Task AssertLedgerAsync(
            DateTime periodStart,
            DateTime periodEnd,
            DateTime missedPeriodStart,
            DateTime missedPeriodEnd,
            int expectedPeriods,
            int expectedEntryCommissions,
            int expectedOnyxCommissions)
        {
            await WithTenantFiltersDisabledAsync(async context =>
            {
                var entryPeriods = await context.EntryCommissionPeriods
                    .Where(period =>
                        period.TenantId == 1 &&
                        period.PeriodStart == periodStart &&
                        period.PeriodEnd == periodEnd)
                    .ToListAsync();
                entryPeriods.Count.ShouldBe(
                    expectedPeriods == 0 ? 0 : 1);
                var onyxPeriods = await context.OnyxCommissionPeriods
                    .Where(period =>
                        period.TenantId == 1 &&
                        period.PeriodStart == periodStart &&
                        period.PeriodEnd == periodEnd)
                    .ToListAsync();
                onyxPeriods.Count.ShouldBe(
                    expectedPeriods == 0 ? 0 : 1);

                var missedEntryPeriods = await context.EntryCommissionPeriods
                    .Where(period =>
                        period.TenantId == 1 &&
                        period.PeriodStart == missedPeriodStart &&
                        period.PeriodEnd == missedPeriodEnd)
                    .ToListAsync();
                missedEntryPeriods.ShouldBeEmpty(
                    "a missed older closed cycle must never be backfilled by the worker");
                var missedOnyxPeriods = await context.OnyxCommissionPeriods
                    .Where(period =>
                        period.TenantId == 1 &&
                        period.PeriodStart == missedPeriodStart &&
                        period.PeriodEnd == missedPeriodEnd)
                    .ToListAsync();
                missedOnyxPeriods.ShouldBeEmpty(
                    "a missed older closed cycle must never be backfilled by the worker");

                var entryCommissions = await context.EntryWeeklyCommissions
                    .Where(commission =>
                        entryPeriods.Select(period => period.Id)
                            .Contains(commission.CommissionPeriodId))
                    .ToListAsync();
                entryCommissions.Count.ShouldBe(expectedEntryCommissions);
                entryCommissions.Count(commission =>
                    commission.TotalAmount > 0m).ShouldBe(1);
                entryCommissions.Count(commission =>
                    commission.TotalAmount == 0m).ShouldBe(5);
                entryCommissions.Count(commission =>
                    commission.HighestCompletedLevel == 1).ShouldBe(1);

                var earned = entryCommissions
                    .Single(commission => commission.TotalAmount == 150m);
                earned.Currency.ShouldBe("ZAR");
                var components = await context.EntryCommissionComponents
                    .Where(component =>
                        EF.Property<Guid>(
                            component,
                            "EntryWeeklyCommissionId") == earned.Id)
                    .ToListAsync();
                components.Count.ShouldBe(1);
                components.Single().Level.ShouldBe(1);
                components.Single().Amount.ShouldBe(150m);

                var onyxCommissions = await context.OnyxWeeklyCommissions
                    .Where(commission =>
                        onyxPeriods.Select(period => period.Id)
                            .Contains(commission.CommissionPeriodId))
                    .ToListAsync();
                onyxCommissions.Count.ShouldBe(expectedOnyxCommissions);
            });
        }

        private async Task CleanupTargetPeriodAsync(
            DateTime periodStart,
            DateTime periodEnd,
            DateTime missedPeriodStart)
        {
            await WithTenantFiltersDisabledAsync(async context =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    DELETE FROM "EntryCommissionComponents"
                    WHERE "EntryWeeklyCommissionId" IN (
                        SELECT "Id" FROM "EntryWeeklyCommissions"
                        WHERE "CommissionPeriodId" IN (
                            SELECT "Id" FROM "EntryCommissionPeriods"
                            WHERE "TenantId" = {0}
                              AND "PeriodStart" = {1}
                              AND "PeriodEnd" = {2}));
                    DELETE FROM "EntryWeeklyCommissions"
                    WHERE "CommissionPeriodId" IN (
                        SELECT "Id" FROM "EntryCommissionPeriods"
                        WHERE "TenantId" = {0}
                          AND "PeriodStart" >= {3}
                          AND "PeriodEnd" <= {2});
                    DELETE FROM "EntryCommissionPeriods"
                    WHERE "TenantId" = {0}
                      AND "PeriodStart" = {1}
                      AND "PeriodEnd" = {2};
                    DELETE FROM "OnyxCommissionComponents"
                    WHERE "OnyxWeeklyCommissionId" IN (
                        SELECT "Id" FROM "OnyxWeeklyCommissions"
                        WHERE "CommissionPeriodId" IN (
                            SELECT "Id" FROM "OnyxCommissionPeriods"
                            WHERE "TenantId" = {0}
                              AND "PeriodStart" = {1}
                              AND "PeriodEnd" = {2}));
                    DELETE FROM "OnyxWeeklyCommissions"
                    WHERE "CommissionPeriodId" IN (
                        SELECT "Id" FROM "OnyxCommissionPeriods"
                        WHERE "TenantId" = {0}
                          AND "PeriodStart" >= {3}
                          AND "PeriodEnd" <= {2});
                    DELETE FROM "OnyxCommissionPeriods"
                    WHERE "TenantId" = {0}
                      AND "PeriodStart" = {1}
                      AND "PeriodEnd" = {2};
                    DELETE FROM "EntryCommissionComponents"
                    WHERE "EntryWeeklyCommissionId" IN (
                        SELECT "Id" FROM "EntryWeeklyCommissions"
                        WHERE "EntryParticipationId" IN (
                            SELECT "Id" FROM "EntryParticipations"
                            WHERE "CustomerId" IN (
                                SELECT "Id" FROM "Customers"
                                WHERE "UserId" IN (
                                    SELECT "Id" FROM "AbpUsers"
                                    WHERE "UserName" LIKE '%-wc-%'))));
                    DELETE FROM "EntryWeeklyCommissions"
                    WHERE "EntryParticipationId" IN (
                        SELECT "Id" FROM "EntryParticipations"
                        WHERE "CustomerId" IN (
                            SELECT "Id" FROM "Customers"
                            WHERE "UserId" IN (
                                SELECT "Id" FROM "AbpUsers"
                                WHERE "UserName" LIKE '%-wc-%')));
                    DELETE FROM "OnyxCommissionComponents"
                    WHERE "OnyxWeeklyCommissionId" IN (
                        SELECT "Id" FROM "OnyxWeeklyCommissions"
                        WHERE "CustomerId" IN (
                            SELECT "Id" FROM "Customers"
                            WHERE "UserId" IN (
                                SELECT "Id" FROM "AbpUsers"
                                WHERE "UserName" LIKE '%-wc-%')));
                    DELETE FROM "OnyxWeeklyCommissions"
                    WHERE "CustomerId" IN (
                        SELECT "Id" FROM "Customers"
                        WHERE "UserId" IN (
                            SELECT "Id" FROM "AbpUsers"
                            WHERE "UserName" LIKE '%-wc-%'));
                    DELETE FROM "EntryParticipations"
                    WHERE "CustomerId" IN (
                        SELECT "Id" FROM "Customers"
                        WHERE "UserId" IN (
                            SELECT "Id" FROM "AbpUsers"
                            WHERE "UserName" LIKE '%-wc-%'));
                    DELETE FROM "MemberPayments"
                    WHERE "CustomerId" IN (
                        SELECT "Id" FROM "Customers"
                        WHERE "UserId" IN (
                            SELECT "Id" FROM "AbpUsers"
                            WHERE "UserName" LIKE '%-wc-%'));
                    DELETE FROM "Customers"
                    WHERE "UserId" IN (
                        SELECT "Id" FROM "AbpUsers"
                        WHERE "UserName" LIKE '%-wc-%');
                    DELETE FROM "AbpUsers"
                    WHERE "UserName" LIKE '%-wc-%';
                    """,
                    1,
                    periodStart,
                    periodEnd,
                    missedPeriodStart);
            });
        }

        private async Task WithTenantFiltersDisabledAsync(
            Func<AqualLifeStyleDbContext, Task> action)
        {
            var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                await UsingDbContextAsync(action);
                await unitOfWork.CompleteAsync();
            }
        }

        private static bool IsPostgreSqlRegressionMode()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("REPRO_PG"),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteProvenanceMarker()
        {
            var markerDirectory = Environment.GetEnvironmentVariable("REPRO_MARKER_DIR");
            if (string.IsNullOrWhiteSpace(markerDirectory))
            {
                return;
            }

            Directory.CreateDirectory(markerDirectory);
            File.WriteAllText(
                Path.Combine(markerDirectory, "weekly-commission-e2e-pg.ran"),
                "Weekly Commission production-like E2E body executed.");
        }

        private sealed class TestableWeeklyCommissionCalculationWorker
            : WeeklyCommissionCalculationWorker
        {
            public TestableWeeklyCommissionCalculationWorker(
                AbpAsyncTimer timer,
                IConfiguration configuration,
                IUnitOfWorkManager unitOfWorkManager,
                IRepository<Tenant, int> tenantRepository,
                LatestClosedCommissionWeekResolver closedWeekResolver,
                IWeeklyCommissionCalculator commissionCalculator,
                IOnyxTravelBenefitSynchronizer travelBenefitSynchronizer,
                IAreaActivationStateResolver areaActivationStateResolver,
                IWeeklyCommissionCalculationLock calculationLock,
                ILogger<WeeklyCommissionCalculationWorker> logger)
                : base(timer, configuration, unitOfWorkManager, tenantRepository,
                    closedWeekResolver, commissionCalculator,
                    travelBenefitSynchronizer, areaActivationStateResolver,
                    calculationLock, logger)
            {
            }

            public Task RunOnceAsync() => DoWorkAsync();
        }
    }

    /// <summary>
    /// The Weekly Commission PostgreSQL regressions share one CI database and
    /// seed the same immutable terms version slot at the resolved/pinned cycle
    /// boundary, so they must never run in parallel with each other.
    /// </summary>
    [CollectionDefinition("WeeklyCommissionPostgreSqlRegression",
        DisableParallelization = true)]
    public class WeeklyCommissionPostgreSqlRegressionCollection
    {
    }
}
