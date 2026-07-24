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

    public class LatestClosedCommissionWeekResolver : ITransientDependency
    {
        public const string CommissionTimeZoneId = "Africa/Johannesburg";

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
            var daysSinceMonday =
                ((int)localAsOf.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var currentWeekStartLocal = DateTime.SpecifyKind(
                localAsOf.Date.AddDays(-daysSinceMonday),
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
