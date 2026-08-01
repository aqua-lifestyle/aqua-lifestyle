using System;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.Payments
{
    /// <summary>
    /// Records the stable provider checkout for one AQGreen participation's
    /// single joining payment. It never activates the participation itself.
    /// </summary>
    public class AQGreenJoiningCheckout : HostedPaymentCheckout
    {
        public Guid ParticipationId { get; private set; }
        public AQGreenJoiningPaymentSchedule Schedule { get; private set; }
        public AQGreenJoiningPaymentStage Stage { get; private set; }

        protected AQGreenJoiningCheckout()
        {
        }

        public static AQGreenJoiningCheckout Create(
            int tenantId,
            Guid participationId,
            int customerId,
            AQGreenJoiningPaymentSchedule schedule,
            AQGreenJoiningPaymentStage stage,
            decimal amount,
            string currency,
            DateTime createdAt)
        {
            if (participationId == Guid.Empty) throw new ArgumentException("A participation is required.", nameof(participationId));
            var checkout = new AQGreenJoiningCheckout
            {
                ParticipationId = participationId,
                Schedule = schedule,
                Stage = stage
            };
            checkout.Initialize(tenantId, customerId, amount, currency, createdAt);
            return checkout;
        }

        public void Complete(Guid paymentId, DateTime completedAt)
        {
            CompletePayment(paymentId, completedAt);
        }

        public void RecordProviderFailure(DateTime failedAt, string providerEvidence) =>
            Terminate(HostedPaymentCheckoutStatus.Failed, failedAt, providerEvidence);

        public void RecordProviderExpiry(DateTime expiredAt, string providerEvidence) =>
            Terminate(HostedPaymentCheckoutStatus.Expired, expiredAt, providerEvidence);

        public void TerminateByAdministrator(
            long administratorUserId,
            DateTime terminatedAt,
            string evidence) =>
            Terminate(
                HostedPaymentCheckoutStatus.AdministrativelyTerminated,
                terminatedAt,
                evidence,
                administratorUserId);
    }
}
