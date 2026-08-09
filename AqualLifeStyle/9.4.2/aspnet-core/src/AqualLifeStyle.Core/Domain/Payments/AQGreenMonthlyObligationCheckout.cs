using System;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.Payments
{
    public enum AQGreenMonthlyPaymentAllocationStatus
    {
        PendingProviderConfirmation = 0,
        Allocated = 1,
        ReconciliationRequired = 2
    }

    /// <summary>
    /// Records a provider checkout created for one exact AQGreen monthly obligation.
    /// </summary>
    public class AQGreenMonthlyObligationCheckout : HostedPaymentCheckout
    {
        public const int MaxAllocationEvidenceLength = 1000;

        public Guid EntryMonthlyObligationId { get; private set; }
        public Guid EntryParticipationId { get; private set; }
        public int PeriodYear { get; private set; }
        public int PeriodMonth { get; private set; }
        public AQGreenMonthlyPaymentAllocationStatus AllocationStatus { get; private set; }
        public string AllocationEvidence { get; private set; }

        protected AQGreenMonthlyObligationCheckout()
        {
        }

        public static AQGreenMonthlyObligationCheckout Create(
            EntryMonthlyObligation obligation,
            DateTime createdAt)
        {
            if (obligation == null) throw new ArgumentNullException(nameof(obligation));
            if (obligation.Status == EntryMonthlyObligationStatus.Paid ||
                obligation.PaymentId.HasValue ||
                obligation.OutstandingAmount != obligation.AmountDue)
                throw new InvalidOperationException(
                    "Only a fully unpaid AQGreen monthly obligation can receive a checkout.");

            var checkout = new AQGreenMonthlyObligationCheckout
            {
                EntryMonthlyObligationId = obligation.Id,
                EntryParticipationId = obligation.EntryParticipationId,
                PeriodYear = obligation.PeriodYear,
                PeriodMonth = obligation.PeriodMonth,
                AllocationStatus = AQGreenMonthlyPaymentAllocationStatus.PendingProviderConfirmation
            };
            checkout.Initialize(
                obligation.TenantId,
                obligation.CustomerId,
                obligation.OutstandingAmount,
                obligation.Currency,
                createdAt);
            return checkout;
        }

        public void CompleteAllocation(Guid paymentId, DateTime completedAt)
        {
            if (!CompletePayment(paymentId, completedAt))
            {
                if (AllocationStatus != AQGreenMonthlyPaymentAllocationStatus.Allocated)
                    throw new InvalidOperationException(
                        "This checkout was completed with a different allocation outcome.");
                return;
            }

            AllocationStatus = AQGreenMonthlyPaymentAllocationStatus.Allocated;
        }

        public void RequireReconciliation(
            Guid paymentId,
            DateTime completedAt,
            string evidence)
        {
            var normalizedEvidence = evidence?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEvidence) ||
                normalizedEvidence.Length > MaxAllocationEvidenceLength)
                throw new ArgumentException(
                    "Valid allocation reconciliation evidence is required.",
                    nameof(evidence));

            if (!CompletePayment(paymentId, completedAt))
            {
                if (AllocationStatus != AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired ||
                    !string.Equals(AllocationEvidence, normalizedEvidence, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "This checkout was completed with different reconciliation evidence.");
                return;
            }

            AllocationStatus = AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired;
            AllocationEvidence = normalizedEvidence;
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
