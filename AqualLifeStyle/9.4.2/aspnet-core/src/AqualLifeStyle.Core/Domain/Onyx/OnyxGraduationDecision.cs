using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum AQGreenGraduationStructuralModel
    {
        LegacyV1 = 1,
        PlacementV2 = 2
    }

    public static class OnyxGraduationRules
    {
        public const int MaximumVersionLength = 64;
        public const string CurrentVersion = "OnyxGraduationV1";

        public static bool IsSupportedVersion(string version) =>
            string.Equals(version, CurrentVersion, StringComparison.Ordinal);
    }

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
        public AQGreenGraduationStructuralModel StructuralModel { get; private set; }
        public string GraduationRulesVersion { get; private set; }
        public EntryNetworkLevel? EvaluatedNetworkLevel { get; private set; }
        public bool AQGreenWasActive { get; private set; }
        public bool LoanWasActive { get; private set; }
        public bool LoanWasAccepted { get; private set; }
        public bool LoanWasAdministratorApproved { get; private set; }
        public decimal EvaluatedFundingAmount { get; private set; }
        public string EvaluatedFundingCurrency { get; private set; }
        public string EvaluatedLoanTermsVersion { get; private set; }

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
            if (evaluatedNetworkLevel < EntryNetworkLevel.Level2)
                throw new InvalidOperationException(
                    "AQGreen Level 2 is required for Onyx graduation.");
            return Create(
                aqGreenParticipation,
                loanAgreement,
                onyxParticipation,
                AQGreenGraduationStructuralModel.LegacyV1,
                evaluatedNetworkLevel,
                administratorUserId,
                justification,
                decidedAt);
        }

        public static OnyxGraduationDecision RecordPlacementV2Approval(
            EntryParticipation aqGreenParticipation,
            OnyxLoanAgreement loanAgreement,
            OnyxParticipation onyxParticipation,
            AQGreenGraduationStructuralEvidenceResult structuralEvidence,
            long administratorUserId,
            string justification,
            DateTime decidedAt)
        {
            if (structuralEvidence == null)
                throw new ArgumentNullException(nameof(structuralEvidence));
            if (structuralEvidence.ParticipantId != aqGreenParticipation?.Id ||
                structuralEvidence.Cutoff != decidedAt)
                throw new InvalidOperationException(
                    "The V2 structural evidence does not match this graduation decision.");
            if (structuralEvidence.StructuralCompletionLevel <
                AQGreenStructuralCompletionLevel.Level2)
                throw new InvalidOperationException(
                    "AQGreen Placement V2 Level 2 is required for Onyx graduation.");
            return Create(
                aqGreenParticipation,
                loanAgreement,
                onyxParticipation,
                AQGreenGraduationStructuralModel.PlacementV2,
                evaluatedNetworkLevel: null,
                administratorUserId,
                justification,
                decidedAt);
        }

        private static OnyxGraduationDecision Create(
            EntryParticipation aqGreenParticipation,
            OnyxLoanAgreement loanAgreement,
            OnyxParticipation onyxParticipation,
            AQGreenGraduationStructuralModel structuralModel,
            EntryNetworkLevel? evaluatedNetworkLevel,
            long administratorUserId,
            string justification,
            DateTime decidedAt)
        {
            if (aqGreenParticipation == null)
                throw new ArgumentNullException(nameof(aqGreenParticipation));
            if (loanAgreement == null)
                throw new ArgumentNullException(nameof(loanAgreement));
            if (onyxParticipation == null)
                throw new ArgumentNullException(nameof(onyxParticipation));
            if (onyxParticipation.AdmissionRoute != OnyxAdmissionRoute.EntryGraduation ||
                onyxParticipation.EntryParticipationId != aqGreenParticipation.Id ||
                onyxParticipation.LoanAgreementId != loanAgreement.Id)
                throw new InvalidOperationException(
                    "The resulting Onyx participation does not match the graduation evidence.");
            EnsureAcceptedAgreement(aqGreenParticipation, loanAgreement, decidedAt);
            if (!string.Equals(
                    onyxParticipation.TermsVersion,
                    loanAgreement.TermsVersion,
                    StringComparison.Ordinal) ||
                onyxParticipation.TermsEffectiveFrom != loanAgreement.EffectiveAt ||
                onyxParticipation.DirectEntryAmount != loanAgreement.PrincipalAmount ||
                !string.Equals(
                    onyxParticipation.Currency,
                    loanAgreement.Currency,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The resulting Onyx participation does not preserve the canonical accepted agreement terms.");
            if (administratorUserId <= 0)
                throw new ArgumentOutOfRangeException(nameof(administratorUserId));
            if (decidedAt == default || decidedAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC decision time is required.",
                    nameof(decidedAt));
            var normalizedJustification = justification?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedJustification))
                throw new ArgumentException(
                    "A graduation justification is required.",
                    nameof(justification));
            if (normalizedJustification.Length > 2000)
                throw new ArgumentException(
                    "The graduation justification cannot exceed 2000 characters.",
                    nameof(justification));

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
                StructuralModel = structuralModel,
                GraduationRulesVersion = OnyxGraduationRules.CurrentVersion,
                EvaluatedNetworkLevel = evaluatedNetworkLevel,
                AQGreenWasActive = true,
                LoanWasActive = true,
                LoanWasAccepted = true,
                LoanWasAdministratorApproved = true,
                EvaluatedFundingAmount = onyxParticipation.DirectEntryAmount,
                EvaluatedFundingCurrency = onyxParticipation.Currency,
                EvaluatedLoanTermsVersion = onyxParticipation.TermsVersion
            };
        }

        private static void EnsureAcceptedAgreement(
            EntryParticipation aqGreenParticipation,
            OnyxLoanAgreement loanAgreement,
            DateTime decidedAt)
        {
            if (aqGreenParticipation.Status != EntryParticipationStatus.Active ||
                loanAgreement.Status != OnyxLoanAgreementStatus.Active ||
                !loanAgreement.EffectiveAt.HasValue ||
                !loanAgreement.MemberAcceptedAt.HasValue ||
                !loanAgreement.MemberAcceptedByUserId.HasValue ||
                !loanAgreement.ApprovedAt.HasValue ||
                !loanAgreement.ApprovedByAdministratorUserId.HasValue)
                throw new InvalidOperationException(
                    "The confirmed graduation evidence is no longer valid.");
            if (loanAgreement.TenantId != aqGreenParticipation.TenantId ||
                loanAgreement.CustomerId != aqGreenParticipation.CustomerId ||
                loanAgreement.EntryParticipationId != aqGreenParticipation.Id)
                throw new InvalidOperationException(
                    "The accepted loan agreement does not belong to this AQGreen participation.");
            if (string.IsNullOrWhiteSpace(loanAgreement.TermsVersion) ||
                loanAgreement.PrincipalAmount <= 0m ||
                string.IsNullOrWhiteSpace(loanAgreement.Currency) ||
                loanAgreement.Currency.Length != 3)
                throw new InvalidOperationException(
                    "The accepted loan agreement terms are invalid.");
            if (decidedAt < loanAgreement.EffectiveAt.Value)
                throw new InvalidOperationException(
                    "Graduation cannot precede the effective accepted agreement.");
        }
    }
}
