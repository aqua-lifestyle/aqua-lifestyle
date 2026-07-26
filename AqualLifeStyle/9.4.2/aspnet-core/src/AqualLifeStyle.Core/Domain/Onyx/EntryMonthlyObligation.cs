using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum EntryMonthlyObligationStatus
    {
        Due = 0,
        GracePeriod = 1,
        Overdue = 2,
        Paid = 3
    }

    public class EntryMonthlyObligation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public Guid EntryParticipationId { get; private set; }
        public int CustomerId { get; private set; }
        public int PeriodYear { get; private set; }
        public int PeriodMonth { get; private set; }
        public decimal AmountDue { get; private set; }
        public decimal OutstandingAmount { get; private set; }
        public string Currency { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime DueAt { get; private set; }
        public int GracePeriodDays { get; private set; }
        public DateTime GracePeriodEndsAt { get; private set; }
        public EntryMonthlyObligationStatus Status { get; private set; }
        public DateTime? LastAssessedAt { get; private set; }
        public DateTime? MarkedOverdueAt { get; private set; }
        public Guid? PaymentId { get; private set; }
        public DateTime? PaidAt { get; private set; }
        public bool IsOwnPayoutEligible => Status != EntryMonthlyObligationStatus.Overdue;

        protected EntryMonthlyObligation()
        {
        }

        private EntryMonthlyObligation(
            EntryParticipation participation,
            int periodYear,
            int periodMonth,
            DateTime dueAt)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (!participation.IsQualifiedForNetwork)
            {
                throw new InvalidOperationException(
                    "Monthly obligations can only be created for active AQGreen participants.");
            }

            if (periodYear < 2000 || periodYear > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(periodYear));
            }

            if (periodMonth < 1 || periodMonth > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(periodMonth));
            }

            if (dueAt == default)
            {
                throw new ArgumentException("A payment due time is required.", nameof(dueAt));
            }

            Id = Guid.NewGuid();
            TenantId = participation.TenantId;
            EntryParticipationId = participation.Id;
            CustomerId = participation.CustomerId;
            PeriodYear = periodYear;
            PeriodMonth = periodMonth;
            AmountDue = participation.MonthlyCommitmentAmount;
            OutstandingAmount = AmountDue;
            Currency = participation.Currency;
            TermsVersion = participation.TermsVersion;
            DueAt = dueAt;
            GracePeriodDays = participation.GracePeriodDays;
            GracePeriodEndsAt = dueAt.AddDays(GracePeriodDays);
            Status = EntryMonthlyObligationStatus.Due;
        }

        public static EntryMonthlyObligation Create(
            EntryParticipation participation,
            int periodYear,
            int periodMonth,
            DateTime dueAt)
        {
            return new EntryMonthlyObligation(
                participation,
                periodYear,
                periodMonth,
                dueAt);
        }

        public void AssessStatus(DateTime asOf)
        {
            if (asOf == default)
            {
                throw new ArgumentException("An assessment time is required.", nameof(asOf));
            }

            if (Status == EntryMonthlyObligationStatus.Paid)
            {
                return;
            }

            if (LastAssessedAt.HasValue && asOf < LastAssessedAt.Value)
            {
                throw new InvalidOperationException(
                    "An obligation cannot be reassessed at an earlier time.");
            }

            LastAssessedAt = asOf;
            if (asOf <= DueAt)
            {
                Status = EntryMonthlyObligationStatus.Due;
                return;
            }

            if (asOf <= GracePeriodEndsAt)
            {
                Status = EntryMonthlyObligationStatus.GracePeriod;
                return;
            }

            Status = EntryMonthlyObligationStatus.Overdue;
            MarkedOverdueAt ??= asOf;
        }

        public void ApplyConfirmedPayment(MemberPayment payment)
        {
            if (payment == null)
            {
                throw new ArgumentNullException(nameof(payment));
            }

            if (PaymentId == payment.Id)
            {
                return;
            }

            if (Status == EntryMonthlyObligationStatus.Paid || PaymentId.HasValue)
            {
                throw new InvalidOperationException(
                    "This AQGreen monthly obligation has already been paid.");
            }

            if (payment.Status != MemberPaymentStatus.Confirmed)
            {
                throw new InvalidOperationException(
                    "Only a confirmed payment can settle a monthly obligation.");
            }

            if (payment.TenantId != TenantId || payment.CustomerId != CustomerId)
            {
                throw new InvalidOperationException(
                    "The payment does not belong to this AQGreen participant.");
            }

            if (payment.Purpose != MemberPaymentPurpose.EntryMonthlyCommitment)
            {
                throw new InvalidOperationException(
                    "The payment is not an AQGreen monthly commitment payment.");
            }

            if (!string.Equals(payment.Currency, Currency, StringComparison.Ordinal) ||
                payment.Amount != AmountDue)
            {
                throw new InvalidOperationException(
                    $"The payment amount must be {Currency} {AmountDue:0.00}.");
            }

            PaymentId = payment.Id;
            PaidAt = payment.ConfirmedAt;
            OutstandingAmount = 0m;
            Status = EntryMonthlyObligationStatus.Paid;
        }
    }
}
