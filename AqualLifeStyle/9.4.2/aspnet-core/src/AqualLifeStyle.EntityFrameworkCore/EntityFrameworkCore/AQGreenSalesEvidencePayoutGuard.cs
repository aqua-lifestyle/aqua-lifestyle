using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using Abp.UI;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenSalesEvidencePayoutGuard : IAQGreenSalesEvidencePayoutGuard, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _provider;
        private readonly IAQGreenWeeklySalesEligibilityMutationLock _reviewLock;
        public AQGreenSalesEvidencePayoutGuard(IDbContextProvider<AqualLifeStyleDbContext> provider,
            IAQGreenWeeklySalesEligibilityMutationLock reviewLock)
        { _provider = provider; _reviewLock = reviewLock; }

        public async Task EnsureAllowedAsync(EntryWeeklyCommission commission)
        {
            if (commission.StructuralModel != AQGreenCommissionStructuralModel.PlacementV2) return;
            var context = _provider.GetDbContext();
            var evidence = await context.AQGreenV2WeeklyCommissionEvidence.AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == commission.TenantId && item.Id == commission.Id);
            if (evidence == null)
                throw new UserFriendlyException("Commission evidence is unavailable; payout is prohibited.");
            if (!evidence.WeeklySalesEligibilityDecisionId.HasValue)
            {
                if (evidence.SalesApplicability != AQGreenWeeklySalesApplicability.NotApplicable ||
                    commission.TotalAmount != 0)
                    throw new UserFriendlyException("Commission sales evidence is unavailable; payout is prohibited.");
                return;
            }
            var decision = await context.AQGreenWeeklySalesEligibilityDecisions.AsNoTracking()
                .SingleAsync(item => item.TenantId == commission.TenantId &&
                    item.Id == evidence.WeeklySalesEligibilityDecisionId.Value);
            await _reviewLock.AcquireAsync(decision.TenantId, decision.ParticipantId,
                decision.CommissionWeekStartUtc, decision.SalesEligibilityRulesVersion);
            // Read AFTER the lock, under Read Committed, including for idempotent payout retries.
            if (await context.AQGreenSalesEvidenceInvalidations.AsNoTracking()
                .AnyAsync(item => item.TenantId == decision.TenantId && item.DecisionId == decision.Id))
                throw new UserFriendlyException("Commission is held pending authorized sales-evidence reconciliation.");
        }

        public async Task<IReadOnlyCollection<Guid>> GetHeldCommissionIdsAsync(IReadOnlyCollection<Guid> commissionIds)
        {
            var context = _provider.GetDbContext();
            return await (from evidence in context.AQGreenV2WeeklyCommissionEvidence.AsNoTracking()
                join invalidation in context.AQGreenSalesEvidenceInvalidations.AsNoTracking()
                    on new { evidence.TenantId, DecisionId = evidence.WeeklySalesEligibilityDecisionId }
                    equals new { invalidation.TenantId, DecisionId = (Guid?)invalidation.DecisionId }
                where commissionIds.Contains(evidence.Id)
                select evidence.Id).Distinct().ToArrayAsync();
        }
    }
}
