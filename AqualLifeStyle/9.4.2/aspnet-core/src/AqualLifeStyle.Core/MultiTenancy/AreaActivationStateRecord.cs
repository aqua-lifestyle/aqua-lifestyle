using System;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.MultiTenancy
{
    public enum AreaActivationStateRecordKind
    {
        Provisioned = 0,
        ObservedBaseline = 1,
        Changed = 2
    }

    public class AreaActivationStateRecord : CreationAuditedAggregateRoot<Guid>
    {
        public const int MaxJustificationLength = 500;

        public int TenantId { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime EffectiveAt { get; private set; }
        public DateTime RecordedAt { get; private set; }
        public long? RecordedByUserId { get; private set; }
        public string Justification { get; private set; }
        public AreaActivationStateRecordKind Kind { get; private set; }

        protected AreaActivationStateRecord()
        {
        }

        private AreaActivationStateRecord(
            Guid id,
            int tenantId,
            bool isActive,
            DateTime effectiveAt,
            DateTime recordedAt,
            long? recordedByUserId,
            string justification,
            AreaActivationStateRecordKind kind)
        {
            if (tenantId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenantId));
            }

            if (effectiveAt == default || effectiveAt.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Area activation effective time must be UTC.",
                    nameof(effectiveAt));
            }

            if (recordedAt == default || recordedAt.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Area activation recording time must be UTC.",
                    nameof(recordedAt));
            }

            if (effectiveAt > recordedAt)
            {
                throw new ArgumentException(
                    "Area activation state cannot be effective after it was recorded.",
                    nameof(effectiveAt));
            }

            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            var normalizedJustification = justification?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedJustification) ||
                normalizedJustification.Length > MaxJustificationLength)
            {
                throw new ArgumentException(
                    $"Justification must contain 1 to {MaxJustificationLength} characters.",
                    nameof(justification));
            }

            Id = id;
            TenantId = tenantId;
            IsActive = isActive;
            EffectiveAt = effectiveAt;
            RecordedAt = recordedAt;
            RecordedByUserId = recordedByUserId;
            Justification = normalizedJustification;
            Kind = kind;
        }

        public static AreaActivationStateRecord Record(
            Guid id,
            int tenantId,
            bool isActive,
            DateTime effectiveAt,
            DateTime recordedAt,
            long? recordedByUserId,
            string justification,
            AreaActivationStateRecordKind kind)
        {
            return new AreaActivationStateRecord(
                id,
                tenantId,
                isActive,
                effectiveAt,
                recordedAt,
                recordedByUserId,
                justification,
                kind);
        }
    }
}
