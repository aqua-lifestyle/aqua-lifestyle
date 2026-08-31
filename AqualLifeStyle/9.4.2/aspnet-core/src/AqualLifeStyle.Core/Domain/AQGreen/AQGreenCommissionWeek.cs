using System;

namespace AqualLifeStyle.Domain.AQGreen
{
    public sealed class AQGreenCommissionWeek
    {
        public const string TimeZoneId = "Africa/Johannesburg";
        public const DayOfWeek StartDay = DayOfWeek.Friday;

        public DateTime StartUtc { get; }
        public DateTime EndExclusiveUtc { get; }

        private AQGreenCommissionWeek(DateTime startUtc, DateTime endExclusiveUtc)
        {
            StartUtc = startUtc;
            EndExclusiveUtc = endExclusiveUtc;
        }

        public static AQGreenCommissionWeek FromStartUtc(DateTime startUtc)
        {
            if (startUtc == default || startUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "A canonical UTC commission-week start is required.",
                    nameof(startUtc));

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(startUtc, timeZone);
            if (localStart.DayOfWeek != StartDay ||
                localStart.TimeOfDay != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "AQGreen commission weeks start Friday 00:00 Africa/Johannesburg.",
                    nameof(startUtc));
            }

            var localEnd = DateTime.SpecifyKind(
                localStart.Date.AddDays(7),
                DateTimeKind.Unspecified);
            return new AQGreenCommissionWeek(
                startUtc,
                TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
        }

        public bool Contains(DateTime instantUtc)
        {
            if (instantUtc == default || instantUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "A UTC instant is required.",
                    nameof(instantUtc));
            return instantUtc >= StartUtc && instantUtc < EndExclusiveUtc;
        }
    }
}
