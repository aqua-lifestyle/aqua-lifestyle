using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenApprovalAuthorityStabilizer
    {
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
