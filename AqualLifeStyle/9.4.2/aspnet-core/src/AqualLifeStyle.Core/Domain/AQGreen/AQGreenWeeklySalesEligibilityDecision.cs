using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.AQGreen
{
    public sealed class AQGreenWeeklySalesEligibilityDecision
        : CreationAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public const int MaximumRejectionReasonLength = 1000;

        private readonly List<AQGreenWeeklySalesEvidenceReference>
            _evidenceReferences = new();

        public int TenantId { get; private set; }
        public Guid ParticipantId { get; private set; }
        public DateTime CommissionWeekStartUtc { get; private set; }
        public string SalesEligibilityRulesVersion { get; private set; }
        public AQGreenWeeklySalesReviewStatus ReviewStatus { get; private set; }
        public int? ReviewedSprayQuantity { get; private set; }
        public int? ReviewedOneLitreQuantity { get; private set; }
        public int? ReviewedFiveLitreQuantity { get; private set; }
        public AQGreenWeeklySalesThresholdResult? ThresholdResult { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public long? ReviewedByUserId { get; private set; }
        public string RejectionReason { get; private set; }
        public IReadOnlyCollection<AQGreenWeeklySalesEvidenceReference>
            EvidenceReferences => _evidenceReferences.AsReadOnly();

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenWeeklySalesEligibilityDecision()
        {
        }

        public static AQGreenWeeklySalesEligibilityDecision Begin(
            int tenantId,
            Guid participantId,
            AQGreenCommissionWeek commissionWeek,
            string salesEligibilityRulesVersion)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (commissionWeek == null)
                throw new ArgumentNullException(nameof(commissionWeek));
            if (!AQGreenWeeklySalesEligibilityRules.IsSupportedVersion(
                    salesEligibilityRulesVersion))
            {
                throw new AQGreenWeeklySalesEligibilityVersionNotSupportedException(
                    salesEligibilityRulesVersion);
            }

            return new AQGreenWeeklySalesEligibilityDecision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ParticipantId = participantId,
                CommissionWeekStartUtc = commissionWeek.StartUtc,
                SalesEligibilityRulesVersion = salesEligibilityRulesVersion,
                ReviewStatus = AQGreenWeeklySalesReviewStatus.HeldForEvidence
            };
        }

        public void AddManualEvidence(string technicalReference, DateTime recordedAt)
        {
            EnsureHeld();
            var evidence = AQGreenWeeklySalesEvidenceReference.Record(
                TenantId,
                Id,
                AQGreenWeeklySalesEvidenceSource.ManualReview,
                technicalReference,
                recordedAt);
            if (_evidenceReferences.Any(item =>
                    item.Source == evidence.Source &&
                    string.Equals(
                        item.TechnicalReference,
                        evidence.TechnicalReference,
                        StringComparison.Ordinal)))
            {
                return;
            }

            _evidenceReferences.Add(evidence);
        }

        public void Confirm(
            AQGreenWeeklySalesQuantities quantities,
            long reviewedByUserId,
            DateTime reviewedAt)
        {
            EnsureHeld();
            EnsureFinalizationEvidence(reviewedByUserId, reviewedAt);
            if (quantities == null) throw new ArgumentNullException(nameof(quantities));

            var result = AQGreenWeeklySalesEligibilityEvaluator.Evaluate(
                SalesEligibilityRulesVersion,
                quantities);
            ReviewedSprayQuantity = quantities.Spray;
            ReviewedOneLitreQuantity = quantities.OneLitre;
            ReviewedFiveLitreQuantity = quantities.FiveLitre;
            ThresholdResult = result;
            ReviewedByUserId = reviewedByUserId;
            ReviewedAt = reviewedAt;
            ReviewStatus = AQGreenWeeklySalesReviewStatus.Confirmed;
        }

        public void Reject(
            string rejectionReason,
            long reviewedByUserId,
            DateTime reviewedAt)
        {
            EnsureHeld();
            EnsureFinalizationEvidence(reviewedByUserId, reviewedAt);
            var normalizedReason = rejectionReason?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReason))
                throw new ArgumentException(
                    "A rejection reason is required.",
                    nameof(rejectionReason));
            if (normalizedReason.Length > MaximumRejectionReasonLength)
                throw new ArgumentException(
                    $"Rejection reasons cannot exceed {MaximumRejectionReasonLength} characters.",
                    nameof(rejectionReason));

            RejectionReason = normalizedReason;
            ReviewedByUserId = reviewedByUserId;
            ReviewedAt = reviewedAt;
            ReviewStatus = AQGreenWeeklySalesReviewStatus.Rejected;
        }

        private void EnsureFinalizationEvidence(
            long reviewedByUserId,
            DateTime reviewedAt)
        {
            if (_evidenceReferences.Count == 0)
                throw new InvalidOperationException(
                    "A final weekly-sales review requires evidence.");
            if (reviewedByUserId <= 0)
                throw new ArgumentOutOfRangeException(nameof(reviewedByUserId));
            if (reviewedAt == default || reviewedAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC review time is required.",
                    nameof(reviewedAt));

            var week = AQGreenCommissionWeek.FromStartUtc(CommissionWeekStartUtc);
            if (reviewedAt < week.EndExclusiveUtc)
                throw new InvalidOperationException(
                    "A weekly-sales review cannot be finalized before the commission week closes.");
        }

        private void EnsureHeld()
        {
            if (ReviewStatus != AQGreenWeeklySalesReviewStatus.HeldForEvidence)
                throw new InvalidOperationException(
                    "A finalized weekly-sales eligibility decision is immutable.");
        }
    }

    public sealed class AQGreenWeeklySalesEvidenceReference
        : Entity<Guid>, IMustHaveTenant
    {
        public const int MaximumTechnicalReferenceLength = 256;

        public int TenantId { get; private set; }
        public Guid DecisionId { get; private set; }
        public AQGreenWeeklySalesEvidenceSource Source { get; private set; }
        public string TechnicalReference { get; private set; }
        public DateTime RecordedAt { get; private set; }

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenWeeklySalesEvidenceReference()
        {
        }

        internal static AQGreenWeeklySalesEvidenceReference Record(
            int tenantId,
            Guid decisionId,
            AQGreenWeeklySalesEvidenceSource source,
            string technicalReference,
            DateTime recordedAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (decisionId == Guid.Empty)
                throw new ArgumentException(
                    "A weekly-sales decision is required.",
                    nameof(decisionId));
            if (source != AQGreenWeeklySalesEvidenceSource.ManualReview)
                throw new ArgumentOutOfRangeException(
                    nameof(source),
                    source,
                    "The evidence source is unsupported.");
            var normalizedReference = technicalReference?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReference))
                throw new ArgumentException(
                    "A technical evidence reference is required.",
                    nameof(technicalReference));
            if (normalizedReference.Length > MaximumTechnicalReferenceLength)
                throw new ArgumentException(
                    $"Technical evidence references cannot exceed {MaximumTechnicalReferenceLength} characters.",
                    nameof(technicalReference));
            if (recordedAt == default || recordedAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC evidence-recording time is required.",
                    nameof(recordedAt));

            return new AQGreenWeeklySalesEvidenceReference
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DecisionId = decisionId,
                Source = source,
                TechnicalReference = normalizedReference,
                RecordedAt = recordedAt
            };
        }
    }
}
