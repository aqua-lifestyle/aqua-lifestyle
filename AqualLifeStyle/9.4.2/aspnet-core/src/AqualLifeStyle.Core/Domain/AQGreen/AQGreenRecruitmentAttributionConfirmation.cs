using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    public enum AQGreenAttributionConfirmationMethod
    {
        MemberInvitationAcceptance = 1,
        AuthorisedProspectiveRootConfirmation = 2
    }

    /// <summary>
    /// Immutable positive confirmation of a specific recruitment attribution.
    /// EvidenceReferenceId identifies durable non-secret acceptance evidence.
    /// </summary>
    public sealed class AQGreenRecruitmentAttributionConfirmation
        : AggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; private set; }
        public Guid AttributionId { get; private set; }
        public DateTime ConfirmedAt { get; private set; }
        public long? ConfirmedByUserId { get; private set; }
        public AQGreenAttributionConfirmationMethod ConfirmationMethod { get; private set; }
        public Guid EvidenceReferenceId { get; private set; }
        public string RulesVersion { get; private set; }

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenRecruitmentAttributionConfirmation()
        {
        }

        private AQGreenRecruitmentAttributionConfirmation(
            AQGreenRecruitmentAttribution attribution,
            DateTime confirmedAt,
            long? confirmedByUserId,
            AQGreenAttributionConfirmationMethod confirmationMethod,
            Guid evidenceReferenceId,
            string rulesVersion)
        {
            if (attribution == null) throw new ArgumentNullException(nameof(attribution));
            if (confirmationMethod != AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance &&
                confirmationMethod != AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation)
                throw new ArgumentOutOfRangeException(
                    nameof(confirmationMethod),
                    confirmationMethod,
                    "The AQGreen attribution confirmation method is not authorised.");
            if ((confirmationMethod == AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance &&
                 attribution.AttributionKind != AQGreenRecruitmentAttributionKind.SponsoredParticipant) ||
                (confirmationMethod == AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation &&
                 attribution.AttributionKind != AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot))
                throw new InvalidOperationException(
                    "The confirmation method does not match the attribution source.");
            if (confirmedAt == default || confirmedAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC confirmation time is required.",
                    nameof(confirmedAt));
            if (confirmedAt < attribution.AttributedAt)
                throw new ArgumentException(
                    "Attribution confirmation cannot precede attribution.",
                    nameof(confirmedAt));
            if (confirmedByUserId <= 0)
                throw new ArgumentOutOfRangeException(nameof(confirmedByUserId));
            if (evidenceReferenceId == Guid.Empty)
                throw new ArgumentException(
                    "A durable non-secret confirmation evidence reference is required.",
                    nameof(evidenceReferenceId));

            Id = Guid.NewGuid();
            TenantId = attribution.TenantId;
            AttributionId = attribution.Id;
            ConfirmedAt = confirmedAt;
            ConfirmedByUserId = confirmedByUserId;
            ConfirmationMethod = confirmationMethod;
            EvidenceReferenceId = evidenceReferenceId;
            RulesVersion = NormalizeRulesVersion(rulesVersion);
        }

        public static AQGreenRecruitmentAttributionConfirmation Confirm(
            AQGreenRecruitmentAttribution attribution,
            DateTime confirmedAt,
            long? confirmedByUserId,
            AQGreenAttributionConfirmationMethod confirmationMethod,
            Guid evidenceReferenceId,
            string rulesVersion) =>
            new(
                attribution,
                confirmedAt,
                confirmedByUserId,
                confirmationMethod,
                evidenceReferenceId,
                rulesVersion);

        private static string NormalizeRulesVersion(string rulesVersion)
        {
            if (string.IsNullOrWhiteSpace(rulesVersion))
                throw new ArgumentException(
                    "A confirmation rules version is required.",
                    nameof(rulesVersion));

            var normalized = rulesVersion.Trim();
            if (normalized.Length > AQGreenRecruitmentAttributionRules.MaximumRulesVersionLength)
                throw new ArgumentException(
                    $"Confirmation rules versions cannot exceed {AQGreenRecruitmentAttributionRules.MaximumRulesVersionLength} characters.",
                    nameof(rulesVersion));
            return normalized;
        }
    }
}
