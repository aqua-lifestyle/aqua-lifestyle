using System;
using AqualLifeStyle.Application.Admin.Commissions;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class LatestClosedCommissionWeekResolverTests
    {
        [Fact]
        public void Resolve_ReturnsTheLatestFullyClosedJohannesburgFridayToThursdayWeek()
        {
            var result = new LatestClosedCommissionWeekResolver().Resolve(
                new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc));

            result.PeriodStartUtc.ShouldBe(
                new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc));
            result.PeriodEndUtc.ShouldBe(
                new DateTime(
                    2026,
                    7,
                    23,
                    21,
                    59,
                    59,
                    999,
                    DateTimeKind.Utc).AddTicks(9999));
            result.TimeZoneId.ShouldBe("Africa/Johannesburg");
        }

        [Fact]
        public void Resolve_RejectsAMissingCalculationTime()
        {
            Should.Throw<ArgumentException>(() =>
                new LatestClosedCommissionWeekResolver().Resolve(default));
        }

        [Fact]
        public void Classifiers_DistinguishCanonicalLegacyAndMalformedPeriods()
        {
            var resolver = new LatestClosedCommissionWeekResolver();
            var canonicalStart =
                new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc);
            var canonicalEnd = canonicalStart.AddDays(7).AddTicks(-1);
            var legacyStart = canonicalStart.AddDays(3);
            var legacyEnd = legacyStart.AddDays(7).AddTicks(-1);

            resolver.IsCanonicalCycle(
                canonicalStart,
                canonicalEnd,
                LatestClosedCommissionWeekResolver.CommissionTimeZoneId)
                .ShouldBeTrue();
            resolver.IsLegacyMondayToSundayCycle(
                legacyStart,
                legacyEnd,
                LatestClosedCommissionWeekResolver.CommissionTimeZoneId)
                .ShouldBeTrue();
            resolver.IsCanonicalCycle(
                canonicalStart,
                canonicalStart.AddDays(2),
                LatestClosedCommissionWeekResolver.CommissionTimeZoneId)
                .ShouldBeFalse();
            resolver.OverlapsCanonicalCycle(legacyStart, legacyEnd)
                .ShouldBeTrue();
            resolver.ResolveFirstCycleStartAfter(legacyEnd).ShouldBe(
                canonicalStart.AddDays(14));
        }
    }
}
