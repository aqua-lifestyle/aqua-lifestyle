using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum AQGreenJoiningPaymentSchedule
    {
        Full = 0,
        TwoInstallments = 1
    }

    public enum AQGreenJoiningPaymentStage
    {
        Full = 0,
        FirstInstallment = 1,
        SecondInstallment = 2
    }

    public enum EntryParticipationStatus
    {
        AwaitingJoiningPayment = 0,
        AwaitingRegistrationPayment = AwaitingJoiningPayment,
        AwaitingActivationPayment = 1,
        Active = 2,
        PaymentConfirmedAwaitingApproval = 3,
        Rejected = 4
    }

    public class EntryParticipation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<EntryRecruiterCorrection> _recruiterCorrections = new();
        private readonly List<EntryParticipationApprovalDecision> _approvalDecisions = new();

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
        public decimal JoiningInstallmentAmount { get; private set; }
        public AQGreenJoiningPaymentSchedule? JoiningPaymentSchedule { get; private set; }
        public decimal RegistrationPaymentAmount { get; private set; }
        public decimal ActivationPaymentAmount { get; private set; }
        public decimal MonthlyCommitmentAmount { get; private set; }
        public int GracePeriodDays { get; private set; }
        public string Currency { get; private set; }
        public IReadOnlyCollection<EntryRecruiterCorrection> RecruiterCorrections => _recruiterCorrections.AsReadOnly();
        public IReadOnlyCollection<EntryParticipationApprovalDecision> ApprovalDecisions =>
            _approvalDecisions.AsReadOnly();
        public bool IsQualifiedForNetwork => Status == EntryParticipationStatus.Active;

        /// <summary>
        /// True when the modern AQGreen joining obligation has been fully paid
        /// (the R1,200 once or two R600 instalments). Historical participations
        /// that use the split registration/activation lifecycle are excluded.
        /// </summary>
        public bool IsJoiningObligationSatisfied =>
            JoiningPaymentAmount > 0m && GetOutstandingJoiningAmount() == 0m;
        public bool IsAwaitingAdministrativeApproval =>
            Status == EntryParticipationStatus.PaymentConfirmedAwaitingApproval;
        public bool IsRejected => Status == EntryParticipationStatus.Rejected;

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
            JoiningInstallmentAmount = terms.JoiningInstallmentAmount;
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
            EnsureEligibleRecruiter(tenantId, customerId, recruiterParticipation);
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
            if (IsJoiningComplete || JoiningPaymentId.HasValue)
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
            Status = EntryParticipationStatus.PaymentConfirmedAwaitingApproval;
        }

        public void SelectJoiningPaymentSchedule(AQGreenJoiningPaymentSchedule schedule)
        {
            if (IsJoiningComplete)
                throw new InvalidOperationException("AQGreen joining is already complete.");
            if (schedule != AQGreenJoiningPaymentSchedule.Full &&
                schedule != AQGreenJoiningPaymentSchedule.TwoInstallments)
                throw new ArgumentOutOfRangeException(
                    nameof(schedule),
                    schedule,
                    "The AQGreen joining payment schedule is unsupported.");
            if (JoiningPaymentAmount <= 0m)
                throw new InvalidOperationException("This AQGreen record does not support selectable joining schedules.");
            if (schedule == AQGreenJoiningPaymentSchedule.TwoInstallments &&
                JoiningInstallmentAmount <= 0m)
                throw new InvalidOperationException(
                    "This AQGreen record does not support joining instalments.");
            if (JoiningPaymentId.HasValue || RegistrationPaymentId.HasValue || ActivationPaymentId.HasValue)
            {
                if (JoiningPaymentSchedule == schedule) return;
                throw new InvalidOperationException(
                    "The AQGreen joining schedule cannot change after a verified payment.");
            }

            JoiningPaymentSchedule = schedule;
            Status = EntryParticipationStatus.AwaitingJoiningPayment;
        }

        public decimal GetConfirmedJoiningAmount()
        {
            if (JoiningPaymentId.HasValue) return JoiningPaymentAmount;
            var confirmed = 0m;
            if (RegistrationPaymentId.HasValue) confirmed += JoiningInstallmentAmount;
            if (ActivationPaymentId.HasValue) confirmed += JoiningInstallmentAmount;
            return confirmed;
        }

        public decimal GetOutstandingJoiningAmount() =>
            Math.Max(0m, JoiningPaymentAmount - GetConfirmedJoiningAmount());

        public AQGreenJoiningPaymentStage GetNextJoiningPaymentStage()
        {
            if (!JoiningPaymentSchedule.HasValue)
                throw new InvalidOperationException("Select an AQGreen joining schedule first.");
            if (IsJoiningComplete)
                throw new InvalidOperationException("AQGreen joining is already complete.");
            if (JoiningPaymentSchedule == AQGreenJoiningPaymentSchedule.Full)
                return AQGreenJoiningPaymentStage.Full;
            return RegistrationPaymentId.HasValue
                ? AQGreenJoiningPaymentStage.SecondInstallment
                : AQGreenJoiningPaymentStage.FirstInstallment;
        }

        public decimal GetNextJoiningPaymentAmount() =>
            GetNextJoiningPaymentStage() == AQGreenJoiningPaymentStage.Full
                ? JoiningPaymentAmount
                : JoiningInstallmentAmount;

        public void ApplyConfirmedJoiningPayment(
            MemberPayment payment,
            AQGreenJoiningPaymentStage stage)
        {
            EnsurePaymentBelongsToParticipation(payment);
            if (payment.Purpose != MemberPaymentPurpose.AQGreenJoining)
                throw new InvalidOperationException("The payment is not an AQGreen joining payment.");
            if (JoiningPaymentId == payment.Id ||
                RegistrationPaymentId == payment.Id ||
                ActivationPaymentId == payment.Id)
                return;
            if (!JoiningPaymentSchedule.HasValue)
                throw new InvalidOperationException("The AQGreen joining schedule is missing.");

            var expectedStage = GetNextJoiningPaymentStage();
            if (stage != expectedStage)
                throw new InvalidOperationException("The payment does not match the next AQGreen joining instalment.");

            if (stage == AQGreenJoiningPaymentStage.Full)
            {
                EnsureExactAmount(payment, JoiningPaymentAmount);
                JoiningPaymentId = payment.Id;
                Status = EntryParticipationStatus.PaymentConfirmedAwaitingApproval;
                return;
            }

            EnsureExactAmount(payment, JoiningInstallmentAmount);
            if (stage == AQGreenJoiningPaymentStage.FirstInstallment)
            {
                RegistrationPaymentId = payment.Id;
                Status = EntryParticipationStatus.AwaitingActivationPayment;
                return;
            }

            if (!RegistrationPaymentId.HasValue || payment.Id == RegistrationPaymentId.Value)
                throw new InvalidOperationException("A distinct first AQGreen joining instalment is required.");
            ActivationPaymentId = payment.Id;
            if (GetConfirmedJoiningAmount() != JoiningPaymentAmount)
                throw new InvalidOperationException("The AQGreen joining total is incomplete.");
            Status = EntryParticipationStatus.PaymentConfirmedAwaitingApproval;
        }

        public void ApproveByAdministrator(long administratorUserId, DateTime decidedAt)
        {
            EnsureAwaitingAdministrativeApproval();
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (decidedAt == default) throw new ArgumentException("A decision time is required.", nameof(decidedAt));
            if (decidedAt < StartedAt)
                throw new ArgumentException("Approval cannot precede the participation start.", nameof(decidedAt));

            _approvalDecisions.Add(EntryParticipationApprovalDecision.Approve(administratorUserId, decidedAt));
            ActivatedAt = decidedAt;
            Status = EntryParticipationStatus.Active;
        }

        public void RejectByAdministrator(long administratorUserId, string reason, DateTime decidedAt)
        {
            EnsureAwaitingAdministrativeApproval();
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (decidedAt == default) throw new ArgumentException("A decision time is required.", nameof(decidedAt));

            _approvalDecisions.Add(
                EntryParticipationApprovalDecision.Reject(administratorUserId, reason, decidedAt));
            Status = EntryParticipationStatus.Rejected;
        }

        private void EnsureAwaitingAdministrativeApproval()
        {
            if (!IsAwaitingAdministrativeApproval)
            {
                throw new InvalidOperationException(
                    "The participation is not awaiting administrative approval.");
            }
        }

        private bool IsJoiningComplete =>
            Status is EntryParticipationStatus.Active or
                EntryParticipationStatus.PaymentConfirmedAwaitingApproval or
                EntryParticipationStatus.Rejected;

        public void CorrectRecruiter(
            EntryParticipation newRecruiterParticipation,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            EnsureEligibleRecruiter(TenantId, CustomerId, newRecruiterParticipation);
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

            if (_recruiterCorrections.Count > 0 &&
                correctedAt <= _recruiterCorrections.Max(item => item.CorrectedAt))
            {
                throw new InvalidOperationException(
                    "Recruiter corrections must be recorded in strictly increasing effective-time order.");
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
            Status = EntryParticipationStatus.PaymentConfirmedAwaitingApproval;
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
            int tenantId,
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
                    "The inviting Club Member must have active AQGreen participation.");
            }

            if (recruiterParticipation.TenantId != tenantId)
            {
                throw new InvalidOperationException(
                    "The inviting Club Member must belong to the same Tenant.");
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
                throw new InvalidOperationException("A Club Member cannot invite themselves into their own network.");
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

    public class EntryParticipationApprovalDecision : Entity<Guid>
    {
        public const int MaxRejectionReasonLength = 1000;

        public long AdministratorUserId { get; private set; }
        public bool Approved { get; private set; }
        public string Reason { get; private set; }
        public DateTime DecidedAt { get; private set; }

        protected EntryParticipationApprovalDecision()
        {
        }

        private EntryParticipationApprovalDecision(
            long administratorUserId,
            bool approved,
            string reason,
            DateTime decidedAt)
        {
            AdministratorUserId = administratorUserId;
            Approved = approved;
            Reason = reason;
            DecidedAt = decidedAt;
        }

        internal static EntryParticipationApprovalDecision Approve(
            long administratorUserId,
            DateTime decidedAt)
        {
            return new EntryParticipationApprovalDecision(administratorUserId, true, null, decidedAt);
        }

        internal static EntryParticipationApprovalDecision Reject(
            long administratorUserId,
            string reason,
            DateTime decidedAt)
        {
            var normalizedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReason))
                throw new ArgumentException("A rejection reason is required.", nameof(reason));
            if (normalizedReason.Length > MaxRejectionReasonLength)
                throw new ArgumentException(
                    $"The rejection reason cannot exceed {MaxRejectionReasonLength} characters.",
                    nameof(reason));

            return new EntryParticipationApprovalDecision(
                administratorUserId,
                false,
                normalizedReason,
                decidedAt);
        }
    }
}
