using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    /// <summary>
    /// Serialized with the production-like Weekly Commission E2E: both tests
    /// seed the same immutable terms version slot at the cycle boundary and
    /// share one CI PostgreSQL database.
    /// </summary>
    [Collection("WeeklyCommissionPostgreSqlRegression")]
    public class WeeklyCommissionPostgreSqlApplicationPathTests
        : AqualLifeStyleWebTestBase
    {
        // Keep this app-path fixture on a closed historical cycle already
        // behind the production-like E2E resolver window, avoiding shared
        // Entry-terms EffectiveAt collisions without runtime clock dependence.
        private static readonly DateTime PeriodStart =
            new(2026, 7, 23, 22, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime PeriodEnd =
            PeriodStart.AddDays(7).AddTicks(-1);

        [Fact]
        public async Task PostgreSqlApplicationPath_RollsBackThenRetriesIdempotently()
        {
            if (!IsPostgreSqlRegressionMode())
            {
                return;
            }

            WriteProvenanceMarker();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            await CleanupTargetPeriodAsync();
            await SeedLevelOneNetworkAsync(suffix);
            await InstallFailureTriggerAsync();

            try
            {
                await Should.ThrowAsync<DbUpdateException>(ExecuteCalculationAsync);
                await AssertLedgerCountsAsync(0, 0, 0);
            }
            finally
            {
                await RemoveFailureTriggerAsync();
            }

            var first = await ExecuteCalculationAsync();
            var retry = await ExecuteCalculationAsync();

            first.WasAlreadyCalculated.ShouldBeFalse();
            first.RecordsCreated.ShouldBe(6);
            first.EvaluatedCount.ShouldBe(6);
            first.EarnedCount.ShouldBe(1);
            first.NotEarnedCount.ShouldBe(5);
            first.TotalEarnedAmount.ShouldBe(150m);
            first.RulesVersion.ShouldBe($"pg-entry-terms-{suffix}");

            retry.WasAlreadyCalculated.ShouldBeTrue();
            retry.PeriodId.ShouldBe(first.PeriodId);
            retry.RecordsCreated.ShouldBe(0);
            retry.EvaluatedCount.ShouldBe(6);
            retry.TotalEarnedAmount.ShouldBe(150m);

            await AssertLedgerCountsAsync(1, 6, 1);
        }

        private async Task SeedLevelOneNetworkAsync(string suffix)
        {
            await WithTenantFiltersDisabledAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue(
                    "the application-path regression must run against real PostgreSQL");
                var activationEffectiveAt = PeriodStart
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
                        "PostgreSQL application-path test baseline",
                        AreaActivationStateRecordKind.ObservedBaseline));
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        $"pg-entry-terms-{suffix}",
                        PeriodStart,
                        150m,
                        250m,
                        1250m));

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    $"pg-entry-programme-{suffix}",
                    PeriodStart.AddMonths(-1),
                    1200m,
                    600m,
                    7);

                var residue = await context.EntryCommissionTermsVersions
                    .Where(version =>
                        version.EffectiveAt == PeriodStart)
                    .ToListAsync();
                residue.ShouldBeEmpty(
                    "a fresh PostgreSQL database is required: an immutable terms version already governs the pinned cycle boundary (unique EffectiveAt), so this test cannot deterministically seed a baseline on a reused database");
                var root = await CreateActiveParticipationAsync(
                    context,
                    terms,
                    null,
                    $"root-{suffix}",
                    0);

                for (var index = 1; index <= 5; index++)
                {
                    await CreateActiveParticipationAsync(
                        context,
                        terms,
                        root,
                        $"recruit-{index}-{suffix}",
                        index);
                }

                await context.SaveChangesAsync();
            });
        }

        private async Task CleanupTargetPeriodAsync()
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
                          AND "PeriodStart" = {1}
                          AND "PeriodEnd" = {2});
                    DELETE FROM "EntryCommissionPeriods"
                    WHERE "TenantId" = {0}
                      AND "PeriodStart" = {1}
                      AND "PeriodEnd" = {2};
                    """,
                    1,
                    PeriodStart,
                    PeriodEnd);
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

        private static async Task<EntryParticipation> CreateActiveParticipationAsync(
            AqualLifeStyleDbContext context,
            EntryProgrammeTerms terms,
            EntryParticipation recruiter,
            string suffix,
            int minuteOffset)
        {
            var userName = $"pg-wc-{suffix}";
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

            var startedAt = PeriodStart.AddDays(-2).AddMinutes(minuteOffset);
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
                $"pg-wc-payment-{suffix}",
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

        private async Task<CommissionCalculationResultDto> ExecuteCalculationAsync()
        {
            var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
            using (var unitOfWork = unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true
            }))
            using (unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                await IocManager.Resolve<IWeeklyCommissionCalculationLock>()
                    .AcquireAsync();
                var result = await IocManager.Resolve<IWeeklyCommissionCalculator>()
                    .CalculateEntryAsync(
                        1,
                        new ClosedCommissionWeek(
                            PeriodStart,
                            PeriodEnd,
                            LatestClosedCommissionWeekResolver.CommissionTimeZoneId),
                        PeriodEnd.AddMinutes(1));
                await unitOfWork.CompleteAsync();
                return result;
            }
        }

        private Task InstallFailureTriggerAsync()
        {
            return UsingDbContextAsync(context => context.Database.ExecuteSqlRawAsync(
                """
                CREATE OR REPLACE FUNCTION fail_weekly_commission_test() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'injected weekly commission persistence failure';
                END;
                $$ LANGUAGE plpgsql;
                DROP TRIGGER IF EXISTS fail_weekly_commission_test ON "EntryWeeklyCommissions";
                CREATE TRIGGER fail_weekly_commission_test
                    BEFORE INSERT ON "EntryWeeklyCommissions"
                    FOR EACH ROW EXECUTE FUNCTION fail_weekly_commission_test();
                """));
        }

        private Task RemoveFailureTriggerAsync()
        {
            return UsingDbContextAsync(context => context.Database.ExecuteSqlRawAsync(
                """
                DROP TRIGGER IF EXISTS fail_weekly_commission_test ON "EntryWeeklyCommissions";
                DROP FUNCTION IF EXISTS fail_weekly_commission_test();
                """));
        }

        private async Task AssertLedgerCountsAsync(
            int periods,
            int commissions,
            int components)
        {
            await WithTenantFiltersDisabledAsync(async context =>
            {
                var periodIds = await context.EntryCommissionPeriods
                    .Where(period =>
                    period.PeriodStart == PeriodStart &&
                    period.PeriodEnd == PeriodEnd)
                    .Select(period => period.Id)
                    .ToListAsync();
                periodIds.Count.ShouldBe(periods);

                var commissionIds = await context.EntryWeeklyCommissions
                    .Where(commission =>
                        periodIds.Contains(commission.CommissionPeriodId))
                    .Select(commission => commission.Id)
                    .ToListAsync();
                commissionIds.Count.ShouldBe(commissions);
                (await context.EntryCommissionComponents.CountAsync(component =>
                    commissionIds.Contains(EF.Property<Guid>(
                        component,
                        "EntryWeeklyCommissionId")))).ShouldBe(components);
            });
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
                Path.Combine(markerDirectory, "weekly-commission-application-path-pg.ran"),
                "PostgreSQL weekly commission application-path body executed.");
        }
    }
}
