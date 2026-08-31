using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenWeeklySalesEligibilityMutationLock
    {
        Task AcquireAsync(
            int tenantId,
            Guid participantId,
            DateTime commissionWeekStartUtc,
            string salesEligibilityRulesVersion,
            CancellationToken cancellationToken = default);
    }

    public interface IAQGreenWeeklySalesEligibilityClock
    {
        Task<DateTime> GetUtcNowAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Narrow internal B5.4-facing contract. It exposes finalized review facts only;
    /// it neither activates AQGreen V2 nor reads commerce data.
    /// </summary>
    public interface IAQGreenWeeklySalesEligibilityDecisionReader
    {
        Task<AQGreenWeeklySalesEligibilitySnapshot> GetFinalDecisionAsync(
            int tenantId,
            Guid participantId,
            DateTime commissionWeekStartUtc,
            string salesEligibilityRulesVersion,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenWeeklySalesEligibilitySnapshot
    {
        public Guid DecisionId { get; }
        public int TenantId { get; }
        public Guid ParticipantId { get; }
        public DateTime CommissionWeekStartUtc { get; }
        public string SalesEligibilityRulesVersion { get; }
        public AQGreenWeeklySalesReviewStatus ReviewStatus { get; }
        public int? ReviewedSprayQuantity { get; }
        public int? ReviewedOneLitreQuantity { get; }
        public int? ReviewedFiveLitreQuantity { get; }
        public AQGreenWeeklySalesThresholdResult? ThresholdResult { get; }
        public DateTime ReviewedAt { get; }
        public long ReviewedByUserId { get; }
        public string RejectionReason { get; }

        public AQGreenWeeklySalesEligibilitySnapshot(
            Guid decisionId,
            int tenantId,
            Guid participantId,
            DateTime commissionWeekStartUtc,
            string salesEligibilityRulesVersion,
            AQGreenWeeklySalesReviewStatus reviewStatus,
            int? reviewedSprayQuantity,
            int? reviewedOneLitreQuantity,
            int? reviewedFiveLitreQuantity,
            AQGreenWeeklySalesThresholdResult? thresholdResult,
            DateTime reviewedAt,
            long reviewedByUserId,
            string rejectionReason)
        {
            DecisionId = decisionId;
            TenantId = tenantId;
            ParticipantId = participantId;
            CommissionWeekStartUtc = commissionWeekStartUtc;
            SalesEligibilityRulesVersion = salesEligibilityRulesVersion;
            ReviewStatus = reviewStatus;
            ReviewedSprayQuantity = reviewedSprayQuantity;
            ReviewedOneLitreQuantity = reviewedOneLitreQuantity;
            ReviewedFiveLitreQuantity = reviewedFiveLitreQuantity;
            ThresholdResult = thresholdResult;
            ReviewedAt = reviewedAt;
            ReviewedByUserId = reviewedByUserId;
            RejectionReason = rejectionReason;
        }
    }
}
