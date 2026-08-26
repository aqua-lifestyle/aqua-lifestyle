using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    public enum AQGreenAcquisitionSource
    {
        MemberInvitation = 1,
        AuthorisedDirectAdmission = 2
    }

    public enum AQGreenRecruitmentAttributionKind
    {
        SponsoredParticipant = 1,
        AuthorisedProspectiveRoot = 2
    }

    public static class AQGreenRecruitmentAttributionRules
    {
        public const int MaximumAssignmentReasonLength = 500;
        public const int MaximumRulesVersionLength = 64;
        public const string CurrentVersion = "AQGreenRecruitmentAttributionV1";
    }

    /// <summary>
    /// Immutable acquisition provenance and recruitment credit for one AQGreen participation.
    /// SourceReferenceId identifies durable non-secret evidence, such as ProgrammeInvitation.Id.
    /// Prospective-root evidence does not establish scope creation, placement, or activation.
    /// </summary>
    public sealed class AQGreenRecruitmentAttribution
        : AggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; private set; }
        public Guid ParticipantId { get; private set; }
        public Guid? CreditedSponsorParticipantId { get; private set; }
        public AQGreenRecruitmentAttributionKind AttributionKind { get; private set; }
        public AQGreenAcquisitionSource AcquisitionSource { get; private set; }
        public Guid SourceReferenceId { get; private set; }
        public DateTime AttributedAt { get; private set; }
        public long? AttributedByUserId { get; private set; }
        public string AssignmentReason { get; private set; }
        public string RulesVersion { get; private set; }

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenRecruitmentAttribution()
        {
        }

        private AQGreenRecruitmentAttribution(
            int tenantId,
            Guid participantId,
            Guid? creditedSponsorParticipantId,
            AQGreenRecruitmentAttributionKind attributionKind,
            AQGreenAcquisitionSource acquisitionSource,
            Guid sourceReferenceId,
            DateTime attributedAt,
            long? attributedByUserId,
            string assignmentReason,
            string rulesVersion)
        {
            EnsureIdentity(tenantId, participantId, creditedSponsorParticipantId);
            EnsureControlledKind(attributionKind);
            EnsureControlledSource(acquisitionSource);
            EnsureSourceReference(sourceReferenceId);
            EnsureAuthoritativeTime(attributedAt);
            EnsureActor(attributedByUserId);

            var normalizedReason = NormalizeOptionalReason(assignmentReason);
            EnsureSourceShape(
                attributionKind,
                acquisitionSource,
                creditedSponsorParticipantId,
                attributedByUserId,
                normalizedReason);

            Id = Guid.NewGuid();
            TenantId = tenantId;
            ParticipantId = participantId;
            CreditedSponsorParticipantId = creditedSponsorParticipantId;
            AttributionKind = attributionKind;
            AcquisitionSource = acquisitionSource;
            SourceReferenceId = sourceReferenceId;
            AttributedAt = attributedAt;
            AttributedByUserId = attributedByUserId;
            AssignmentReason = normalizedReason;
            RulesVersion = NormalizeRulesVersion(rulesVersion);
        }

        public static AQGreenRecruitmentAttribution Create(
            int tenantId,
            Guid participantId,
            Guid? creditedSponsorParticipantId,
            AQGreenRecruitmentAttributionKind attributionKind,
            AQGreenAcquisitionSource acquisitionSource,
            Guid sourceReferenceId,
            DateTime attributedAt,
            long? attributedByUserId,
            string assignmentReason,
            string rulesVersion) =>
            new(
                tenantId,
                participantId,
                creditedSponsorParticipantId,
                attributionKind,
                acquisitionSource,
                sourceReferenceId,
                attributedAt,
                attributedByUserId,
                assignmentReason,
                rulesVersion);

        private static void EnsureIdentity(
            int tenantId,
            Guid participantId,
            Guid? creditedSponsorParticipantId)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (creditedSponsorParticipantId == Guid.Empty)
                throw new ArgumentException(
                    "A credited sponsor participation must not be empty.",
                    nameof(creditedSponsorParticipantId));
            if (participantId == creditedSponsorParticipantId)
                throw new InvalidOperationException(
                    "An AQGreen participant cannot receive recruitment credit for themselves.");
        }

        private static void EnsureControlledKind(
            AQGreenRecruitmentAttributionKind attributionKind)
        {
            if (attributionKind != AQGreenRecruitmentAttributionKind.SponsoredParticipant &&
                attributionKind != AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attributionKind),
                    attributionKind,
                    "The AQGreen attribution kind is not authorised.");
            }
        }

        private static void EnsureControlledSource(AQGreenAcquisitionSource acquisitionSource)
        {
            if (acquisitionSource != AQGreenAcquisitionSource.MemberInvitation &&
                acquisitionSource != AQGreenAcquisitionSource.AuthorisedDirectAdmission)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(acquisitionSource),
                    acquisitionSource,
                    "The AQGreen acquisition source is not authorised.");
            }
        }

        private static void EnsureSourceReference(Guid sourceReferenceId)
        {
            if (sourceReferenceId == Guid.Empty)
                throw new ArgumentException(
                    "A durable non-secret acquisition source reference is required.",
                    nameof(sourceReferenceId));
        }

        private static void EnsureAuthoritativeTime(DateTime attributedAt)
        {
            if (attributedAt == default || attributedAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC attribution time is required.",
                    nameof(attributedAt));
        }

        private static void EnsureActor(long? attributedByUserId)
        {
            if (attributedByUserId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributedByUserId));
        }

        private static string NormalizeOptionalReason(string assignmentReason)
        {
            if (assignmentReason == null) return null;
            if (string.IsNullOrWhiteSpace(assignmentReason))
                throw new ArgumentException(
                    "An assignment reason must not be blank.",
                    nameof(assignmentReason));

            var normalized = assignmentReason.Trim();
            if (normalized.Length > AQGreenRecruitmentAttributionRules.MaximumAssignmentReasonLength)
                throw new ArgumentException(
                    $"Assignment reasons cannot exceed {AQGreenRecruitmentAttributionRules.MaximumAssignmentReasonLength} characters.",
                    nameof(assignmentReason));
            return normalized;
        }

        private static void EnsureSourceShape(
            AQGreenRecruitmentAttributionKind attributionKind,
            AQGreenAcquisitionSource acquisitionSource,
            Guid? creditedSponsorParticipantId,
            long? attributedByUserId,
            string assignmentReason)
        {
            if (attributionKind == AQGreenRecruitmentAttributionKind.SponsoredParticipant)
            {
                if (acquisitionSource != AQGreenAcquisitionSource.MemberInvitation)
                    throw new InvalidOperationException(
                        "Sponsored attribution requires member-invitation provenance.");
                if (!creditedSponsorParticipantId.HasValue)
                    throw new InvalidOperationException(
                        "Member-invitation attribution requires a credited sponsor participation.");
                if (assignmentReason != null)
                    throw new InvalidOperationException(
                        "Member-invitation attribution does not use an administrative assignment reason.");
                return;
            }

            if (acquisitionSource != AQGreenAcquisitionSource.AuthorisedDirectAdmission)
                throw new InvalidOperationException(
                    "Authorised-root attribution requires direct-admission provenance.");
            if (creditedSponsorParticipantId.HasValue)
                throw new InvalidOperationException(
                    "An authorised root attribution cannot have a credited sponsor.");
            if (!attributedByUserId.HasValue)
                throw new InvalidOperationException(
                    "An authorised root attribution requires an audit actor.");
            if (assignmentReason == null)
                throw new InvalidOperationException(
                    "An authorised root attribution requires an assignment reason.");
        }

        private static string NormalizeRulesVersion(string rulesVersion)
        {
            if (string.IsNullOrWhiteSpace(rulesVersion))
                throw new ArgumentException(
                    "An attribution rules version is required.",
                    nameof(rulesVersion));

            var normalized = rulesVersion.Trim();
            if (normalized.Length > AQGreenRecruitmentAttributionRules.MaximumRulesVersionLength)
                throw new ArgumentException(
                    $"Attribution rules versions cannot exceed {AQGreenRecruitmentAttributionRules.MaximumRulesVersionLength} characters.",
                    nameof(rulesVersion));
            return normalized;
        }
    }
}
