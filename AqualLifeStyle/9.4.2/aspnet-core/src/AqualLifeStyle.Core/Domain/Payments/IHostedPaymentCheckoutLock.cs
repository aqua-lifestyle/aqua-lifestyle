using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Payments
{
    /// <summary>
    /// Serialises checkout lifecycle decisions across application instances.
    /// The lock is owned by the current database transaction.
    /// </summary>
    public interface IHostedPaymentCheckoutLock
    {
        Task AcquireCheckoutAsync(Guid checkoutId);
        Task AcquireAQGreenParticipationAsync(Guid participationId);
        Task AcquireDirectOnyxCustomerAsync(int customerId);
        Task AcquireCustomerAreaTransitionsAsync(params int[] customerIds);
        Task AcquireProgrammeApprovalUserSessionAsync(long userId);
        Task ReleaseProgrammeApprovalUserSessionAsync(long userId);
        Task AcquireProgrammeParticipationDecisionAsync(Guid participationId);
    }
}
