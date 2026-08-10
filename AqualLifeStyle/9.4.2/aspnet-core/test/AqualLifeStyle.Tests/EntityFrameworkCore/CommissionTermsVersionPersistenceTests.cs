using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class CommissionTermsVersionPersistenceTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime FridayBoundary =
            new(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Versions_PersistAndReloadWithAllFinancialFacts()
        {
            var entryVersion = EntryCommissionTermsVersion.Create(
                "persisted-entry-v1",
                FridayBoundary,
                150m,
                250m,
                1250m,
                "ZAR");
            var onyxVersion = OnyxCommissionTermsVersion.Create(
                "persisted-onyx-v1",
                FridayBoundary.AddDays(7),
                50m,
                20m,
                12.62m,
                5m,
                4m,
                "ZAR");

            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(entryVersion);
                context.OnyxCommissionTermsVersions.Add(onyxVersion);
                await context.SaveChangesAsync();
            });

            await UsingDbContextAsync(null, async context =>
            {
                var reloadedEntry = await context.EntryCommissionTermsVersions
                    .SingleAsync(version => version.Id == entryVersion.Id);
                reloadedEntry.Version.ShouldBe("persisted-entry-v1");
                reloadedEntry.EffectiveAt.ShouldBe(FridayBoundary);
                reloadedEntry.LevelOneComponentAmount.ShouldBe(150m);
                reloadedEntry.LevelTwoComponentAmount.ShouldBe(250m);
                reloadedEntry.LevelThreeComponentAmount.ShouldBe(1250m);
                reloadedEntry.Currency.ShouldBe("ZAR");
                reloadedEntry.ToTerms().Version.ShouldBe("persisted-entry-v1");

                var reloadedOnyx = await context.OnyxCommissionTermsVersions
                    .SingleAsync(version => version.Id == onyxVersion.Id);
                reloadedOnyx.Version.ShouldBe("persisted-onyx-v1");
                reloadedOnyx.EffectiveAt.ShouldBe(FridayBoundary.AddDays(7));
                reloadedOnyx.LevelThreePerPersonRate.ShouldBe(12.62m);
                reloadedOnyx.LevelFivePerPersonRate.ShouldBe(4m);
                reloadedOnyx.ToTerms().GetPerPersonRate(OnyxNetworkLevel.Level1)
                    .ShouldBe(50m);
            });
        }

        [Fact]
        public async Task TwoVersions_AtTheSameBoundary_AreRejected()
        {
            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "overlap-entry-v1",
                        FridayBoundary,
                        150m,
                        250m,
                        1250m));
                await context.SaveChangesAsync();
            });

            await Should.ThrowAsync<DbUpdateException>(async () =>
                await UsingDbContextAsync(null, async context =>
                {
                    context.EntryCommissionTermsVersions.Add(
                        EntryCommissionTermsVersion.Create(
                            "overlap-entry-v2",
                            FridayBoundary,
                            160m,
                            260m,
                            1260m));
                    await context.SaveChangesAsync();
                }));
        }

        [Fact]
        public async Task TwoVersions_WithTheSameVersionIdentifier_AreRejected()
        {
            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "duplicate-version-id",
                        FridayBoundary,
                        150m,
                        250m,
                        1250m));
                await context.SaveChangesAsync();
            });

            await Should.ThrowAsync<DbUpdateException>(async () =>
                await UsingDbContextAsync(null, async context =>
                {
                    context.EntryCommissionTermsVersions.Add(
                        EntryCommissionTermsVersion.Create(
                            "duplicate-version-id",
                            FridayBoundary.AddDays(7),
                            150m,
                            250m,
                            1250m));
                    await context.SaveChangesAsync();
                }));
        }

        [Fact]
        public async Task PersistedVersions_CannotBeModified()
        {
            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "append-only-entry-v1",
                        FridayBoundary,
                        150m,
                        250m,
                        1250m));
                context.OnyxCommissionTermsVersions.Add(
                    OnyxCommissionTermsVersion.Create(
                        "append-only-onyx-v1",
                        FridayBoundary.AddDays(7),
                        50m,
                        20m,
                        12.62m,
                        5m,
                        4m));
                await context.SaveChangesAsync();
            });

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await UsingDbContextAsync(null, async context =>
                {
                    var entryVersion = await context.EntryCommissionTermsVersions
                        .SingleAsync(version =>
                            version.Version == "append-only-entry-v1");
                    context.Entry(entryVersion).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                }));

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await UsingDbContextAsync(null, async context =>
                {
                    var onyxVersion = await context.OnyxCommissionTermsVersions
                        .SingleAsync(version =>
                            version.Version == "append-only-onyx-v1");
                    context.Entry(onyxVersion).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                }));
        }

        [Fact]
        public async Task PersistedVersions_CannotBeDeleted()
        {
            var entryVersion = EntryCommissionTermsVersion.Create(
                "append-only-entry-delete",
                FridayBoundary,
                150m,
                250m,
                1250m);
            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(entryVersion);
                await context.SaveChangesAsync();
            });

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await UsingDbContextAsync(null, async context =>
                {
                    var persisted = await context.EntryCommissionTermsVersions
                        .SingleAsync(version => version.Id == entryVersion.Id);
                    context.EntryCommissionTermsVersions.Remove(persisted);
                    await context.SaveChangesAsync();
                }));
        }
    }
}
