using System;
using AqualLifeStyle.Application.Admin.Commissions;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class LatestClosedCommissionWeekResolverTests
    {
        [Fact]
        public void Resolve_ReturnsTheLatestFullyClosedJohannesburgMondayToSundayWeek()
        {
            var result = new LatestClosedCommissionWeekResolver().Resolve(
                new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc));

            result.PeriodStartUtc.ShouldBe(
                new DateTime(2026, 7, 12, 22, 0, 0, DateTimeKind.Utc));
            result.PeriodEndUtc.ShouldBe(
                new DateTime(
                    2026,
                    7,
                    19,
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
    }
}
