using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Customers
{
    public class CustomerAreaAssignment : FullAuditedEntity<Guid>, IMustHaveTenant
    {
        public const int MaxReasonLength = 500;

        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public Guid AreaId { get; private set; }
        public DateTime EffectiveFrom { get; private set; }
        public DateTime? EffectiveTo { get; private set; }
        public bool IsMigrationBaseline { get; private set; }
        public string Reason { get; private set; }
        public bool IsCurrent => !EffectiveTo.HasValue;

        protected CustomerAreaAssignment()
        {
        }

        internal static CustomerAreaAssignment Start(
            int tenantId,
            Guid areaId,
            DateTime effectiveFrom,
            string reason,
            bool isMigrationBaseline = false)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (areaId == Guid.Empty) throw new ArgumentException("An Area is required.", nameof(areaId));
            if (effectiveFrom == default) throw new ArgumentException("An effective time is required.", nameof(effectiveFrom));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An assignment reason is required.", nameof(reason));
            var normalizedReason = reason.Trim();
            if (normalizedReason.Length > MaxReasonLength)
                throw new ArgumentException($"Assignment reason cannot exceed {MaxReasonLength} characters.", nameof(reason));

            return new CustomerAreaAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AreaId = areaId,
                EffectiveFrom = effectiveFrom,
                IsMigrationBaseline = isMigrationBaseline,
                Reason = normalizedReason
            };
        }

        internal void End(DateTime effectiveTo)
        {
            if (EffectiveTo.HasValue) throw new InvalidOperationException("The Area assignment has already ended.");
            if (effectiveTo < EffectiveFrom)
                throw new ArgumentException("An Area assignment cannot end before it starts.", nameof(effectiveTo));
            EffectiveTo = effectiveTo;
        }
    }
}
