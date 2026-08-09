using System;
using System.Collections.Generic;
using System.Linq;
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
        Active = 1,
        PaymentConfirmedAwaitingApproval = 2,
        Rejected = 3
    }

    public class OnyxParticipation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<OnyxRecruiterCorrection> _recruiterCorrections = new();
        private readonly List<OnyxParticipationApprovalDecision> _approvalDecisions = new();

        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public int? RecruiterCustomerId { get; private set; }
        public bool JoinedIndependently => !RecruiterCustomerId.HasValue;
        public int OnyxMembershipId { get; private set; }
        public OnyxAdmissionRoute AdmissionRoute { get; private set; }
        public OnyxParticipationStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public Guid? DirectEntryPaymentId { get; private set; }
        public Guid? EntryParticipationId { get; private set; }
        public Guid? LoanAgreementId { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public decimal DirectEntryAmount { get; private set; }
        public string Currency { get; private set; }
        public IReadOnlyCollection<OnyxRecruiterCorrection> RecruiterCorrections =>
            _recruiterCorrections.AsReadOnly();
        public IReadOnlyCollection<OnyxParticipationApprovalDecision> ApprovalDecisions =>
            _approvalDecisions.AsReadOnly();
        public bool IsAwaitingAdministrativeApproval =>
            Status == OnyxParticipationStatus.PaymentConfirmedAwaitingApproval;
        public bool IsRejected => Status == OnyxParticipationStatus.Rejected;

        protected OnyxParticipation()
        {
        }

        private OnyxParticipation(
            int tenantId,
            int customerId,
            int? recruiterCustomerId,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            EnsureValidRecruiter(customerId, recruiterCustomerId);
            if (onyxMembershipId <= 0) throw new ArgumentOutOfRangeException(nameof(onyxMembershipId));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (startedAt == default) throw new ArgumentException("A start time is required.", nameof(startedAt));
            if (startedAt < terms.EffectiveFrom)
            {
                throw new ArgumentException("Participation cannot start before its terms are effective.", nameof(startedAt));
            }

            TenantId = tenantId;
            CustomerId = customerId;
            RecruiterCustomerId = recruiterCustomerId;
            OnyxMembershipId = onyxMembershipId;
            AdmissionRoute = OnyxAdmissionRoute.DirectPayment;
            Status = OnyxParticipationStatus.AwaitingDirectEntryPayment;
            StartedAt = startedAt;
            TermsVersion = terms.Version;
            TermsEffectiveFrom = terms.EffectiveFrom;
            DirectEntryAmount = terms.DirectEntryAmount;
            Currency = terms.Currency;
        }

        public static OnyxParticipation StartDirectIndependently(
            int tenantId,
            int customerId,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            return new OnyxParticipation(tenantId, customerId, null, onyxMembershipId, terms, startedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public static OnyxParticipation StartDirectUnderRecruiter(
            int tenantId,
            int customerId,
            OnyxParticipation recruiterParticipation,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            if (recruiterParticipation == null)
            {
                throw new ArgumentNullException(nameof(recruiterParticipation));
            }

            if (recruiterParticipation.Status != OnyxParticipationStatus.Active)
            {
                throw new InvalidOperationException(
                    "The inviting Club Member must have active Onyx participation.");
            }

            EnsureValidRecruiter(customerId, recruiterParticipation.CustomerId);
            return new OnyxParticipation(
                tenantId,
                customerId,
                recruiterParticipation.CustomerId,
                onyxMembershipId,
                terms,
                startedAt)
            {
                Id = Guid.NewGuid()
            };
        }

        public static OnyxParticipation GraduateFromAQGreenIndependently(
            EntryParticipation aqGreenParticipation,
            OnyxLoanAgreement loanAgreement,
            int onyxMembershipId,
            OnyxPlanTerms terms,
            DateTime graduatedAt)
        {
            if (aqGreenParticipation == null)
            {
                throw new ArgumentNullException(nameof(aqGreenParticipation));
            }

            if (loanAgreement == null)
            {
                throw new ArgumentNullException(nameof(loanAgreement));
            }

            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            if (aqGreenParticipation.Status != EntryParticipationStatus.Active)
            {
                throw new InvalidOperationException(
                    "Only an active AQGreen participant can graduate to Onyx.");
            }

            if (loanAgreement.Status != OnyxLoanAgreementStatus.Active ||
                !loanAgreement.EffectiveAt.HasValue ||
                !loanAgreement.MemberAcceptedAt.HasValue ||
                !loanAgreement.MemberAcceptedByUserId.HasValue ||
                !loanAgreement.ApprovedAt.HasValue ||
                !loanAgreement.ApprovedByAdministratorUserId.HasValue)
            {
                throw new InvalidOperationException(
                    "The Onyx loan agreement must be active, accepted, and administrator-approved before AQGreen graduation.");
            }

            if (loanAgreement.TenantId != aqGreenParticipation.TenantId ||
                loanAgreement.CustomerId != aqGreenParticipation.CustomerId ||
                loanAgreement.EntryParticipationId != aqGreenParticipation.Id)
            {
                throw new InvalidOperationException(
                    "The loan agreement does not belong to this AQGreen participation.");
            }

            if (loanAgreement.PrincipalAmount != terms.DirectEntryAmount ||
                !string.Equals(
                    loanAgreement.Currency,
                    terms.Currency,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The loan agreement does not match the Onyx participation terms.");
            }

            if (graduatedAt < loanAgreement.EffectiveAt.Value)
            {
                throw new ArgumentException(
                    "Graduation cannot precede the effective loan agreement.",
                    nameof(graduatedAt));
            }

            return new OnyxParticipation(
                aqGreenParticipation.TenantId,
                aqGreenParticipation.CustomerId,
                recruiterCustomerId: null,
                onyxMembershipId,
                terms,
                graduatedAt)
            {
                Id = Guid.NewGuid(),
                AdmissionRoute = OnyxAdmissionRoute.EntryGraduation,
                Status = OnyxParticipationStatus.Active,
                ActivatedAt = graduatedAt,
                EntryParticipationId = aqGreenParticipation.Id,
                LoanAgreementId = loanAgreement.Id
            };
        }

        public void ApplyConfirmedDirectEntryPayment(MemberPayment payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (DirectEntryPaymentId == payment.Id)
            {
                return;
            }

            if (IsSettled || DirectEntryPaymentId.HasValue)
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
            Status = OnyxParticipationStatus.PaymentConfirmedAwaitingApproval;
        }

        public void ApproveByAdministrator(long administratorUserId, DateTime decidedAt)
        {
            EnsureAwaitingAdministrativeApproval();
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (decidedAt == default) throw new ArgumentException("A decision time is required.", nameof(decidedAt));
            if (decidedAt < StartedAt)
                throw new ArgumentException("Approval cannot precede the participation start.", nameof(decidedAt));

            _approvalDecisions.Add(OnyxParticipationApprovalDecision.Approve(administratorUserId, decidedAt));
            ActivatedAt = decidedAt;
            Status = OnyxParticipationStatus.Active;
        }

        public void RejectByAdministrator(long administratorUserId, string reason, DateTime decidedAt)
        {
            EnsureAwaitingAdministrativeApproval();
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (decidedAt == default) throw new ArgumentException("A decision time is required.", nameof(decidedAt));

            _approvalDecisions.Add(
                OnyxParticipationApprovalDecision.Reject(administratorUserId, reason, decidedAt));
            Status = OnyxParticipationStatus.Rejected;
        }

        private void EnsureAwaitingAdministrativeApproval()
        {
            if (!IsAwaitingAdministrativeApproval)
            {
                throw new InvalidOperationException(
                    "The participation is not awaiting administrative approval.");
            }
        }

        private bool IsSettled =>
            Status is OnyxParticipationStatus.Active or
                OnyxParticipationStatus.PaymentConfirmedAwaitingApproval or
                OnyxParticipationStatus.Rejected;

        public void CorrectRecruiter(
            OnyxParticipation newRecruiterParticipation,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            if (newRecruiterParticipation == null)
                throw new ArgumentNullException(nameof(newRecruiterParticipation));
            if (newRecruiterParticipation.Status != OnyxParticipationStatus.Active)
                throw new InvalidOperationException("The inviting Club Member must have active Onyx participation.");

            EnsureValidRecruiter(CustomerId, newRecruiterParticipation.CustomerId);
            RecordRecruiterCorrection(
                newRecruiterParticipation.CustomerId,
                administratorUserId,
                reason,
                correctedAt);
        }

        public void CorrectToIndependent(
            long administratorUserId,
            string reason,
            DateTime correctedAt) =>
            RecordRecruiterCorrection(null, administratorUserId, reason, correctedAt);

        private void RecordRecruiterCorrection(
            int? newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            EnsureValidRecruiter(CustomerId, newRecruiterCustomerId);
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A correction reason is required.", nameof(reason));
            if (correctedAt == default || correctedAt < StartedAt)
                throw new ArgumentException("A valid correction time is required.", nameof(correctedAt));
            if (newRecruiterCustomerId == RecruiterCustomerId) return;
            if (_recruiterCorrections.Count > 0 &&
                correctedAt <= _recruiterCorrections.Max(item => item.CorrectedAt))
            {
                throw new InvalidOperationException(
                    "Recruiter corrections must be recorded in strictly increasing effective-time order.");
            }

            _recruiterCorrections.Add(OnyxRecruiterCorrection.Record(
                RecruiterCustomerId,
                newRecruiterCustomerId,
                administratorUserId,
                reason,
                correctedAt));
            RecruiterCustomerId = newRecruiterCustomerId;
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

    public class OnyxRecruiterCorrection : Entity<Guid>
    {
        public int? PreviousRecruiterCustomerId { get; private set; }
        public int? NewRecruiterCustomerId { get; private set; }
        public long AdministratorUserId { get; private set; }
        public string Reason { get; private set; }
        public DateTime CorrectedAt { get; private set; }

        protected OnyxRecruiterCorrection()
        {
        }

        private OnyxRecruiterCorrection(
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

        internal static OnyxRecruiterCorrection Record(
            int? previousRecruiterCustomerId,
            int? newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt) =>
            new OnyxRecruiterCorrection(
                previousRecruiterCustomerId,
                newRecruiterCustomerId,
                administratorUserId,
                reason,
                correctedAt);
    }

    public class OnyxParticipationApprovalDecision : Entity<Guid>
    {
        public const int MaxRejectionReasonLength = 1000;

        public long AdministratorUserId { get; private set; }
        public bool Approved { get; private set; }
        public string Reason { get; private set; }
        public DateTime DecidedAt { get; private set; }

        protected OnyxParticipationApprovalDecision()
        {
        }

        private OnyxParticipationApprovalDecision(
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

        internal static OnyxParticipationApprovalDecision Approve(
            long administratorUserId,
            DateTime decidedAt)
        {
            return new OnyxParticipationApprovalDecision(administratorUserId, true, null, decidedAt);
        }

        internal static OnyxParticipationApprovalDecision Reject(
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

            return new OnyxParticipationApprovalDecision(
                administratorUserId,
                false,
                normalizedReason,
                decidedAt);
        }
    }
}
