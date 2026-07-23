using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum OnyxAdmissionRoute
    {
        DirectPayment = 0,
        EntryGraduation = 1
    }

    public enum OnyxParticipationStatus
    {
        AwaitingDirectEntryPayment = 0,
        Active = 1
    }

    public class OnyxParticipation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public int OnyxMembershipId { get; private set; }
        public OnyxAdmissionRoute AdmissionRoute { get; private set; }
        public OnyxParticipationStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public Guid? DirectEntryPaymentId { get; private set; }
        public Guid? EntryParticipationId { get; private set; }
        public Guid? FundingAgreementId { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public decimal DirectEntryAmount { get; private set; }
        public string Currency { get; private set; }

        protected OnyxParticipation()
        {
        }

        private OnyxParticipation(
            int tenantId,
            int customerId,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (onyxMembershipId <= 0) throw new ArgumentOutOfRangeException(nameof(onyxMembershipId));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (startedAt == default) throw new ArgumentException("A start time is required.", nameof(startedAt));
            if (startedAt < terms.EffectiveFrom)
            {
                throw new ArgumentException("Participation cannot start before its terms are effective.", nameof(startedAt));
            }

            TenantId = tenantId;
            CustomerId = customerId;
            OnyxMembershipId = onyxMembershipId;
            AdmissionRoute = OnyxAdmissionRoute.DirectPayment;
            Status = OnyxParticipationStatus.AwaitingDirectEntryPayment;
            StartedAt = startedAt;
            TermsVersion = terms.Version;
            TermsEffectiveFrom = terms.EffectiveFrom;
            DirectEntryAmount = terms.DirectEntryAmount;
            Currency = terms.Currency;
        }

        public static OnyxParticipation StartDirect(
            int tenantId,
            int customerId,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            return new OnyxParticipation(tenantId, customerId, onyxMembershipId, terms, startedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public void ApplyConfirmedDirectEntryPayment(MemberPayment payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (DirectEntryPaymentId == payment.Id)
            {
                return;
            }

            if (Status == OnyxParticipationStatus.Active || DirectEntryPaymentId.HasValue)
            {
                throw new InvalidOperationException("The direct Onyx entry payment has already been recorded.");
            }

            if (payment.Status != MemberPaymentStatus.Confirmed)
            {
                throw new InvalidOperationException("Only a confirmed payment can activate direct Onyx participation.");
            }

            if (payment.TenantId != TenantId || payment.CustomerId != CustomerId)
            {
                throw new InvalidOperationException("The payment does not belong to this Onyx participant.");
            }

            if (payment.Purpose != MemberPaymentPurpose.OnyxDirectEntry)
            {
                throw new InvalidOperationException("The payment is not a direct Onyx entry payment.");
            }

            if (!string.Equals(payment.Currency, Currency, StringComparison.Ordinal) ||
                payment.Amount != DirectEntryAmount)
            {
                throw new InvalidOperationException($"The payment amount must be {Currency} {DirectEntryAmount:0.00}.");
            }

            DirectEntryPaymentId = payment.Id;
            ActivatedAt = payment.ConfirmedAt;
            Status = OnyxParticipationStatus.Active;
        }
    }
}
