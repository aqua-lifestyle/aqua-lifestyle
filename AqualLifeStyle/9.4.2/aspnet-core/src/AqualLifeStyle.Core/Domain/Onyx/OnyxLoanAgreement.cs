using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum OnyxLoanAgreementStatus
    {
        AwaitingMemberAcceptance = 0,
        AwaitingAdministratorApproval = 1,
        Active = 2,
        Overdue = 3,
        Settled = 4
    }

    public enum OnyxLoanWeeklyRequirementStatus
    {
        Due = 0,
        Overdue = 1,
        Satisfied = 2
    }

    public class OnyxLoanAgreement : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<OnyxLoanWeeklyRequirement> _weeklyRequirements = new();
        private readonly List<OnyxLoanRepaymentAllocation> _repayments = new();

        public int TenantId { get; set; }
        public Guid EntryParticipationId { get; private set; }
        public int CustomerId { get; private set; }
        public OnyxLoanAgreementStatus Status { get; private set; }
        public string TermsVersion { get; private set; }
        public decimal PrincipalAmount { get; private set; }
        public decimal InterestRatePercent { get; private set; }
        public decimal TotalPayableAmount { get; private set; }
        public decimal OutstandingAmount { get; private set; }
        public string Currency { get; private set; }
        public int RepaymentPeriodMonths { get; private set; }
        public int InitialWeeklyRequirementCount { get; private set; }
        public decimal InitialWeeklyMinimumAmount { get; private set; }
        public DateTime OfferedAt { get; private set; }
        public long? MemberAcceptedByUserId { get; private set; }
        public string MemberConfirmation { get; private set; }
        public DateTime? MemberAcceptedAt { get; private set; }
        public long? ApprovedByAdministratorUserId { get; private set; }
        public DateTime? ApprovedAt { get; private set; }
        public DateTime? EffectiveAt { get; private set; }
        public DateTime? RepaymentDeadlineAt { get; private set; }
        public DateTime? LastAssessedAt { get; private set; }
        public DateTime? SettledAt { get; private set; }
        public bool RequiresPayoutHold =>
            Status == OnyxLoanAgreementStatus.Overdue ||
            (Status == OnyxLoanAgreementStatus.Active &&
             _weeklyRequirements.Any(requirement =>
                 requirement.Status == OnyxLoanWeeklyRequirementStatus.Overdue));

        /// <summary>
        /// Evaluates the payout-hold rule at a historical cutoff from immutable
        /// facts only: agreement existence by <see cref="EffectiveAt"/>, settlement
        /// by <see cref="SettledAt"/>, agreement-level overdue by
        /// <see cref="RepaymentDeadlineAt"/> and the repayments received by the
        /// cutoff, and weekly requirement standing by each requirement's due and
        /// satisfaction timestamps. The current <see cref="Status"/> projection,
        /// <see cref="OutstandingAmount"/>, and requirement statuses are never
        /// consulted, so later assessments or repayments cannot rewrite a closed
        /// commission cycle.
        /// </summary>
        public bool WasRequiringPayoutHoldAt(DateTime cutoffUtc)
        {
            if (cutoffUtc == default)
            {
                throw new ArgumentException(
                    "A cutoff time is required.",
                    nameof(cutoffUtc));
            }

            if (!EffectiveAt.HasValue || EffectiveAt.Value > cutoffUtc)
            {
                return false;
            }

            if (SettledAt.HasValue && SettledAt.Value <= cutoffUtc)
            {
                return false;
            }

            if (cutoffUtc > RepaymentDeadlineAt.Value &&
                OutstandingAt(cutoffUtc) > 0m)
            {
                return true;
            }

            return _weeklyRequirements.Any(requirement =>
                requirement.WasOverdueAt(cutoffUtc));
        }

        private decimal OutstandingAt(DateTime cutoffUtc)
        {
            var settledByCutoff = _repayments
                .Where(repayment => repayment.ReceivedAt <= cutoffUtc)
                .Sum(repayment => repayment.Amount);
            return TotalPayableAmount - settledByCutoff;
        }
        public IReadOnlyCollection<OnyxLoanWeeklyRequirement> WeeklyRequirements =>
            _weeklyRequirements.AsReadOnly();
        public IReadOnlyCollection<OnyxLoanRepaymentAllocation> Repayments =>
            _repayments.AsReadOnly();

        protected OnyxLoanAgreement()
        {
        }

        private OnyxLoanAgreement(
            EntryParticipation participation,
            OnyxLoanTerms terms,
            DateTime offeredAt)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            if (offeredAt == default || offeredAt < terms.EffectiveFrom)
            {
                throw new ArgumentException(
                    "The loan offer cannot precede its terms.",
                    nameof(offeredAt));
            }

            Id = Guid.NewGuid();
            TenantId = participation.TenantId;
            EntryParticipationId = participation.Id;
            CustomerId = participation.CustomerId;
            Status = OnyxLoanAgreementStatus.AwaitingMemberAcceptance;
            TermsVersion = terms.Version;
            PrincipalAmount = terms.PrincipalAmount;
            InterestRatePercent = terms.InterestRatePercent;
            TotalPayableAmount = terms.TotalPayableAmount;
            OutstandingAmount = terms.TotalPayableAmount;
            Currency = terms.Currency;
            RepaymentPeriodMonths = terms.RepaymentPeriodMonths;
            InitialWeeklyRequirementCount = terms.InitialWeeklyRequirementCount;
            InitialWeeklyMinimumAmount = terms.InitialWeeklyMinimumAmount;
            OfferedAt = offeredAt;
        }

        public static OnyxLoanAgreement OfferToEligibleEntryParticipant(
            EntryParticipation participation,
            IEnumerable<EntryParticipation> networkParticipations,
            EntryNetworkQualificationEvaluator qualificationEvaluator,
            OnyxLoanTerms terms,
            DateTime offeredAt)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (networkParticipations == null)
            {
                throw new ArgumentNullException(nameof(networkParticipations));
            }

            if (qualificationEvaluator == null)
            {
                throw new ArgumentNullException(nameof(qualificationEvaluator));
            }

            var level = qualificationEvaluator.Evaluate(
                participation.CustomerId,
                networkParticipations);
            if (level < EntryNetworkLevel.Level2)
            {
                throw new InvalidOperationException(
                    "The loan agreement becomes available at AQGreen Level 2.");
            }

            return new OnyxLoanAgreement(participation, terms, offeredAt);
        }

        public void AcceptByMember(
            long memberUserId,
            string confirmation,
            DateTime acceptedAt)
        {
            if (memberUserId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(memberUserId));
            }

            if (string.IsNullOrWhiteSpace(confirmation))
            {
                throw new ArgumentException(
                    "The member must confirm the loan terms.",
                    nameof(confirmation));
            }

            if (acceptedAt == default || acceptedAt < OfferedAt)
            {
                throw new ArgumentException(
                    "The acceptance time cannot precede the offer.",
                    nameof(acceptedAt));
            }

            var normalizedConfirmation = confirmation.Trim();
            if (normalizedConfirmation.Length > 512)
            {
                throw new ArgumentException(
                    "The member confirmation cannot exceed 512 characters.",
                    nameof(confirmation));
            }
            if (MemberAcceptedAt.HasValue)
            {
                if (MemberAcceptedByUserId != memberUserId ||
                    !string.Equals(
                        MemberConfirmation,
                        normalizedConfirmation,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The agreement was already accepted using different member facts.");
                }

                return;
            }

            if (Status != OnyxLoanAgreementStatus.AwaitingMemberAcceptance)
            {
                throw new InvalidOperationException(
                    "This loan agreement is not awaiting member acceptance.");
            }

            MemberAcceptedByUserId = memberUserId;
            MemberConfirmation = normalizedConfirmation;
            MemberAcceptedAt = acceptedAt;
            Status = OnyxLoanAgreementStatus.AwaitingAdministratorApproval;
        }

        public void ApproveByAdministrator(
            long administratorUserId,
            DateTime approvedAt)
        {
            if (administratorUserId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            }

            if (!MemberAcceptedAt.HasValue)
            {
                throw new InvalidOperationException(
                    "The member must accept the agreement before administrator approval.");
            }

            if (approvedAt == default || approvedAt < MemberAcceptedAt.Value)
            {
                throw new ArgumentException(
                    "Approval cannot precede member acceptance.",
                    nameof(approvedAt));
            }

            if (ApprovedAt.HasValue)
            {
                if (ApprovedByAdministratorUserId != administratorUserId ||
                    ApprovedAt != approvedAt)
                {
                    throw new InvalidOperationException(
                        "The agreement was already approved using different administrator facts.");
                }

                return;
            }

            if (Status != OnyxLoanAgreementStatus.AwaitingAdministratorApproval)
            {
                throw new InvalidOperationException(
                    "This loan agreement is not awaiting administrator approval.");
            }

            ApprovedByAdministratorUserId = administratorUserId;
            ApprovedAt = approvedAt;
            EffectiveAt = approvedAt;
            RepaymentDeadlineAt = approvedAt.AddMonths(RepaymentPeriodMonths);
            Status = OnyxLoanAgreementStatus.Active;

            for (var number = 1; number <= InitialWeeklyRequirementCount; number++)
            {
                _weeklyRequirements.Add(OnyxLoanWeeklyRequirement.Create(
                    number,
                    InitialWeeklyMinimumAmount,
                    approvedAt.AddDays(7 * number)));
            }
        }

        public void AssessCompliance(DateTime asOf)
        {
            EnsureAgreementIsEffective();
            if (asOf == default)
            {
                throw new ArgumentException("An assessment time is required.", nameof(asOf));
            }

            if (LastAssessedAt.HasValue && asOf < LastAssessedAt.Value)
            {
                throw new InvalidOperationException(
                    "A loan agreement cannot be reassessed at an earlier time.");
            }

            if (Status == OnyxLoanAgreementStatus.Settled)
            {
                return;
            }

            LastAssessedAt = asOf;
            foreach (var requirement in _weeklyRequirements)
            {
                requirement.Assess(asOf);
            }

            if (asOf > RepaymentDeadlineAt.Value && OutstandingAmount > 0m)
            {
                Status = OnyxLoanAgreementStatus.Overdue;
            }
        }

        public void ApplyConfirmedRepayment(
            MemberPayment payment,
            int? weeklyRequirementNumber = null)
        {
            EnsureAgreementIsEffective();
            if (payment == null)
            {
                throw new ArgumentNullException(nameof(payment));
            }

            var existing = _repayments.SingleOrDefault(
                repayment => repayment.PaymentId == payment.Id);
            if (existing != null)
            {
                existing.EnsureMatches(payment, weeklyRequirementNumber);
                return;
            }

            if (Status == OnyxLoanAgreementStatus.Settled)
            {
                throw new InvalidOperationException(
                    "This loan agreement has already been settled.");
            }

            if (payment.Status != MemberPaymentStatus.Confirmed)
            {
                throw new InvalidOperationException(
                    "Only a confirmed payment can be applied to the loan.");
            }

            if (payment.TenantId != TenantId || payment.CustomerId != CustomerId)
            {
                throw new InvalidOperationException(
                    "The payment does not belong to this loan agreement.");
            }

            if (payment.Purpose != MemberPaymentPurpose.OnyxLoanRepayment)
            {
                throw new InvalidOperationException(
                    "The payment is not an Onyx loan repayment.");
            }

            if (!string.Equals(payment.Currency, Currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The payment currency does not match the loan agreement.");
            }

            if (payment.Amount > OutstandingAmount)
            {
                throw new InvalidOperationException(
                    $"The payment cannot exceed the outstanding {Currency} {OutstandingAmount:0.00}.");
            }

            OnyxLoanWeeklyRequirement requirement = null;
            if (weeklyRequirementNumber.HasValue)
            {
                requirement = _weeklyRequirements.SingleOrDefault(
                    item => item.RequirementNumber == weeklyRequirementNumber.Value);
                if (requirement == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(weeklyRequirementNumber));
                }
            }

            requirement?.Credit(payment.Amount, payment.ConfirmedAt.Value);
            _repayments.Add(OnyxLoanRepaymentAllocation.Record(
                payment,
                weeklyRequirementNumber));
            OutstandingAmount -= payment.Amount;

            if (OutstandingAmount == 0m)
            {
                Status = OnyxLoanAgreementStatus.Settled;
                SettledAt = payment.ConfirmedAt;
            }
        }

        private void EnsureAgreementIsEffective()
        {
            if (!EffectiveAt.HasValue ||
                Status == OnyxLoanAgreementStatus.AwaitingMemberAcceptance ||
                Status == OnyxLoanAgreementStatus.AwaitingAdministratorApproval)
            {
                throw new InvalidOperationException(
                    "The loan agreement is not yet effective.");
            }
        }
    }

    public class OnyxLoanWeeklyRequirement : Entity<Guid>
    {
        public int RequirementNumber { get; private set; }
        public decimal MinimumAmount { get; private set; }
        public decimal CreditedAmount { get; private set; }
        public DateTime DueAt { get; private set; }
        public OnyxLoanWeeklyRequirementStatus Status { get; private set; }
        public DateTime? SatisfiedAt { get; private set; }
        public DateTime? MarkedOverdueAt { get; private set; }

        protected OnyxLoanWeeklyRequirement()
        {
        }

        private OnyxLoanWeeklyRequirement(
            int requirementNumber,
            decimal minimumAmount,
            DateTime dueAt)
        {
            Id = Guid.NewGuid();
            RequirementNumber = requirementNumber;
            MinimumAmount = minimumAmount;
            DueAt = dueAt;
            Status = OnyxLoanWeeklyRequirementStatus.Due;
        }

        internal static OnyxLoanWeeklyRequirement Create(
            int requirementNumber,
            decimal minimumAmount,
            DateTime dueAt)
        {
            if (requirementNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requirementNumber));
            }

            if (minimumAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAmount));
            }

            if (dueAt == default)
            {
                throw new ArgumentException("A weekly due time is required.", nameof(dueAt));
            }

            return new OnyxLoanWeeklyRequirement(
                requirementNumber,
                minimumAmount,
                dueAt);
        }

        internal void Assess(DateTime asOf)
        {
            if (Status == OnyxLoanWeeklyRequirementStatus.Satisfied)
            {
                return;
            }

            if (asOf > DueAt)
            {
                Status = OnyxLoanWeeklyRequirementStatus.Overdue;
                MarkedOverdueAt ??= asOf;
            }
        }

        internal void Credit(decimal amount, DateTime creditedAt)
        {
            CreditedAmount += amount;
            if (CreditedAmount >= MinimumAmount)
            {
                Status = OnyxLoanWeeklyRequirementStatus.Satisfied;
                SatisfiedAt ??= creditedAt;
            }
        }

        /// <summary>
        /// Evaluates the weekly requirement standing at a historical cutoff from
        /// immutable facts only: satisfaction is proven by <see cref="SatisfiedAt"/>
        /// and the overdue boundary by <see cref="DueAt"/>. The current
        /// <see cref="Status"/> projection is never consulted.
        /// </summary>
        public bool WasOverdueAt(DateTime cutoffUtc)
        {
            if (cutoffUtc == default)
            {
                throw new ArgumentException(
                    "A cutoff time is required.",
                    nameof(cutoffUtc));
            }

            if (SatisfiedAt.HasValue && SatisfiedAt.Value <= cutoffUtc)
            {
                return false;
            }

            return cutoffUtc > DueAt;
        }
    }

    public class OnyxLoanRepaymentAllocation : Entity<Guid>
    {
        public Guid PaymentId { get; private set; }
        public decimal Amount { get; private set; }
        public int? WeeklyRequirementNumber { get; private set; }
        public DateTime ReceivedAt { get; private set; }

        protected OnyxLoanRepaymentAllocation()
        {
        }

        private OnyxLoanRepaymentAllocation(
            Guid paymentId,
            decimal amount,
            int? weeklyRequirementNumber,
            DateTime receivedAt)
        {
            Id = Guid.NewGuid();
            PaymentId = paymentId;
            Amount = amount;
            WeeklyRequirementNumber = weeklyRequirementNumber;
            ReceivedAt = receivedAt;
        }

        internal static OnyxLoanRepaymentAllocation Record(
            MemberPayment payment,
            int? weeklyRequirementNumber)
        {
            return new OnyxLoanRepaymentAllocation(
                payment.Id,
                payment.Amount,
                weeklyRequirementNumber,
                payment.ConfirmedAt.Value);
        }

        internal void EnsureMatches(
            MemberPayment payment,
            int? weeklyRequirementNumber)
        {
            if (Amount != payment.Amount ||
                WeeklyRequirementNumber != weeklyRequirementNumber)
            {
                throw new InvalidOperationException(
                    "This repayment was already allocated using different payment facts.");
            }
        }
    }
}
