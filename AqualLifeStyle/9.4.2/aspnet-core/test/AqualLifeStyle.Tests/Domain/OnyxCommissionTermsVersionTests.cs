using System;
using AqualLifeStyle.Domain.Onyx;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxCommissionTermsVersionTests
    {
        private static readonly DateTime FridayBoundary =
            new(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Create_AcceptsOnlyCanonicalFridayJohannesburgBoundaries()
        {
            var version = OnyxCommissionTermsVersion.Create(
                "onyx-2026-07",
                FridayBoundary,
                50m,
                20m,
                12.62m,
                5m,
                4m);

            version.Version.ShouldBe("onyx-2026-07");
            version.EffectiveAt.ShouldBe(FridayBoundary);
            version.LevelOnePerPersonRate.ShouldBe(50m);
            version.LevelTwoPerPersonRate.ShouldBe(20m);
            version.LevelThreePerPersonRate.ShouldBe(12.62m);
            version.LevelFourPerPersonRate.ShouldBe(5m);
            version.LevelFivePerPersonRate.ShouldBe(4m);
            version.Currency.ShouldBe("ZAR");
        }

        [Fact]
        public void Create_RejectsAnEffectiveDateHalfwayThroughACycle()
        {
            var midCycle = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    "onyx-mid-cycle",
                    midCycle,
                    50m,
                    20m,
                    12.62m,
                    5m,
                    4m));
        }

        [Fact]
        public void Create_RejectsMissingOrInvalidIdentityAndRates()
        {
            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    " ",
                    FridayBoundary,
                    50m,
                    20m,
                    12.62m,
                    5m,
                    4m));
            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    "onyx-invalid-rate",
                    FridayBoundary,
                    0m,
                    20m,
                    12.62m,
                    5m,
                    4m));
            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    "onyx-invalid-rate",
                    FridayBoundary,
                    50m,
                    20m,
                    0m,
                    5m,
                    4m));
            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    "onyx-invalid-rate",
                    FridayBoundary,
                    50m,
                    20m,
                    12.62m,
                    5m,
                    -4m));
            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    "onyx-invalid-currency",
                    FridayBoundary,
                    50m,
                    20m,
                    12.62m,
                    5m,
                    4m,
                    "Rands"));
            Should.Throw<ArgumentException>(() =>
                OnyxCommissionTermsVersion.Create(
                    "onyx-missing-boundary",
                    default,
                    50m,
                    20m,
                    12.62m,
                    5m,
                    4m));
        }

        [Fact]
        public void ToTerms_ProjectsTheImmutableCalculationTerms()
        {
            var version = OnyxCommissionTermsVersion.Create(
                "onyx-2026-07",
                FridayBoundary,
                50m,
                20m,
                12.62m,
                5m,
                4m,
                "ZAR");

            var terms = version.ToTerms();

            terms.Version.ShouldBe("onyx-2026-07");
            terms.EffectiveFrom.ShouldBe(FridayBoundary);
            terms.GetPerPersonRate(OnyxNetworkLevel.Level1).ShouldBe(50m);
            terms.GetPerPersonRate(OnyxNetworkLevel.Level3).ShouldBe(12.62m);
            terms.GetPerPersonRate(OnyxNetworkLevel.Level5).ShouldBe(4m);
            terms.Currency.ShouldBe("ZAR");
        }
    }
}
