using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    public class OnyxGraduationDecision : CreationAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public Guid EntryParticipationId { get; private set; }
        public Guid LoanAgreementId { get; private set; }
        public Guid OnyxParticipationId { get; private set; }
        public long AdministratorUserId { get; private set; }
        public DateTime DecidedAt { get; private set; }
        public string Justification { get; private set; }
        public EntryNetworkLevel EvaluatedNetworkLevel { get; private set; }
        public bool AQGreenWasActive { get; private set; }
        public bool LoanWasActive { get; private set; }
        public bool LoanWasAccepted { get; private set; }
        public bool LoanWasAdministratorApproved { get; private set; }
        public decimal EvaluatedFundingAmount { get; private set; }
        public string EvaluatedFundingCurrency { get; private set; }

        protected OnyxGraduationDecision()
        {
        }

        public static OnyxGraduationDecision RecordApproval(
            EntryParticipation aqGreenParticipation,
            OnyxLoanAgreement loanAgreement,
            OnyxParticipation onyxParticipation,
            EntryNetworkLevel evaluatedNetworkLevel,
            long administratorUserId,
            string justification,
            DateTime decidedAt)
        {
            if (aqGreenParticipation == null) throw new ArgumentNullException(nameof(aqGreenParticipation));
            if (loanAgreement == null) throw new ArgumentNullException(nameof(loanAgreement));
            if (onyxParticipation == null) throw new ArgumentNullException(nameof(onyxParticipation));
            if (onyxParticipation.AdmissionRoute != OnyxAdmissionRoute.EntryGraduation ||
                onyxParticipation.EntryParticipationId != aqGreenParticipation.Id ||
                onyxParticipation.LoanAgreementId != loanAgreement.Id)
                throw new InvalidOperationException("The resulting Onyx participation does not match the graduation evidence.");
            if (evaluatedNetworkLevel < EntryNetworkLevel.Level2)
                throw new InvalidOperationException("AQGreen Level 2 is required for Onyx graduation.");
            if (aqGreenParticipation.Status != EntryParticipationStatus.Active ||
                loanAgreement.Status != OnyxLoanAgreementStatus.Active ||
                !loanAgreement.MemberAcceptedAt.HasValue ||
                !loanAgreement.ApprovedAt.HasValue)
                throw new InvalidOperationException("The confirmed graduation evidence is no longer valid.");
            if (administratorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (decidedAt == default) throw new ArgumentException("A decision time is required.", nameof(decidedAt));
            var normalizedJustification = justification?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedJustification))
                throw new ArgumentException("A graduation justification is required.", nameof(justification));
            if (normalizedJustification.Length > 2000)
                throw new ArgumentException("The graduation justification cannot exceed 2000 characters.", nameof(justification));

            return new OnyxGraduationDecision
            {
                Id = Guid.NewGuid(),
                TenantId = aqGreenParticipation.TenantId,
                CustomerId = aqGreenParticipation.CustomerId,
                EntryParticipationId = aqGreenParticipation.Id,
                LoanAgreementId = loanAgreement.Id,
                OnyxParticipationId = onyxParticipation.Id,
                AdministratorUserId = administratorUserId,
                DecidedAt = decidedAt,
                Justification = normalizedJustification,
                EvaluatedNetworkLevel = evaluatedNetworkLevel,
                AQGreenWasActive = aqGreenParticipation.Status == EntryParticipationStatus.Active,
                LoanWasActive = loanAgreement.Status == OnyxLoanAgreementStatus.Active,
                LoanWasAccepted = loanAgreement.MemberAcceptedAt.HasValue,
                LoanWasAdministratorApproved = loanAgreement.ApprovedAt.HasValue,
                EvaluatedFundingAmount = loanAgreement.PrincipalAmount,
                EvaluatedFundingCurrency = loanAgreement.Currency
            };
        }
    }
}
