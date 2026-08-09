using System;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Append-only host evidence defining when AQGreen monthly obligations are due.
    /// A new business decision is represented by a new version rather than by
    /// changing an existing row.
    /// </summary>
    public sealed class EntryMonthlyObligationDuePolicy
        : CreationAuditedAggregateRoot<Guid>
    {
        public const int MaxVersionLength = 64;
        public const string TimeZoneId = "Africa/Johannesburg";

        public string Version { get; private set; }
        public int DueDayOfMonth { get; private set; }
        public DateTime EffectiveFrom { get; private set; }

        private EntryMonthlyObligationDuePolicy()
        {
        }

        private EntryMonthlyObligationDuePolicy(
            string version,
            int dueDayOfMonth,
            DateTime effectiveFrom)
        {
            var normalizedVersion = version?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                throw new ArgumentException("A due-policy version is required.", nameof(version));
            }

            if (normalizedVersion.Length > MaxVersionLength)
            {
                throw new ArgumentException(
                    $"The due-policy version cannot exceed {MaxVersionLength} characters.",
                    nameof(version));
            }

            if (dueDayOfMonth < 1 || dueDayOfMonth > 28)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dueDayOfMonth),
                    "The due day must be between 1 and 28.");
            }

            if (!IsCanonicalJohannesburgMonthStart(effectiveFrom))
            {
                throw new ArgumentException(
                    "The effective time must be the first obligation month at 00:00 Africa/Johannesburg, stored as UTC.",
                    nameof(effectiveFrom));
            }

            Id = Guid.NewGuid();
            Version = normalizedVersion;
            DueDayOfMonth = dueDayOfMonth;
            EffectiveFrom = effectiveFrom;
        }

        public static EntryMonthlyObligationDuePolicy Create(
            string version,
            int dueDayOfMonth,
            DateTime effectiveFrom)
        {
            return new EntryMonthlyObligationDuePolicy(
                version,
                dueDayOfMonth,
                effectiveFrom);
        }

        public DateTime ResolveDueAtUtc(int periodYear, int periodMonth)
        {
            if (!HasValidEvidence())
            {
                throw new InvalidOperationException("The stored AQGreen due policy is invalid.");
            }

            ValidatePeriod(periodYear, periodMonth);
            var localDueAt = new DateTime(
                periodYear,
                periodMonth,
                DueDayOfMonth,
                0,
                0,
                0,
                DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localDueAt, JohannesburgTimeZone());
        }

        public bool HasValidEvidence()
        {
            return !string.IsNullOrWhiteSpace(Version) &&
                string.Equals(Version, Version.Trim(), StringComparison.Ordinal) &&
                Version.Length <= MaxVersionLength &&
                DueDayOfMonth >= 1 &&
                DueDayOfMonth <= 28 &&
                IsCanonicalJohannesburgMonthStart(NormalizeStoredUtc(EffectiveFrom));
        }

        public static DateTime JohannesburgMonthStartUtc(int year, int month)
        {
            ValidatePeriod(year, month);
            var localMonthStart = new DateTime(
                year,
                month,
                1,
                0,
                0,
                0,
                DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(
                localMonthStart,
                JohannesburgTimeZone());
        }

        public static (int Year, int Month) JohannesburgMonth(DateTime utcInstant)
        {
            var normalizedUtc = NormalizeUtcInstant(utcInstant, nameof(utcInstant));
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                normalizedUtc,
                JohannesburgTimeZone());
            return (local.Year, local.Month);
        }

        private static bool IsCanonicalJohannesburgMonthStart(DateTime effectiveFrom)
        {
            if (effectiveFrom == default || effectiveFrom.Kind != DateTimeKind.Utc)
            {
                return false;
            }

            var local = TimeZoneInfo.ConvertTimeFromUtc(
                effectiveFrom,
                JohannesburgTimeZone());
            return local.Day == 1 && local.TimeOfDay == TimeSpan.Zero;
        }

        private static DateTime NormalizeStoredUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value;
        }

        private static DateTime NormalizeUtcInstant(DateTime value, string parameterName)
        {
            if (value == default)
            {
                throw new ArgumentException("A UTC instant is required.", parameterName);
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value;
        }

        private static TimeZoneInfo JohannesburgTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }

        private static void ValidatePeriod(int year, int month)
        {
            if (year < 2000 || year > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(year));
            }

            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month));
            }
        }
    }
}
