using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum EntryParticipationStatus
    {
        AwaitingRegistrationPayment = 0,
        AwaitingActivationPayment = 1,
        Active = 2
    }

    public class EntryParticipation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<EntryRecruiterCorrection> _recruiterCorrections = new();

        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public int RecruiterCustomerId { get; private set; }
        public EntryParticipationStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public Guid? RegistrationPaymentId { get; private set; }
        public Guid? ActivationPaymentId { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public decimal RegistrationPaymentAmount { get; private set; }
        public decimal ActivationPaymentAmount { get; private set; }
        public decimal MonthlyCommitmentAmount { get; private set; }
        public int GracePeriodDays { get; private set; }
        public string Currency { get; private set; }
        public IReadOnlyCollection<EntryRecruiterCorrection> RecruiterCorrections => _recruiterCorrections.AsReadOnly();
        public bool IsQualifiedForNetwork => Status == EntryParticipationStatus.Active;

        protected EntryParticipation()
        {
        }

        private EntryParticipation(
            int tenantId,
            int customerId,
            int recruiterCustomerId,
            EntryProgrammeTerms terms,
            DateTime startedAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            EnsureValidRecruiter(customerId, recruiterCustomerId);
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (startedAt == default) throw new ArgumentException("A start time is required.", nameof(startedAt));
            if (startedAt < terms.EffectiveFrom)
            {
                throw new ArgumentException("Participation cannot start before its terms are effective.", nameof(startedAt));
            }

            TenantId = tenantId;
            CustomerId = customerId;
            RecruiterCustomerId = recruiterCustomerId;
            StartedAt = startedAt;
            TermsVersion = terms.Version;
            TermsEffectiveFrom = terms.EffectiveFrom;
            RegistrationPaymentAmount = terms.RegistrationPaymentAmount;
            ActivationPaymentAmount = terms.ActivationPaymentAmount;
            MonthlyCommitmentAmount = terms.MonthlyCommitmentAmount;
            GracePeriodDays = terms.GracePeriodDays;
            Currency = terms.Currency;
            Status = EntryParticipationStatus.AwaitingRegistrationPayment;
        }

        public static EntryParticipation Start(
            int tenantId,
            int customerId,
            int recruiterCustomerId,
            EntryProgrammeTerms terms,
            DateTime startedAt)
        {
            return new EntryParticipation(tenantId, customerId, recruiterCustomerId, terms, startedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public void ApplyConfirmedActivationPayment(MemberPayment payment)
        {
            EnsurePaymentBelongsToParticipation(payment);

            switch (payment.Purpose)
            {
                case MemberPaymentPurpose.EntryRegistration:
                    ApplyRegistrationPayment(payment);
                    return;
                case MemberPaymentPurpose.EntryActivation:
                    ApplyCompletionPayment(payment);
                    return;
                default:
                    throw new InvalidOperationException("The confirmed payment is not an Entry activation payment.");
            }
        }

        public void CorrectRecruiter(
            int newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            EnsureValidRecruiter(CustomerId, newRecruiterCustomerId);
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A correction reason is required.", nameof(reason));
            }

            if (correctedAt == default || correctedAt < StartedAt)
            {
                throw new ArgumentException("A valid correction time is required.", nameof(correctedAt));
            }

            if (newRecruiterCustomerId == RecruiterCustomerId)
            {
                return;
            }

            _recruiterCorrections.Add(EntryRecruiterCorrection.Record(
                RecruiterCustomerId,
                newRecruiterCustomerId,
                administratorUserId,
                reason,
                correctedAt));
            RecruiterCustomerId = newRecruiterCustomerId;
        }

        private void ApplyRegistrationPayment(MemberPayment payment)
        {
            if (RegistrationPaymentId == payment.Id)
            {
                return;
            }

            if (RegistrationPaymentId.HasValue)
            {
                throw new InvalidOperationException("The Entry registration payment has already been recorded.");
            }

            EnsureExactAmount(payment, RegistrationPaymentAmount);
            RegistrationPaymentId = payment.Id;
            Status = EntryParticipationStatus.AwaitingActivationPayment;
        }

        private void ApplyCompletionPayment(MemberPayment payment)
        {
            if (ActivationPaymentId == payment.Id)
            {
                return;
            }

            if (!RegistrationPaymentId.HasValue)
            {
                throw new InvalidOperationException("The Entry registration payment must be confirmed first.");
            }

            if (ActivationPaymentId.HasValue)
            {
                throw new InvalidOperationException("The Entry activation payment has already been recorded.");
            }

            EnsureExactAmount(payment, ActivationPaymentAmount);
            ActivationPaymentId = payment.Id;
            ActivatedAt = payment.ConfirmedAt;
            Status = EntryParticipationStatus.Active;
        }

        private void EnsurePaymentBelongsToParticipation(MemberPayment payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (payment.Status != MemberPaymentStatus.Confirmed)
            {
                throw new InvalidOperationException("Only a confirmed payment can be applied.");
            }

            if (payment.TenantId != TenantId || payment.CustomerId != CustomerId)
            {
                throw new InvalidOperationException("The payment does not belong to this Entry participant.");
            }

            if (!string.Equals(payment.Currency, Currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The payment currency does not match the Entry terms.");
            }
        }

        private static void EnsureExactAmount(MemberPayment payment, decimal expectedAmount)
        {
            if (payment.Amount != expectedAmount)
            {
                throw new InvalidOperationException($"The payment amount must be {payment.Currency} {expectedAmount:0.00}.");
            }
        }

        private static void EnsureValidRecruiter(int customerId, int recruiterCustomerId)
        {
            if (recruiterCustomerId <= 0) throw new ArgumentOutOfRangeException(nameof(recruiterCustomerId));
            if (customerId == recruiterCustomerId)
            {
                throw new InvalidOperationException("A customer cannot recruit themselves.");
            }
        }
    }

    public class EntryRecruiterCorrection : Entity<Guid>
    {
        public int PreviousRecruiterCustomerId { get; private set; }
        public int NewRecruiterCustomerId { get; private set; }
        public long AdministratorUserId { get; private set; }
        public string Reason { get; private set; }
        public DateTime CorrectedAt { get; private set; }

        protected EntryRecruiterCorrection()
        {
        }

        private EntryRecruiterCorrection(
            int previousRecruiterCustomerId,
            int newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            Id = Guid.NewGuid();
            PreviousRecruiterCustomerId = previousRecruiterCustomerId;
            NewRecruiterCustomerId = newRecruiterCustomerId;
            AdministratorUserId = administratorUserId;
            Reason = reason.Trim();
            CorrectedAt = correctedAt;
        }

        internal static EntryRecruiterCorrection Record(
            int previousRecruiterCustomerId,
            int newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            return new EntryRecruiterCorrection(
                previousRecruiterCustomerId,
                newRecruiterCustomerId,
                administratorUserId,
                reason,
                correctedAt);
        }
    }
}
