using System;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.Payments
{
    /// <summary>
    /// Captures the customer's intended direct-Onyx placement while payment is
    /// pending. It is deliberately not a programme participation or network
    /// placement; those are created only after verified payment confirmation.
    /// </summary>
    public class DirectOnyxCheckoutIntent : HostedPaymentCheckout
    {
        public const int MaxInviteCodeLength = 32;

        public int? RecruiterCustomerId { get; private set; }
        public string InviteCode { get; private set; }
        public int OnyxMembershipId { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public Guid? ParticipationId { get; private set; }

        protected DirectOnyxCheckoutIntent()
        {
        }

        public static DirectOnyxCheckoutIntent Create(
            int tenantId,
            int customerId,
            int? recruiterCustomerId,
            string inviteCode,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime createdAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (recruiterCustomerId == customerId)
                throw new ArgumentException("A Club Member cannot invite themselves into their own network.", nameof(recruiterCustomerId));
            if (onyxMembershipId <= 0) throw new ArgumentOutOfRangeException(nameof(onyxMembershipId));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (createdAt == default) throw new ArgumentException("A creation time is required.", nameof(createdAt));

            var normalizedInviteCode = string.IsNullOrWhiteSpace(inviteCode)
                ? null
                : inviteCode.Trim().ToUpperInvariant();
            if (normalizedInviteCode?.Length > MaxInviteCodeLength)
                throw new ArgumentException($"Invitation code cannot exceed {MaxInviteCodeLength} characters.", nameof(inviteCode));

            var intent = new DirectOnyxCheckoutIntent
            {
                RecruiterCustomerId = recruiterCustomerId,
                InviteCode = normalizedInviteCode,
                OnyxMembershipId = onyxMembershipId,
                TermsVersion = terms.Version,
                TermsEffectiveFrom = terms.EffectiveFrom,
            };
            intent.Initialize(tenantId, customerId, terms.DirectEntryAmount, terms.Currency, createdAt);
            return intent;
        }

        public void Complete(Guid paymentId, Guid participationId, DateTime completedAt)
        {
            if (participationId == Guid.Empty) throw new ArgumentException("A participation is required.", nameof(participationId));
            if (PaymentId == paymentId)
            {
                if (ParticipationId == participationId)
                    return;
                throw new InvalidOperationException("The completed checkout cannot be reassigned to a different participation.");
            }
            CompletePayment(paymentId, completedAt);
            ParticipationId = participationId;
        }

        public void RecordProviderFailure(DateTime failedAt, string providerEvidence) =>
            Terminate(HostedPaymentCheckoutStatus.Failed, failedAt, providerEvidence);

        public void RecordProviderExpiry(DateTime expiredAt, string providerEvidence) =>
            Terminate(HostedPaymentCheckoutStatus.Expired, expiredAt, providerEvidence);

        public OnyxPlanTerms RestoreTerms() => OnyxPlanTerms.Create(
            TermsVersion,
            TermsEffectiveFrom,
            Amount,
            Currency);

    }
}
