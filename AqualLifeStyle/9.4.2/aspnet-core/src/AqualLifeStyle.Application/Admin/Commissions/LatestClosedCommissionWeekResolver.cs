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
    }
}
