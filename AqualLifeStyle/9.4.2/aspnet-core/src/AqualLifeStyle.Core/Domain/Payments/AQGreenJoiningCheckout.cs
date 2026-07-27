using System;

namespace AqualLifeStyle.Domain.Payments
{
    /// <summary>
    /// Records the stable provider checkout for one AQGreen participation's
    /// single joining payment. It never activates the participation itself.
    /// </summary>
    public class AQGreenJoiningCheckout : HostedPaymentCheckout
    {
        public Guid ParticipationId { get; private set; }

        protected AQGreenJoiningCheckout()
        {
        }

        public static AQGreenJoiningCheckout Create(
            int tenantId,
            Guid participationId,
            int customerId,
            decimal amount,
            string currency,
            DateTime createdAt)
        {
            if (participationId == Guid.Empty) throw new ArgumentException("A participation is required.", nameof(participationId));
            var checkout = new AQGreenJoiningCheckout
            {
                ParticipationId = participationId
            };
            checkout.Initialize(tenantId, customerId, amount, currency, createdAt);
            return checkout;
        }

        public void Complete(Guid paymentId, DateTime completedAt)
        {
            CompletePayment(paymentId, completedAt);
        }
    }
}
