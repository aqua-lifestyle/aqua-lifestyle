using System;
using System.Collections.Generic;
using Abp.Auditing;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Application.Admin.Commissions.Dto
{
    public class BeginAQGreenWeeklySalesReviewInput
    {
        public int TenantId { get; set; }
        public Guid ParticipantId { get; set; }
        public DateTime CommissionWeekStartUtc { get; set; }
    }

    public class ConfirmAQGreenWeeklySalesEligibilityInput
        : BeginAQGreenWeeklySalesReviewInput
    {
        public int SprayQuantity { get; set; }
        public int OneLitreQuantity { get; set; }
        public int FiveLitreQuantity { get; set; }
        [DisableAuditing]
        public List<string> EvidenceReferences { get; set; } = new();
    }

    public class RejectAQGreenWeeklySalesEligibilityInput
        : BeginAQGreenWeeklySalesReviewInput
    {
        public string RejectionReason { get; set; }
        [DisableAuditing]
        public List<string> EvidenceReferences { get; set; } = new();
    }

    public class AQGreenWeeklySalesEligibilityDecisionDto
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public Guid ParticipantId { get; set; }
        public DateTime CommissionWeekStartUtc { get; set; }
        public string SalesEligibilityRulesVersion { get; set; }
        public AQGreenWeeklySalesReviewStatus ReviewStatus { get; set; }
        public int? ReviewedSprayQuantity { get; set; }
        public int? ReviewedOneLitreQuantity { get; set; }
        public int? ReviewedFiveLitreQuantity { get; set; }
        public AQGreenWeeklySalesThresholdResult? ThresholdResult { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public long? ReviewedByUserId { get; set; }
        public string RejectionReason { get; set; }
    }
}
