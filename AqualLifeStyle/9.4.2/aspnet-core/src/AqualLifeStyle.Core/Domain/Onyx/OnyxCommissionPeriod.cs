using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    public class OnyxCommissionPeriod : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public DateTime PeriodStart { get; private set; }
        public DateTime PeriodEnd { get; private set; }
        public string TimeZoneId { get; private set; }
        public DateTime CalculatedAt { get; private set; }
        public string RulesVersion { get; private set; }

        protected OnyxCommissionPeriod()
        {
        }

        private OnyxCommissionPeriod(
            int tenantId,
            DateTime periodStart,
            DateTime periodEnd,
            string timeZoneId,
            DateTime calculatedAt,
            OnyxCommissionTerms terms)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (periodStart == default)
            {
                throw new ArgumentException("A period start is required.", nameof(periodStart));
            }

            if (periodEnd <= periodStart)
            {
                throw new ArgumentException(
                    "The period end must be after its start.",
                    nameof(periodEnd));
            }

            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                throw new ArgumentException(
                    "An Onyx commission time zone is required.",
                    nameof(timeZoneId));
            }

            if (calculatedAt <= periodEnd)
            {
                throw new ArgumentException(
                    "Commission can only be calculated after the period closes.",
                    nameof(calculatedAt));
            }

            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (periodStart < terms.EffectiveFrom)
            {
                throw new ArgumentException(
                    "The Onyx commission terms must be effective when the period begins.",
                    nameof(terms));
            }

            Id = Guid.NewGuid();
            TenantId = tenantId;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
            TimeZoneId = timeZoneId.Trim();
            CalculatedAt = calculatedAt;
            RulesVersion = terms.Version;
        }

        public static OnyxCommissionPeriod CreateClosedPeriod(
            int tenantId,
            DateTime periodStart,
            DateTime periodEnd,
            string timeZoneId,
            DateTime calculatedAt,
            OnyxCommissionTerms terms)
        {
            return new OnyxCommissionPeriod(
                tenantId,
                periodStart,
                periodEnd,
                timeZoneId,
                calculatedAt,
                terms);
        }
    }
}
