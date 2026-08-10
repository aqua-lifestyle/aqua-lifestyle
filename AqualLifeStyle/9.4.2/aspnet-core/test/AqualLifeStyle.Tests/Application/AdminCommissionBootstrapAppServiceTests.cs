using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Configuration.Startup;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    /// <summary>
    /// Focused regression coverage for the financial bootstrap that creates the
    /// initial immutable Entry and Onyx commission terms. The bootstrap writes
    /// financially authoritative data, so idempotency, fail-closed conflicts,
    /// staging atomicity and host-only access are all exercised against the
    /// persistence layer rather than mocks.
    /// </summary>
    public class AdminCommissionBootstrapAppServiceTests : AqualLifeStyleTestBase
    {
        private const string ExpectedEntryVersion =
            AdminCommissionBootstrapAppService.InitialEntryTermsVersion;
        private const string ExpectedOnyxVersion =
            AdminCommissionBootstrapAppService.InitialOnyxTermsVersion;

        private const decimal EntryLevelOne = 150m;
        private const decimal EntryLevelTwo = 250m;
        private const decimal EntryLevelThree = 1250m;
        private const decimal OnyxLevelOne = 50m;
        private const decimal OnyxLevelTwo = 20m;
        private const decimal OnyxLevelThree = 12.62m;
        private const decimal OnyxLevelFour = 5m;
        private const decimal OnyxLevelFive = 4m;

        private static readonly DateTime FirstAutomatedCycleBoundaryUtc =
            new(2026, 8, 13, 22, 0, 0, DateTimeKind.Utc);

        private readonly IAdminCommissionBootstrapAppService _service;

        public AdminCommissionBootstrapAppServiceTests()
        {
            LoginAsHostAdmin();
            _service = Resolve<IAdminCommissionBootstrapAppService>();
        }

        [Fact]
        public async Task InitialBootstrap_InsertsExactlyOneEntryAndOneOnyxVersion_WithExpectedFinancialFacts()
        {
            var result = await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = false });

            result.DryRun.ShouldBeFalse();
            result.AnyConflict.ShouldBeFalse();
            result.Rows.Count.ShouldBe(2);
            result.Rows[0].Programme.ShouldBe("Entry");
            result.Rows[0].Version.ShouldBe(ExpectedEntryVersion);
            result.Rows[0].EffectiveAtUtc.ShouldBe(FirstAutomatedCycleBoundaryUtc);
            result.Rows[0].Status.ShouldBe(
                CommissionTermsBootstrapRowStatus.Inserted);
            result.Rows[1].Programme.ShouldBe("Onyx");
            result.Rows[1].Version.ShouldBe(ExpectedOnyxVersion);
            result.Rows[1].EffectiveAtUtc.ShouldBe(FirstAutomatedCycleBoundaryUtc);
            result.Rows[1].Status.ShouldBe(
                CommissionTermsBootstrapRowStatus.Inserted);

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(1);

            UsingDbContext(null, context =>
            {
                var entry = context.EntryCommissionTermsVersions.Single();
                entry.Version.ShouldBe(ExpectedEntryVersion);
                entry.EffectiveAt.ShouldBe(FirstAutomatedCycleBoundaryUtc);
                entry.LevelOneComponentAmount.ShouldBe(EntryLevelOne);
                entry.LevelTwoComponentAmount.ShouldBe(EntryLevelTwo);
                entry.LevelThreeComponentAmount.ShouldBe(EntryLevelThree);
                entry.Currency.ShouldBe("ZAR");

                var onyx = context.OnyxCommissionTermsVersions.Single();
                onyx.Version.ShouldBe(ExpectedOnyxVersion);
                onyx.EffectiveAt.ShouldBe(FirstAutomatedCycleBoundaryUtc);
                onyx.LevelOnePerPersonRate.ShouldBe(OnyxLevelOne);
                onyx.LevelTwoPerPersonRate.ShouldBe(OnyxLevelTwo);
                onyx.LevelThreePerPersonRate.ShouldBe(OnyxLevelThree);
                onyx.LevelFourPerPersonRate.ShouldBe(OnyxLevelFour);
                onyx.LevelFivePerPersonRate.ShouldBe(OnyxLevelFive);
                onyx.Currency.ShouldBe("ZAR");
            });
        }

        [Fact]
        public async Task ExactRerun_ReportsAlreadyPresent_NoNewRows_NoMutation()
        {
            var first = await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = false });

            var (entryId, onyxId) = UsingDbContext(null, context =>
                (context.EntryCommissionTermsVersions.Single().Id,
                 context.OnyxCommissionTermsVersions.Single().Id));

            var second = await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = false });

            second.AnyConflict.ShouldBeFalse();
            second.Rows.Count.ShouldBe(2);
            second.Rows.ShouldAllBe(row =>
                row.Status == CommissionTermsBootstrapRowStatus.AlreadyPresent);

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(1);

            UsingDbContext(null, context =>
            {
                var entry = context.EntryCommissionTermsVersions.Single();
                entry.Id.ShouldBe(entryId);
                entry.Version.ShouldBe(first.Rows[0].Version);
                entry.LevelOneComponentAmount.ShouldBe(EntryLevelOne);
                entry.LevelTwoComponentAmount.ShouldBe(EntryLevelTwo);
                entry.LevelThreeComponentAmount.ShouldBe(EntryLevelThree);
                entry.Currency.ShouldBe("ZAR");

                var onyx = context.OnyxCommissionTermsVersions.Single();
                onyx.Id.ShouldBe(onyxId);
                onyx.Version.ShouldBe(first.Rows[1].Version);
                onyx.LevelOnePerPersonRate.ShouldBe(OnyxLevelOne);
                onyx.LevelFivePerPersonRate.ShouldBe(OnyxLevelFive);
                onyx.Currency.ShouldBe("ZAR");
            });
        }

        [Fact]
        public async Task SameVersionDifferentRates_FailsClosed_WithoutPartialInserts()
        {
            SeedEntryVersion(ExpectedEntryVersion, levelOne: 200m);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = false }));

            exception.Message.ShouldContain("different rates");

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(0);
        }

        [Fact]
        public async Task SameEffectiveAtDifferentVersion_FailsClosed()
        {
            SeedEntryVersion("entry-other-version-bootstrap-v1");

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = false }));

            exception.Message.ShouldContain(
                "occupies the authorised effective boundary");

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(0);
        }

        [Fact]
        public async Task EntryConflict_PreventsPartialOnyxInsert()
        {
            SeedEntryVersion(ExpectedEntryVersion, levelTwo: 999m);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = false }));

            exception.Message.ShouldContain("different rates");

            TermsCounts().Onyx.ShouldBe(0);
        }

        [Fact]
        public async Task OnyxConflict_PreventsPartialEntryInsert_AndExercisesRestoredOnyxStaging()
        {
            SeedOnyxVersion(ExpectedOnyxVersion, levelFive: 9m);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = false }));

            exception.Message.ShouldContain("Onyx");
            exception.Message.ShouldContain("different rates");

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(0);
            onyxCount.ShouldBe(1);
        }

        [Fact]
        public async Task DryRun_WhenRowsAbsent_ReportsWouldInsert_AndLeavesDatabaseUnchanged()
        {
            var result = await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = true });

            result.DryRun.ShouldBeTrue();
            result.AnyConflict.ShouldBeFalse();
            result.Rows.Count.ShouldBe(2);
            result.Rows.ShouldAllBe(row =>
                row.Status == CommissionTermsBootstrapRowStatus.WouldInsert);

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(0);
            onyxCount.ShouldBe(0);
        }

        [Fact]
        public async Task DryRun_WhenRowsAlreadyPresent_ReportsAlreadyPresent_AndLeavesDatabaseUnchanged()
        {
            SeedEntryVersion(ExpectedEntryVersion);
            SeedOnyxVersion(ExpectedOnyxVersion);

            var result = await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = true });

            result.AnyConflict.ShouldBeFalse();
            result.Rows.Count.ShouldBe(2);
            result.Rows.ShouldAllBe(row =>
                row.Status == CommissionTermsBootstrapRowStatus.AlreadyPresent);

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(1);

            UsingDbContext(null, context =>
            {
                var entry = context.EntryCommissionTermsVersions.Single();
                entry.LevelOneComponentAmount.ShouldBe(EntryLevelOne);
                var onyx = context.OnyxCommissionTermsVersions.Single();
                onyx.LevelThreePerPersonRate.ShouldBe(OnyxLevelThree);
            });
        }

        [Fact]
        public async Task DryRun_Conflict_FailsWithSameSemanticsAsExecution_AndLeavesDatabaseUnchanged()
        {
            SeedEntryVersion(ExpectedEntryVersion, levelOne: 300m);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = true }));

            exception.Message.ShouldContain("different rates");

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(0);
        }

        [Fact]
        public async Task CanonicalBoundaryValidation_AcceptsFridayMidnightJohannesburg_RejectsNonCanonical()
        {
            CommissionCycleBoundary
                .IsCanonicalCycleBoundary(FirstAutomatedCycleBoundaryUtc)
                .ShouldBeTrue();
            CommissionCycleBoundary
                .IsCanonicalCycleBoundary(
                    new DateTime(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc))
                .ShouldBeTrue();

            CommissionCycleBoundary
                .IsCanonicalCycleBoundary(
                    new DateTime(2026, 8, 14, 22, 0, 0, DateTimeKind.Utc))
                .ShouldBeFalse();
            CommissionCycleBoundary
                .IsCanonicalCycleBoundary(
                    new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc))
                .ShouldBeFalse();
            CommissionCycleBoundary
                .IsCanonicalCycleBoundary(
                    new DateTime(2026, 8, 12, 22, 0, 0, DateTimeKind.Utc))
                .ShouldBeFalse();
            CommissionCycleBoundary
                .IsCanonicalCycleBoundary(default)
                .ShouldBeFalse();

            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "noncanonical-boundary",
                    new DateTime(2026, 8, 14, 22, 0, 0, DateTimeKind.Utc),
                    EntryLevelOne,
                    EntryLevelTwo,
                    EntryLevelThree));
        }

        [Fact]
        public async Task ProgrammeSeparation_ExactOnyxRow_AllowsEntryOnlyInsert_WithoutTouchingOnyx()
        {
            SeedOnyxVersion(ExpectedOnyxVersion);

            var result = await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = false });

            result.AnyConflict.ShouldBeFalse();
            result.Rows.Count.ShouldBe(2);
            result.Rows[0].Programme.ShouldBe("Entry");
            result.Rows[0].Status.ShouldBe(
                CommissionTermsBootstrapRowStatus.Inserted);
            result.Rows[1].Programme.ShouldBe("Onyx");
            result.Rows[1].Status.ShouldBe(
                CommissionTermsBootstrapRowStatus.AlreadyPresent);

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(1);

            UsingDbContext(null, context =>
            {
                var entry = context.EntryCommissionTermsVersions.Single();
                entry.LevelOneComponentAmount.ShouldBe(EntryLevelOne);
                entry.LevelTwoComponentAmount.ShouldBe(EntryLevelTwo);
                entry.LevelThreeComponentAmount.ShouldBe(EntryLevelThree);

                var onyx = context.OnyxCommissionTermsVersions.Single();
                onyx.LevelOnePerPersonRate.ShouldBe(OnyxLevelOne);
                onyx.LevelTwoPerPersonRate.ShouldBe(OnyxLevelTwo);
                onyx.LevelThreePerPersonRate.ShouldBe(OnyxLevelThree);
                onyx.LevelFourPerPersonRate.ShouldBe(OnyxLevelFour);
                onyx.LevelFivePerPersonRate.ShouldBe(OnyxLevelFive);
            });
        }

        [Fact]
        public async Task ProgrammeSeparation_OnyxRateConflict_FailsClosed_UsingOnyxPerPersonRatesOnly()
        {
            SeedEntryVersion(ExpectedEntryVersion);
            SeedOnyxVersion(ExpectedOnyxVersion, levelThree: 30m);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = false }));

            exception.Message.ShouldContain("Onyx");
            exception.Message.ShouldContain("different rates");

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(1);
            onyxCount.ShouldBe(1);

            UsingDbContext(null, context =>
            {
                var entry = context.EntryCommissionTermsVersions.Single();
                entry.LevelOneComponentAmount.ShouldBe(EntryLevelOne);
                var onyx = context.OnyxCommissionTermsVersions.Single();
                onyx.LevelThreePerPersonRate.ShouldBe(30m);
            });
        }

        [Fact]
        public async Task Bootstrap_LeavesBothWorkersDisabled_AndTriggersNoCalculation()
        {
            var configuration = Resolve<IConfiguration>();
            var startupConfiguration =
                Resolve<IAbpStartupConfiguration>();

            configuration.GetValue<bool>("App:WeeklyCommissions:Enabled")
                .ShouldBeFalse();
            configuration.GetValue<bool>("App:EntryMonthlyObligations:Enabled")
                .ShouldBeFalse();
            startupConfiguration.BackgroundJobs.IsJobExecutionEnabled
                .ShouldBeFalse();

            await _service.BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = false });

            configuration.GetValue<bool>("App:WeeklyCommissions:Enabled")
                .ShouldBeFalse();
            configuration.GetValue<bool>("App:EntryMonthlyObligations:Enabled")
                .ShouldBeFalse();
            startupConfiguration.BackgroundJobs.IsJobExecutionEnabled
                .ShouldBeFalse();

            UsingDbContext(null, context =>
            {
                context.EntryCommissionPeriods.Count().ShouldBe(0);
                context.OnyxCommissionPeriods.Count().ShouldBe(0);
            });
        }

        [Fact]
        public async Task TenantSession_IsRejected()
        {
            LoginAsDefaultTenantAdmin();

            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
                await _service.BootstrapInitialCommissionTermsAsync(
                    new BootstrapInitialCommissionTermsInput { DryRun = true }));

            var (entryCount, onyxCount) = TermsCounts();
            entryCount.ShouldBe(0);
            onyxCount.ShouldBe(0);
        }

        private void SeedEntryVersion(
            string version,
            decimal levelOne = EntryLevelOne,
            decimal levelTwo = EntryLevelTwo,
            decimal levelThree = EntryLevelThree)
        {
            UsingDbContext(null, context =>
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        version,
                        FirstAutomatedCycleBoundaryUtc,
                        levelOne,
                        levelTwo,
                        levelThree));
            });
        }

        private void SeedOnyxVersion(
            string version,
            decimal levelOne = OnyxLevelOne,
            decimal levelTwo = OnyxLevelTwo,
            decimal levelThree = OnyxLevelThree,
            decimal levelFour = OnyxLevelFour,
            decimal levelFive = OnyxLevelFive)
        {
            UsingDbContext(null, context =>
            {
                context.OnyxCommissionTermsVersions.Add(
                    OnyxCommissionTermsVersion.Create(
                        version,
                        FirstAutomatedCycleBoundaryUtc,
                        levelOne,
                        levelTwo,
                        levelThree,
                        levelFour,
                        levelFive));
            });
        }

        private (int Entry, int Onyx) TermsCounts()
        {
            return UsingDbContext(null, context =>
                (context.EntryCommissionTermsVersions.Count(),
                 context.OnyxCommissionTermsVersions.Count()));
        }
    }
}
