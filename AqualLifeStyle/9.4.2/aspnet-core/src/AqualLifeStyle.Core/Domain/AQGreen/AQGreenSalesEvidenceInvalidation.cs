using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>Append-only pending reconciliation, never an adjustment or reversal.</summary>
    public sealed class AQGreenSalesEvidenceInvalidation : Entity<Guid>, IMustHaveTenant
    {
        public const int MaximumReasonLength = 1000;
        public int TenantId { get; private set; }
        public Guid DecisionId { get; private set; }
        // The original evidence ID is also the idempotency key: one invalidation per fact.
        public Guid EvidenceId { get; private set; }
        public string Reason { get; private set; }
        public long RecordedByUserId { get; private set; }
        public DateTime RecordedAtUtc { get; private set; }

        int IMustHaveTenant.TenantId { get => TenantId; set => TenantId = value; }
        private AQGreenSalesEvidenceInvalidation() { }

        public static AQGreenSalesEvidenceInvalidation Record(
            AQGreenWeeklySalesEligibilityDecision decision, Guid evidenceId,
            string reason, long actorId, DateTime recordedAtUtc)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (decision.ReviewStatus != AQGreenWeeklySalesReviewStatus.Confirmed ||
                !System.Linq.Enumerable.Any(decision.EvidenceReferences, item => item.Id == evidenceId))
                throw new InvalidOperationException("Only evidence belonging to a finalized confirmed review can be invalidated.");
            var normalizedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > MaximumReasonLength)
                throw new ArgumentException("A bounded invalidation reason is required.", nameof(reason));
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (!decision.ReviewedAt.HasValue || recordedAtUtc == default || recordedAtUtc.Kind != DateTimeKind.Utc || recordedAtUtc < decision.ReviewedAt.Value)
                throw new ArgumentException("An authoritative UTC time after review is required.", nameof(recordedAtUtc));
            return new AQGreenSalesEvidenceInvalidation
            {
                Id = Guid.NewGuid(), TenantId = decision.TenantId, DecisionId = decision.Id,
                EvidenceId = evidenceId, Reason = normalizedReason,
                RecordedByUserId = actorId, RecordedAtUtc = recordedAtUtc
            };
        }
    }
}
