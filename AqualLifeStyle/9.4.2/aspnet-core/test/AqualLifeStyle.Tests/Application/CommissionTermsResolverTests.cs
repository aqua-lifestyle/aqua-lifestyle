using System;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class CommissionTermsResolverTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime FirstFridayBoundary =
            new(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc);

        private static ClosedCommissionWeek CycleStartingAt(DateTime boundaryUtc)
        {
            return new ClosedCommissionWeek(
                boundaryUtc,
                boundaryUtc.AddDays(7).AddTicks(-1),
                LatestClosedCommissionWeekResolver.CommissionTimeZoneId);
        }

        private async Task<EntryCommissionTermsVersion> AddEntryVersionAsync(
            string version,
            DateTime boundaryUtc,
            decimal levelOne = 150m,
            decimal levelTwo = 250m,
            decimal levelThree = 1250m)
        {
            var persisted = EntryCommissionTermsVersion.Create(
                version,
                boundaryUtc,
                levelOne,
                levelTwo,
                levelThree);
            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(persisted);
                await context.SaveChangesAsync();
            });
            return persisted;
        }

        private async Task<OnyxCommissionTermsVersion> AddOnyxVersionAsync(
            string version,
            DateTime boundaryUtc,
            decimal levelOne = 50m,
            decimal levelTwo = 20m,
            decimal levelThree = 12.62m,
            decimal levelFour = 5m,
            decimal levelFive = 4m)
        {
            var persisted = OnyxCommissionTermsVersion.Create(
                version,
                boundaryUtc,
                levelOne,
                levelTwo,
                levelThree,
                levelFour,
                levelFive);
            await UsingDbContextAsync(null, async context =>
            {
                context.OnyxCommissionTermsVersions.Add(persisted);
                await context.SaveChangesAsync();
            });
            return persisted;
        }

        private async Task<EntryCommissionTerms> ResolveEntryTermsAsync(
            ClosedCommissionWeek cycle)
        {
            using (var unitOfWork = Resolve<IUnitOfWorkManager>().Begin())
            {
                var resolver = Resolve<ICommissionTermsResolver>();
                var terms = await resolver.ResolveEntryTermsAsync(cycle);
                await unitOfWork.CompleteAsync();
                return terms;
            }
        }

        private async Task<OnyxCommissionTerms> ResolveOnyxTermsAsync(
            ClosedCommissionWeek cycle)
        {
            using (var unitOfWork = Resolve<IUnitOfWorkManager>().Begin())
            {
                var resolver = Resolve<ICommissionTermsResolver>();
                var terms = await resolver.ResolveOnyxTermsAsync(cycle);
                await unitOfWork.CompleteAsync();
                return terms;
            }
        }

        [Fact]
        public async Task CycleBeforeV2Boundary_ResolvesV1()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary, 150m);
            await AddEntryVersionAsync("entry-v2", FirstFridayBoundary.AddDays(7), 160m);
            var v1Cycle = CycleStartingAt(FirstFridayBoundary);

            var terms = await ResolveEntryTermsAsync(v1Cycle);

            terms.Version.ShouldBe("entry-v1");
            terms.GetComponentAmount(1).ShouldBe(150m);
        }

        [Fact]
        public async Task CycleOpeningExactlyAtV2Boundary_ResolvesV2()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary, 150m);
            await AddEntryVersionAsync("entry-v2", FirstFridayBoundary.AddDays(7), 160m);
            var v2Cycle = CycleStartingAt(FirstFridayBoundary.AddDays(7));

            var terms = await ResolveEntryTermsAsync(v2Cycle);

            terms.Version.ShouldBe("entry-v2");
            terms.GetComponentAmount(1).ShouldBe(160m);
        }

        [Fact]
        public async Task CycleAfterV2Boundary_ResolvesV2()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary, 150m);
            await AddEntryVersionAsync("entry-v2", FirstFridayBoundary.AddDays(7), 160m);
            var laterCycle = CycleStartingAt(FirstFridayBoundary.AddDays(14));

            var terms = await ResolveEntryTermsAsync(laterCycle);

            terms.Version.ShouldBe("entry-v2");
            terms.GetComponentAmount(1).ShouldBe(160m);
        }

        [Fact]
        public async Task FutureVersionCannotRewriteAnEarlierClosedCycle()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary, 150m);
            var historicalCycle = CycleStartingAt(FirstFridayBoundary.AddDays(7));

            var before = await ResolveEntryTermsAsync(historicalCycle);
            before.Version.ShouldBe("entry-v1");
            before.GetComponentAmount(1).ShouldBe(150m);

            await AddEntryVersionAsync("entry-v2", FirstFridayBoundary.AddDays(14), 160m);

            var after = await ResolveEntryTermsAsync(historicalCycle);
            after.Version.ShouldBe("entry-v1");
            after.GetComponentAmount(1).ShouldBe(150m);
            after.EffectiveFrom.ShouldBe(before.EffectiveFrom);
        }

        [Fact]
        public async Task NextFridayVersionCannotRewriteThePrecedingCycle()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary, 150m);
            await AddEntryVersionAsync(
                "entry-v2",
                FirstFridayBoundary.AddDays(7),
                160m);

            var terms = await ResolveEntryTermsAsync(
                CycleStartingAt(FirstFridayBoundary));

            terms.Version.ShouldBe("entry-v1");
            terms.GetComponentAmount(1).ShouldBe(150m);
        }

        [Fact]
        public async Task MissingTermsVersion_FailsClosedWithoutCurrentTermsFallback()
        {
            var cycle = CycleStartingAt(FirstFridayBoundary);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await ResolveEntryTermsAsync(cycle));

            exception.Message.ShouldContain("refuses to use current terms");
        }

        [Fact]
        public async Task MissingOnyxTermsVersion_FailsClosedWithoutCurrentTermsFallback()
        {
            var cycle = CycleStartingAt(FirstFridayBoundary);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await ResolveOnyxTermsAsync(cycle));

            exception.Message.ShouldContain("refuses to use current terms");
        }

        [Fact]
        public async Task OverlappingVersionsAtTheSameBoundary_AreRejected()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary);
            var duplicate = EntryCommissionTermsVersion.Create(
                "entry-v1-duplicate",
                FirstFridayBoundary,
                150m,
                250m,
                1250m);

            await Should.ThrowAsync<DbUpdateException>(async () =>
                await UsingDbContextAsync(null, async context =>
                {
                    context.EntryCommissionTermsVersions.Add(duplicate);
                    await context.SaveChangesAsync();
                }));
        }

        [Fact]
        public async Task ProgrammeVersions_AreIsolatedFromEachOther()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary);
            await AddOnyxVersionAsync("onyx-v1", FirstFridayBoundary);
            var cycle = CycleStartingAt(FirstFridayBoundary);

            var entryTerms = await ResolveEntryTermsAsync(cycle);
            var onyxTerms = await ResolveOnyxTermsAsync(cycle);

            entryTerms.Version.ShouldBe("entry-v1");
            onyxTerms.Version.ShouldBe("onyx-v1");
        }

        [Fact]
        public async Task EntryVersion_CannotResolveForOnyxAndViceVersa()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary);
            var cycle = CycleStartingAt(FirstFridayBoundary);

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await ResolveOnyxTermsAsync(cycle));

            await AddOnyxVersionAsync("onyx-v1", FirstFridayBoundary);
            await UsingDbContextAsync(null, async context =>
            {
                var entryVersions = await context.EntryCommissionTermsVersions.CountAsync();
                entryVersions.ShouldBe(1);
            });

            var entryTerms = await ResolveEntryTermsAsync(cycle);
            entryTerms.Version.ShouldBe("entry-v1");
        }

        [Fact]
        public async Task OnyxRates_ResolveFromThePersistedVersionOnly()
        {
            await AddOnyxVersionAsync(
                "onyx-v1",
                FirstFridayBoundary,
                50m,
                20m,
                12.62m,
                5m,
                4m);

            var terms = await ResolveOnyxTermsAsync(
                CycleStartingAt(FirstFridayBoundary));

            terms.GetPerPersonRate(OnyxNetworkLevel.Level1).ShouldBe(50m);
            terms.GetPerPersonRate(OnyxNetworkLevel.Level3).ShouldBe(12.62m);
            terms.GetPerPersonRate(OnyxNetworkLevel.Level5).ShouldBe(4m);
            terms.Currency.ShouldBe("ZAR");
        }

        [Fact]
        public async Task CalculationNeverTouchesTheCurrentProvider()
        {
            await AddEntryVersionAsync("entry-v1", FirstFridayBoundary, 150m);
            var cycle = CycleStartingAt(FirstFridayBoundary);

            var terms = await ResolveEntryTermsAsync(cycle);

            var currentProvider = Resolve<ICurrentCommissionTermsProvider>();
            currentProvider.GetEntryTerms().Version.ShouldNotBe(terms.Version);
            currentProvider.GetEntryTerms().EffectiveFrom.ShouldNotBe(
                terms.EffectiveFrom);
        }
    }
}
