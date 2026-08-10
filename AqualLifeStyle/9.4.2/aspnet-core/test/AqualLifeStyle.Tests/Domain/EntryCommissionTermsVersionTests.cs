using System;
using AqualLifeStyle.Domain.Onyx;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EntryCommissionTermsVersionTests
    {
        private static readonly DateTime FridayBoundary =
            new(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Create_AcceptsOnlyCanonicalFridayJohannesburgBoundaries()
        {
            var version = EntryCommissionTermsVersion.Create(
                "entry-2026-07",
                FridayBoundary,
                150m,
                250m,
                1250m);

            version.Version.ShouldBe("entry-2026-07");
            version.EffectiveAt.ShouldBe(FridayBoundary);
            version.LevelOneComponentAmount.ShouldBe(150m);
            version.LevelTwoComponentAmount.ShouldBe(250m);
            version.LevelThreeComponentAmount.ShouldBe(1250m);
            version.Currency.ShouldBe("ZAR");
        }

        [Fact]
        public void Create_RejectsAnEffectiveDateHalfwayThroughACycle()
        {
            var midCycle = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "entry-mid-cycle",
                    midCycle,
                    150m,
                    250m,
                    1250m));
        }

        [Fact]
        public void Create_RejectsAThursdayBoundaryThatIsNotACycleStart()
        {
            var thursday = new DateTime(2026, 7, 15, 22, 0, 0, DateTimeKind.Utc);

            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "entry-thursday",
                    thursday,
                    150m,
                    250m,
                    1250m));
        }

        [Fact]
        public void Create_RejectsMissingOrInvalidIdentityAndRates()
        {
            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    " ",
                    FridayBoundary,
                    150m,
                    250m,
                    1250m));
            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "entry-invalid-amount",
                    FridayBoundary,
                    0m,
                    250m,
                    1250m));
            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "entry-invalid-amount",
                    FridayBoundary,
                    150m,
                    -1m,
                    1250m));
            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "entry-invalid-currency",
                    FridayBoundary,
                    150m,
                    250m,
                    1250m,
                    "Rands"));
            Should.Throw<ArgumentException>(() =>
                EntryCommissionTermsVersion.Create(
                    "entry-missing-boundary",
                    default,
                    150m,
                    250m,
                    1250m));
        }

        [Fact]
        public void ToTerms_ProjectsTheImmutableCalculationTerms()
        {
            var version = EntryCommissionTermsVersion.Create(
                "entry-2026-07",
                FridayBoundary,
                150m,
                250m,
                1250m,
                "ZAR");

            var terms = version.ToTerms();

            terms.Version.ShouldBe("entry-2026-07");
            terms.EffectiveFrom.ShouldBe(FridayBoundary);
            terms.GetComponentAmount(1).ShouldBe(150m);
            terms.GetComponentAmount(2).ShouldBe(250m);
            terms.GetComponentAmount(3).ShouldBe(1250m);
            terms.Currency.ShouldBe("ZAR");
        }
    }
}
