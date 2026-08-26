using System;
using System.Linq;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AQGreenRecruitmentAttributionTests
    {
        private static readonly DateTime AttributedAt =
            new(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void MemberInvitationAttribution_PreservesSponsorAndProvenanceSeparately()
        {
            var participantId = Guid.NewGuid();
            var sponsorId = Guid.NewGuid();
            var invitationId = Guid.NewGuid();

            var attribution = CreateMemberAttribution(
                participantId,
                sponsorId,
                invitationId);

            attribution.TenantId.ShouldBe(1);
            attribution.ParticipantId.ShouldBe(participantId);
            attribution.CreditedSponsorParticipantId.ShouldBe(sponsorId);
            attribution.AttributionKind.ShouldBe(
                AQGreenRecruitmentAttributionKind.SponsoredParticipant);
            attribution.AcquisitionSource.ShouldBe(AQGreenAcquisitionSource.MemberInvitation);
            attribution.SourceReferenceId.ShouldBe(invitationId);
            attribution.AssignmentReason.ShouldBeNull();
            attribution.RulesVersion.ShouldBe(AQGreenRecruitmentAttributionRules.CurrentVersion);
        }

        [Fact]
        public void AuthorisedRootAttribution_RequiresAndPreservesExplicitRootEvidence()
        {
            var attribution = AQGreenRecruitmentAttribution.Create(
                1,
                Guid.NewGuid(),
                null,
                AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                Guid.NewGuid(),
                AttributedAt,
                42,
                "  Approved prospective root attribution  ",
                AQGreenRecruitmentAttributionRules.CurrentVersion);

            attribution.CreditedSponsorParticipantId.ShouldBeNull();
            attribution.AttributionKind.ShouldBe(
                AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot);
            attribution.AcquisitionSource.ShouldBe(AQGreenAcquisitionSource.AuthorisedDirectAdmission);
            attribution.AttributedByUserId.ShouldBe(42);
            attribution.AssignmentReason.ShouldBe("Approved prospective root attribution");
        }

        [Fact]
        public void MemberInvitationAttribution_FailsClosedWithoutSponsor()
        {
            Should.Throw<InvalidOperationException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1,
                    Guid.NewGuid(),
                    null,
                    AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                    AQGreenAcquisitionSource.MemberInvitation,
                    Guid.NewGuid(),
                    AttributedAt,
                    null,
                    null,
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
        }

        [Fact]
        public void Attribution_RejectsSelfSponsorship()
        {
            var participantId = Guid.NewGuid();

            Should.Throw<InvalidOperationException>(() =>
                CreateMemberAttribution(participantId, participantId, Guid.NewGuid()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void Attribution_RejectsUndefinedAcquisitionSource(int value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                    (AQGreenAcquisitionSource)value,
                    Guid.NewGuid(),
                    AttributedAt,
                    null,
                    null,
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void Attribution_RejectsUndefinedAttributionKind(int value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    (AQGreenRecruitmentAttributionKind)value,
                    AQGreenAcquisitionSource.MemberInvitation,
                    Guid.NewGuid(),
                    AttributedAt,
                    null,
                    null,
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
        }

        [Fact]
        public void RootAttribution_RejectsSponsorMissingActorOrMissingReason()
        {
            Should.Throw<InvalidOperationException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1, Guid.NewGuid(), Guid.NewGuid(),
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    Guid.NewGuid(), AttributedAt, 42, "reason",
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
            Should.Throw<InvalidOperationException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1, Guid.NewGuid(), null,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    Guid.NewGuid(), AttributedAt, null, "reason",
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
            Should.Throw<InvalidOperationException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1, Guid.NewGuid(), null,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    Guid.NewGuid(), AttributedAt, 42, null,
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
        }

        [Fact]
        public void Attribution_RejectsKindAndAcquisitionSourceContradictions()
        {
            Should.Throw<InvalidOperationException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1, Guid.NewGuid(), Guid.NewGuid(),
                    AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    Guid.NewGuid(), AttributedAt, null, null,
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
            Should.Throw<InvalidOperationException>(() =>
                AQGreenRecruitmentAttribution.Create(
                    1, Guid.NewGuid(), null,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    AQGreenAcquisitionSource.MemberInvitation,
                    Guid.NewGuid(), AttributedAt, 42, "root evidence",
                    AQGreenRecruitmentAttributionRules.CurrentVersion));
        }

        [Fact]
        public void Attribution_RequiresValidIdentityTimeReferenceActorAndRulesVersion()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                CreateMemberAttribution(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tenantId: 0));
            Should.Throw<ArgumentException>(() =>
                CreateMemberAttribution(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
            Should.Throw<ArgumentException>(() =>
                CreateMemberAttribution(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
            Should.Throw<ArgumentException>(() =>
                CreateMemberAttribution(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
            Should.Throw<ArgumentException>(() =>
                CreateMemberAttribution(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                    attributedAt: DateTime.SpecifyKind(AttributedAt, DateTimeKind.Local)));
            Should.Throw<ArgumentOutOfRangeException>(() =>
                CreateMemberAttribution(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                    attributedByUserId: 0));
            Should.Throw<ArgumentException>(() =>
                CreateMemberAttribution(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                    rulesVersion: " \t "));
        }

        [Fact]
        public void Confirmation_PreservesMemberInvitationEvidenceReference()
        {
            var attribution = CreateMemberAttribution(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var evidenceId = Guid.NewGuid();

            var confirmation = AQGreenRecruitmentAttributionConfirmation.Confirm(
                attribution,
                AttributedAt.AddMinutes(1),
                77,
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                evidenceId,
                AQGreenRecruitmentAttributionRules.CurrentVersion);

            confirmation.TenantId.ShouldBe(attribution.TenantId);
            confirmation.AttributionId.ShouldBe(attribution.Id);
            confirmation.ConfirmationMethod.ShouldBe(
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance);
            confirmation.EvidenceReferenceId.ShouldBe(evidenceId);
            confirmation.ConfirmedByUserId.ShouldBe(77);
        }

        [Fact]
        public void Confirmation_CanConfirmProspectiveRootEvidenceWithoutPlacementClaim()
        {
            var root = AQGreenRecruitmentAttribution.Create(
                1, Guid.NewGuid(), null,
                AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                Guid.NewGuid(), AttributedAt, 42, "prospective root evidence",
                AQGreenRecruitmentAttributionRules.CurrentVersion);

            var confirmation = Confirm(
                root,
                method: AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation);

            confirmation.AttributionId.ShouldBe(root.Id);
            confirmation.ConfirmationMethod.ShouldBe(
                AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation);
        }

        [Fact]
        public void Confirmation_RejectsInvalidAttributionMethodTimeEvidenceActorAndRules()
        {
            var attribution = CreateMemberAttribution(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var root = AQGreenRecruitmentAttribution.Create(
                1, Guid.NewGuid(), null,
                AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                Guid.NewGuid(), AttributedAt, 42, "root",
                AQGreenRecruitmentAttributionRules.CurrentVersion);

            Should.Throw<ArgumentNullException>(() => Confirm(null));
            Should.Throw<ArgumentOutOfRangeException>(() =>
                Confirm(attribution, method: (AQGreenAttributionConfirmationMethod)3));
            Should.Throw<InvalidOperationException>(() => Confirm(root));
            Should.Throw<InvalidOperationException>(() =>
                Confirm(
                    attribution,
                    method: AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation));
            Should.Throw<ArgumentException>(() =>
                Confirm(attribution, confirmedAt: AttributedAt.AddTicks(-1)));
            Should.Throw<ArgumentException>(() =>
                Confirm(attribution, evidenceReferenceId: Guid.Empty));
            Should.Throw<ArgumentOutOfRangeException>(() =>
                Confirm(attribution, confirmedByUserId: 0));
            Should.Throw<ArgumentException>(() =>
                Confirm(attribution, rulesVersion: "\n\t"));
        }

        [Fact]
        public void AttributionAndConfirmation_HaveNoPublicMutationSurface()
        {
            AssertNoPublicSetters(
                typeof(AQGreenRecruitmentAttribution),
                "TenantId", "ParticipantId", "CreditedSponsorParticipantId",
                "AttributionKind", "AcquisitionSource", "SourceReferenceId", "AttributedAt",
                "AttributedByUserId", "AssignmentReason", "RulesVersion");
            AssertNoPublicSetters(
                typeof(AQGreenRecruitmentAttributionConfirmation),
                "TenantId", "AttributionId", "ConfirmedAt", "ConfirmedByUserId",
                "ConfirmationMethod", "EvidenceReferenceId", "RulesVersion");
        }

        private static AQGreenRecruitmentAttribution CreateMemberAttribution(
            Guid participantId,
            Guid sponsorId,
            Guid invitationId,
            int tenantId = 1,
            DateTime? attributedAt = null,
            long? attributedByUserId = null,
            string rulesVersion = AQGreenRecruitmentAttributionRules.CurrentVersion) =>
            AQGreenRecruitmentAttribution.Create(
                tenantId,
                participantId,
                sponsorId,
                AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                AQGreenAcquisitionSource.MemberInvitation,
                invitationId,
                attributedAt ?? AttributedAt,
                attributedByUserId,
                null,
                rulesVersion);

        private static AQGreenRecruitmentAttributionConfirmation Confirm(
            AQGreenRecruitmentAttribution attribution,
            DateTime? confirmedAt = null,
            long? confirmedByUserId = null,
            AQGreenAttributionConfirmationMethod method =
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
            Guid? evidenceReferenceId = null,
            string rulesVersion = AQGreenRecruitmentAttributionRules.CurrentVersion) =>
            AQGreenRecruitmentAttributionConfirmation.Confirm(
                attribution,
                confirmedAt ?? AttributedAt.AddMinutes(1),
                confirmedByUserId,
                method,
                evidenceReferenceId ?? Guid.NewGuid(),
                rulesVersion);

        private static void AssertNoPublicSetters(Type type, params string[] properties)
        {
            properties.All(property =>
                    type.GetProperty(property)?.GetSetMethod(nonPublic: false) == null)
                .ShouldBeTrue($"{type.Name} must not expose evidence mutation");
        }
    }
}
