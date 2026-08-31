using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenWeeklySalesEligibilityDecisionReader
        : IAQGreenWeeklySalesEligibilityDecisionReader, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenWeeklySalesEligibilityDecisionReader(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<AQGreenWeeklySalesEligibilitySnapshot> GetFinalDecisionAsync(
            int tenantId,
            Guid participantId,
            DateTime commissionWeekStartUtc,
            string salesEligibilityRulesVersion,
            CancellationToken cancellationToken = default)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (participantId == Guid.Empty)
                throw new ArgumentException("A participation is required.", nameof(participantId));
            AQGreenCommissionWeek.FromStartUtc(commissionWeekStartUtc);
            if (!AQGreenWeeklySalesEligibilityRules.IsSupportedVersion(
                    salesEligibilityRulesVersion))
            {
                throw new AQGreenWeeklySalesEligibilityVersionNotSupportedException(
                    salesEligibilityRulesVersion);
            }

            var decision = await _dbContextProvider.GetDbContext()
                .AQGreenWeeklySalesEligibilityDecisions
                .AsNoTracking()
                .Include(item => item.EvidenceReferences)
                .SingleOrDefaultAsync(item =>
                        item.TenantId == tenantId &&
                        item.ParticipantId == participantId &&
                        item.CommissionWeekStartUtc == commissionWeekStartUtc &&
                        item.SalesEligibilityRulesVersion == salesEligibilityRulesVersion,
                    cancellationToken);

            if (decision == null ||
                decision.ReviewStatus == AQGreenWeeklySalesReviewStatus.HeldForEvidence)
            {
                throw new AQGreenWeeklySalesEligibilityUnavailableException(
                    "No finalized AQGreen weekly-sales eligibility decision is available.");
            }

            ValidateFinalDecision(decision);
            return new AQGreenWeeklySalesEligibilitySnapshot(
                decision.Id,
                decision.TenantId,
                decision.ParticipantId,
                decision.CommissionWeekStartUtc,
                decision.SalesEligibilityRulesVersion,
                decision.ReviewStatus,
                decision.ReviewedSprayQuantity,
                decision.ReviewedOneLitreQuantity,
                decision.ReviewedFiveLitreQuantity,
                decision.ThresholdResult,
                decision.ReviewedAt.Value,
                decision.ReviewedByUserId.Value,
                decision.RejectionReason);
        }

        private static void ValidateFinalDecision(
            AQGreenWeeklySalesEligibilityDecision decision)
        {
            if (!AQGreenWeeklySalesEligibilityRules.IsSupportedVersion(
                    decision.SalesEligibilityRulesVersion))
            {
                throw new AQGreenWeeklySalesEligibilityIntegrityException(
                    "The stored weekly-sales rules version is unsupported.");
            }
            if (decision.EvidenceReferences.Count == 0 ||
                decision.ReviewedAt == null ||
                decision.ReviewedByUserId == null ||
                decision.ReviewedByUserId <= 0)
            {
                throw new AQGreenWeeklySalesEligibilityIntegrityException(
                    "The finalized weekly-sales decision has incomplete evidence or audit facts.");
            }

            if (decision.ReviewStatus == AQGreenWeeklySalesReviewStatus.Confirmed)
            {
                if (decision.ReviewedSprayQuantity == null ||
                    decision.ReviewedOneLitreQuantity == null ||
                    decision.ReviewedFiveLitreQuantity == null ||
                    decision.ThresholdResult == null ||
                    decision.RejectionReason != null)
                {
                    throw new AQGreenWeeklySalesEligibilityIntegrityException(
                        "The confirmed weekly-sales decision has an invalid state shape.");
                }
                AQGreenWeeklySalesThresholdResult recalculated;
                try
                {
                    recalculated = AQGreenWeeklySalesEligibilityEvaluator.Evaluate(
                        decision.SalesEligibilityRulesVersion,
                        new AQGreenWeeklySalesQuantities(
                            decision.ReviewedSprayQuantity.Value,
                            decision.ReviewedOneLitreQuantity.Value,
                            decision.ReviewedFiveLitreQuantity.Value));
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is AQGreenWeeklySalesEligibilityVersionNotSupportedException)
                {
                    throw new AQGreenWeeklySalesEligibilityIntegrityException(
                        "The confirmed weekly-sales quantities cannot be safely evaluated.");
                }
                if (recalculated != decision.ThresholdResult)
                {
                    throw new AQGreenWeeklySalesEligibilityIntegrityException(
                        "The stored weekly-sales threshold result does not match the versioned evaluator.");
                }
            }
            else if (decision.ReviewStatus == AQGreenWeeklySalesReviewStatus.Rejected)
            {
                if (decision.ReviewedSprayQuantity != null ||
                    decision.ReviewedOneLitreQuantity != null ||
                    decision.ReviewedFiveLitreQuantity != null ||
                    decision.ThresholdResult != null ||
                    string.IsNullOrWhiteSpace(decision.RejectionReason))
                {
                    throw new AQGreenWeeklySalesEligibilityIntegrityException(
                        "The rejected weekly-sales decision has an invalid state shape.");
                }
            }
            else
            {
                throw new AQGreenWeeklySalesEligibilityIntegrityException(
                    "The stored weekly-sales review status is unsupported.");
            }

            var week = AQGreenCommissionWeek.FromStartUtc(
                decision.CommissionWeekStartUtc);
            if (decision.ReviewedAt < week.EndExclusiveUtc)
            {
                throw new AQGreenWeeklySalesEligibilityIntegrityException(
                    "The stored weekly-sales decision predates the commission-week close.");
            }
        }
    }
}
