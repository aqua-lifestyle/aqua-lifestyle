using System;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Canonical commission-cycle boundary semantics for effective-dated terms.
    /// The confirmed weekly commission cycle runs Friday 00:00 to Thursday
    /// 23:59:59.9999999 Africa/Johannesburg, so the only instants at which a
    /// terms version may become effective are Friday 00:00 Johannesburg cycle
    /// starts. This mirrors the canonical cycle definition owned by
    /// <c>LatestClosedCommissionWeekResolver</c> (Application layer); this helper
    /// is the domain-side validation used before a version can be persisted.
    /// </summary>
    public static class CommissionCycleBoundary
    {
        public const string TimeZoneId = "Africa/Johannesburg";
        public const DayOfWeek CycleStartDay = DayOfWeek.Friday;

        public static bool IsCanonicalCycleBoundary(DateTime utc)
        {
            if (utc == default)
            {
                return false;
            }

            var normalizedUtc = utc.Kind == DateTimeKind.Utc
                ? utc
                : utc.ToUniversalTime();
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                normalizedUtc,
                TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
            return local.DayOfWeek == CycleStartDay &&
                local.TimeOfDay == TimeSpan.Zero;
        }
    }
}
