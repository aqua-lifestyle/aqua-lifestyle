using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenSalesEvidencePayoutGuard
    {
        // Called within the payout transaction, after the per-commission lock.
        Task EnsureAllowedAsync(EntryWeeklyCommission commission);
        Task<IReadOnlyCollection<Guid>> GetHeldCommissionIdsAsync(IReadOnlyCollection<Guid> commissionIds);
    }
}
