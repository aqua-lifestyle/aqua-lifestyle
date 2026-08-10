using System;
using Abp.Dependency;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public sealed class ClosedCommissionWeek
    {
        public DateTime PeriodStartUtc { get; }
        public DateTime PeriodEndUtc { get; }
        public string TimeZoneId { get; }

        public ClosedCommissionWeek(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            string timeZoneId)
        {
            PeriodStartUtc = periodStartUtc;
            PeriodEndUtc = periodEndUtc;
            TimeZoneId = timeZoneId;
        }
    }

    /// <summary>
    /// Resolves the latest fully-closed AQGreen commission week for a given
    /// instant. The confirmed commission cycle is:
    ///
    /// <code>
    /// Friday 00:00 Africa/Johannesburg  →  Thursday 23:59:59.999 Africa/Johannesburg
    /// </code>
    ///
    /// A week is "closed" once its Thursday has elapsed. On each daily wake the
    /// worker resolves the latest closed week, so commissions for a just-closed
    /// Friday–Thursday cycle are produced promptly (robust to host restarts on
    /// the nominal Friday morning slot) without ever computing the open current
    /// cycle. This is the single authoritative cycle definition for both AQGreen
    /// and Onyx weekly commissions; do not reintroduce Monday–Sunday boundaries
    /// in the worker, admin service, or frontend.
    /// </summary>
    public class LatestClosedCommissionWeekResolver : ITransientDependency
    {
        public const string CommissionTimeZoneId = "Africa/Johannesburg";
        public const DayOfWeek WeekStartDay = DayOfWeek.Friday;

        public ClosedCommissionWeek Resolve(DateTime asOfUtc)
        {
            if (asOfUtc == default)
            {
                throw new ArgumentException(
                    "A calculation time is required.",
                    nameof(asOfUtc));
            }

            var normalizedUtc = asOfUtc.Kind == DateTimeKind.Utc
                ? asOfUtc
                : asOfUtc.ToUniversalTime();
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                CommissionTimeZoneId);
            var localAsOf = TimeZoneInfo.ConvertTimeFromUtc(
                normalizedUtc,
                timeZone);
            var daysSinceWeekStart =
                ((int)localAsOf.DayOfWeek - (int)WeekStartDay + 7) % 7;
            var currentWeekStartLocal = DateTime.SpecifyKind(
                localAsOf.Date.AddDays(-daysSinceWeekStart),
                DateTimeKind.Unspecified);
            var closedWeekStartLocal = currentWeekStartLocal.AddDays(-7);
            var closedWeekEndLocal = currentWeekStartLocal.AddTicks(-1);

            return new ClosedCommissionWeek(
                TimeZoneInfo.ConvertTimeToUtc(closedWeekStartLocal, timeZone),
                TimeZoneInfo.ConvertTimeToUtc(closedWeekEndLocal, timeZone),
                CommissionTimeZoneId);
        }

        public bool IsCanonicalCycle(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            string timeZoneId)
        {
            return IsCycle(
                periodStartUtc,
                periodEndUtc,
                timeZoneId,
                WeekStartDay);
        }

        public bool IsLegacyMondayToSundayCycle(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            string timeZoneId)
        {
            return IsCycle(
                periodStartUtc,
                periodEndUtc,
                timeZoneId,
                DayOfWeek.Monday);
        }

        public DateTime ResolveFirstCycleStartAfter(DateTime periodEndUtc)
        {
            var normalizedEnd = NormalizeUtc(periodEndUtc, nameof(periodEndUtc));
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                CommissionTimeZoneId);
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(normalizedEnd, timeZone);
            var daysUntilFriday =
                ((int)WeekStartDay - (int)localEnd.DayOfWeek + 7) % 7;
            var candidate = DateTime.SpecifyKind(
                localEnd.Date.AddDays(daysUntilFriday),
                DateTimeKind.Unspecified);
            if (candidate <= localEnd)
            {
                candidate = candidate.AddDays(7);
            }

            return TimeZoneInfo.ConvertTimeToUtc(candidate, timeZone);
        }

        public bool OverlapsCanonicalCycle(
            DateTime periodStartUtc,
            DateTime periodEndUtc)
        {
            if (periodStartUtc == default ||
                periodEndUtc == default ||
                periodEndUtc <= periodStartUtc)
            {
                return false;
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                CommissionTimeZoneId);
            var normalizedStart = periodStartUtc.Kind == DateTimeKind.Utc
                ? periodStartUtc
                : periodStartUtc.ToUniversalTime();
            var normalizedEnd = periodEndUtc.Kind == DateTimeKind.Utc
                ? periodEndUtc
                : periodEndUtc.ToUniversalTime();
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(
                normalizedStart,
                timeZone);
            var daysSinceFriday =
                ((int)localStart.DayOfWeek - (int)WeekStartDay + 7) % 7;
            var canonicalStartLocal = DateTime.SpecifyKind(
                localStart.Date.AddDays(-daysSinceFriday),
                DateTimeKind.Unspecified);
            var canonicalStartUtc = TimeZoneInfo.ConvertTimeToUtc(
                canonicalStartLocal,
                timeZone);
            var canonicalEndUtc = TimeZoneInfo.ConvertTimeToUtc(
                canonicalStartLocal.AddDays(7).AddTicks(-1),
                timeZone);

            return normalizedStart <= canonicalEndUtc &&
                normalizedEnd >= canonicalStartUtc;
        }

        private static bool IsCycle(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            string timeZoneId,
            DayOfWeek startDay)
        {
            if (!string.Equals(
                    timeZoneId,
                    CommissionTimeZoneId,
                    StringComparison.Ordinal) ||
                periodStartUtc == default ||
                periodEndUtc == default ||
                periodEndUtc <= periodStartUtc)
            {
                return false;
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                CommissionTimeZoneId);
            var normalizedStart = periodStartUtc.Kind == DateTimeKind.Utc
                ? periodStartUtc
                : periodStartUtc.ToUniversalTime();
            var normalizedEnd = periodEndUtc.Kind == DateTimeKind.Utc
                ? periodEndUtc
                : periodEndUtc.ToUniversalTime();
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(normalizedStart, timeZone);
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(normalizedEnd, timeZone);
            var expectedEnd = DateTime.SpecifyKind(
                localStart,
                DateTimeKind.Unspecified).AddDays(7).AddTicks(-1);

            return localStart.DayOfWeek == startDay &&
                localStart.TimeOfDay == TimeSpan.Zero &&
                Math.Abs((localEnd - expectedEnd).Ticks) < TimeSpan.TicksPerMillisecond;
        }

        private static DateTime NormalizeUtc(DateTime value, string parameterName)
        {
            if (value == default)
            {
                throw new ArgumentException(
                    "A commission cycle timestamp is required.",
                    parameterName);
            }

            return value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();
        }
    }
}
