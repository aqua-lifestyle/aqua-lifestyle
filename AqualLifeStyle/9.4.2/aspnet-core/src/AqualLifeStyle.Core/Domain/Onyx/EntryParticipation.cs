using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum EntryParticipationStatus
    {
        AwaitingJoiningPayment = 0,
        AwaitingRegistrationPayment = AwaitingJoiningPayment,
        AwaitingActivationPayment = 1,
        Active = 2
    }

    public class EntryParticipation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<EntryRecruiterCorrection> _recruiterCorrections = new();

        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public int? RecruiterCustomerId { get; private set; }
        public bool JoinedIndependently => !RecruiterCustomerId.HasValue;
        public EntryParticipationStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public Guid? JoiningPaymentId { get; private set; }
        public Guid? RegistrationPaymentId { get; private set; }
        public Guid? ActivationPaymentId { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public decimal JoiningPaymentAmount { get; private set; }
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
            int? recruiterCustomerId,
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
            JoiningPaymentAmount = terms.JoiningPaymentAmount;
            RegistrationPaymentAmount = terms.RegistrationPaymentAmount;
            ActivationPaymentAmount = terms.ActivationPaymentAmount;
            MonthlyCommitmentAmount = terms.MonthlyCommitmentAmount;
            GracePeriodDays = terms.GracePeriodDays;
            Currency = terms.Currency;
            Status = EntryParticipationStatus.AwaitingJoiningPayment;
        }

        public static EntryParticipation StartIndependently(
            int tenantId,
            int customerId,
            EntryProgrammeTerms terms,
            DateTime startedAt)
        {
            return new EntryParticipation(tenantId, customerId, null, terms, startedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public static EntryParticipation StartUnderRecruiter(
            int tenantId,
            int customerId,
            EntryParticipation recruiterParticipation,
            EntryProgrammeTerms terms,
            DateTime startedAt)
        {
            EnsureEligibleRecruiter(customerId, recruiterParticipation);
            return new EntryParticipation(
                tenantId,
                customerId,
                recruiterParticipation.CustomerId,
                terms,
                startedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public void ApplyConfirmedActivationPayment(MemberPayment payment)
        {
            if (JoiningPaymentAmount > 0m)
            {
                throw new InvalidOperationException(
                    "This AQGreen participation uses the single joining payment lifecycle.");
            }
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
                    throw new InvalidOperationException("The confirmed payment is not an AQGreen activation payment.");
            }
        }

        public void ApplyConfirmedJoiningPayment(MemberPayment payment)
        {
            if (JoiningPaymentAmount <= 0m)
            {
                throw new InvalidOperationException(
                    "This historical AQGreen participation uses the previous split-payment lifecycle.");
            }

            EnsurePaymentBelongsToParticipation(payment);
            if (payment.Purpose != MemberPaymentPurpose.AQGreenJoining)
            {
                throw new InvalidOperationException(
                    "The confirmed payment is not an AQGreen joining payment.");
            }
            if (JoiningPaymentId == payment.Id)
            {
                return;
            }
            if (Status == EntryParticipationStatus.Active || JoiningPaymentId.HasValue)
            {
                throw new InvalidOperationException(
                    "The AQGreen joining payment has already been recorded.");
            }
            if (RegistrationPaymentId.HasValue || ActivationPaymentId.HasValue)
            {
                throw new InvalidOperationException(
                    "A historical AQGreen payment already exists for this participation.");
            }

            EnsureExactAmount(payment, JoiningPaymentAmount);
            JoiningPaymentId = payment.Id;
            ActivatedAt = payment.ConfirmedAt;
            Status = EntryParticipationStatus.Active;
        }

        public void CorrectRecruiter(
            EntryParticipation newRecruiterParticipation,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            EnsureEligibleRecruiter(CustomerId, newRecruiterParticipation);
            RecordRecruiterCorrection(
                newRecruiterParticipation.CustomerId,
                administratorUserId,
                reason,
                correctedAt);
        }

        public void CorrectToIndependent(
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            RecordRecruiterCorrection(null, administratorUserId, reason, correctedAt);
        }

        private void RecordRecruiterCorrection(
            int? newRecruiterCustomerId,
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
                throw new InvalidOperationException("The AQGreen registration payment has already been recorded.");
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
                throw new InvalidOperationException("The AQGreen registration payment must be confirmed first.");
            }

            if (ActivationPaymentId.HasValue)
            {
                throw new InvalidOperationException("The AQGreen activation payment has already been recorded.");
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
                throw new InvalidOperationException("The payment does not belong to this AQGreen participant.");
            }

            if (!string.Equals(payment.Currency, Currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The payment currency does not match the AQGreen terms.");
            }
        }

        private static void EnsureExactAmount(MemberPayment payment, decimal expectedAmount)
        {
            if (payment.Amount != expectedAmount)
            {
                throw new InvalidOperationException($"The payment amount must be {payment.Currency} {expectedAmount:0.00}.");
            }
        }

        private static void EnsureEligibleRecruiter(
            int customerId,
            EntryParticipation recruiterParticipation)
        {
            if (recruiterParticipation == null)
            {
                throw new ArgumentNullException(nameof(recruiterParticipation));
            }

            if (!recruiterParticipation.IsQualifiedForNetwork)
            {
                throw new InvalidOperationException(
                    "The recruiting customer must have active AQGreen participation.");
            }

            EnsureValidRecruiter(customerId, recruiterParticipation.CustomerId);
        }

        private static void EnsureValidRecruiter(int customerId, int? recruiterCustomerId)
        {
            if (recruiterCustomerId.HasValue && recruiterCustomerId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recruiterCustomerId));
            }

            if (customerId == recruiterCustomerId)
            {
                throw new InvalidOperationException("A customer cannot recruit themselves.");
            }
        }
    }

    public class EntryRecruiterCorrection : Entity<Guid>
    {
        public int? PreviousRecruiterCustomerId { get; private set; }
        public int? NewRecruiterCustomerId { get; private set; }
        public long AdministratorUserId { get; private set; }
        public string Reason { get; private set; }
        public DateTime CorrectedAt { get; private set; }

        protected EntryRecruiterCorrection()
        {
        }

        private EntryRecruiterCorrection(
            int? previousRecruiterCustomerId,
            int? newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            PreviousRecruiterCustomerId = previousRecruiterCustomerId;
            NewRecruiterCustomerId = newRecruiterCustomerId;
            AdministratorUserId = administratorUserId;
            Reason = reason.Trim();
            CorrectedAt = correctedAt;
        }

        internal static EntryRecruiterCorrection Record(
            int? previousRecruiterCustomerId,
            int? newRecruiterCustomerId,
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
