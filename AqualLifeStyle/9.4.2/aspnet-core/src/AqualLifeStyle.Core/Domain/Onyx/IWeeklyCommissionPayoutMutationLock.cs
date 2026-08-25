using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Serialises payout state transitions for one commission across application instances.
    /// The lock is owned by the current database transaction.
    /// </summary>
    public interface IWeeklyCommissionPayoutMutationLock
    {
        Task AcquireEntryAsync(Guid commissionId);
        Task AcquireOnyxAsync(Guid commissionId);
    }
}
