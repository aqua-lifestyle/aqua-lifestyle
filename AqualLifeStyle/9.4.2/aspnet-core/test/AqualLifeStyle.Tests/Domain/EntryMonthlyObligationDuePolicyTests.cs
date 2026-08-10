using System;
using AqualLifeStyle.Domain.Onyx;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EntryMonthlyObligationDuePolicyTests
    {
        [Theory]
        [InlineData(1, 7, 31)]
        [InlineData(28, 8, 27)]
        public void DayOneAndTwentyEight_ResolveAtJohannesburgMidnightInUtc(
            int dueDay,
            int expectedUtcMonth,
            int expectedUtcDay)
        {
            var policy = EntryMonthlyObligationDuePolicy.Create(
                $"due-day-{dueDay}",
                dueDay,
                EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8));

            var dueAtUtc = policy.ResolveDueAtUtc(2026, 8);

            dueAtUtc.ShouldBe(new DateTime(
                2026,
                expectedUtcMonth,
                expectedUtcDay,
                22,
                0,
                0,
                DateTimeKind.Utc));
        }

        [Fact]
        public void JohannesburgMonth_UsesLocalMonthAtUtcBoundary()
        {
            var month = EntryMonthlyObligationDuePolicy.JohannesburgMonth(
                new DateTime(2026, 7, 31, 22, 30, 0, DateTimeKind.Utc));

            month.Year.ShouldBe(2026);
            month.Month.ShouldBe(8);
        }

        [Theory]
        [InlineData(null, 10)]
        [InlineData("", 10)]
        [InlineData("valid", 0)]
        [InlineData("valid", 29)]
        public void InvalidVersionOrDueDay_IsRejected(string version, int dueDay)
        {
            Should.Throw<ArgumentException>(() =>
                EntryMonthlyObligationDuePolicy.Create(
                    version,
                    dueDay,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)));
        }

        [Fact]
        public void VersionLongerThanMaximum_IsRejected()
        {
            Should.Throw<ArgumentException>(() =>
                EntryMonthlyObligationDuePolicy.Create(
                    new string('v', EntryMonthlyObligationDuePolicy.MaxVersionLength + 1),
                    10,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)));
        }

        [Fact]
        public void EffectiveTimeMustBeStoredAsUtc()
        {
            Should.Throw<ArgumentException>(() =>
                EntryMonthlyObligationDuePolicy.Create(
                    "non-utc-effective",
                    10,
                    new DateTime(
                        2026,
                        7,
                        31,
                        22,
                        0,
                        0,
                        DateTimeKind.Unspecified)));
        }

        [Theory]
        [InlineData(2026, 8, 1, 0)]
        [InlineData(2026, 7, 31, 23)]
        public void NonCanonicalEffectiveTime_IsRejected(
            int year,
            int month,
            int day,
            int hour)
        {
            Should.Throw<ArgumentException>(() =>
                EntryMonthlyObligationDuePolicy.Create(
                    "invalid-effective",
                    10,
                    new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc)));
        }
    }
}
