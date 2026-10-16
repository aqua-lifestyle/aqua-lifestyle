using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenApprovalAuthorityStabilizer
    {
        /// <summary>
        /// Locks a customer row without assuming that it is already a member or
        /// has active Area evidence. Re-read and authorize its current state after
        /// acquisition, within the same caller-owned transaction.
        /// </summary>
        Task LockCustomerAsync(int tenantId, int customerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Locks a strictly soft-deleted customer's original Area and matching
        /// administrator grant for restoration. Does not restore the record or
        /// authorize the caller. Live approval stabilization still rejects it.
        /// </summary>
        Task<Guid> StabilizeRemovedCustomerAsync(int tenantId, int customerId,
            long areaAdministratorUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Locks and returns the participant customer's current Area. When an
        /// Area administrator is supplied, the matching current assignment is
        /// locked as well. The caller must re-read and authorize after this call.
        /// </summary>
        Task<Guid> StabilizeAsync(
            int tenantId,
            int customerId,
            long? areaAdministratorUserId,
            CancellationToken cancellationToken = default);
    }

}
