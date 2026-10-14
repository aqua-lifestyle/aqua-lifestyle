using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    /// <summary>
    /// Records the authenticated member's explicit invitation acceptance inside
    /// joining's transaction. Never reconstructs historical acceptance on retry.
    /// Invitation resolution already holds the ordered customer transition locks.
    /// </summary>
    public sealed class AQGreenInvitationAttributionRecorder : ITransientDependency
    {
        private readonly IRepository<AQGreenRecruitmentAttribution, Guid> _attributions;
        private readonly IRepository<AQGreenRecruitmentAttributionConfirmation, Guid> _confirmations;
        private readonly IRepository<AQGreenNetworkPlacement, Guid> _placements;
        private readonly IRepository<AQGreenPlacementTreeScope, Guid> _scopes;

        public AQGreenInvitationAttributionRecorder(
            IRepository<AQGreenRecruitmentAttribution, Guid> attributions,
            IRepository<AQGreenRecruitmentAttributionConfirmation, Guid> confirmations,
            IRepository<AQGreenNetworkPlacement, Guid> placements,
            IRepository<AQGreenPlacementTreeScope, Guid> scopes)
        {
            _attributions = attributions;
            _confirmations = confirmations;
            _placements = placements;
            _scopes = scopes;
        }

        public async Task RecordAsync(
            EntryParticipation participant,
            ProgrammeInvitationPlacement invitation,
            long memberUserId,
            DateTime acceptedAt,
            bool isNew)
        {
            if (invitation == null || invitation.TenantId != participant.TenantId ||
                invitation.RecruiterCustomerId != participant.RecruiterCustomerId)
                throw new InvalidOperationException("V2 joining requires matching invitation evidence.");

            var sponsorPlacement = await _placements.GetAll().AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == participant.TenantId &&
                    item.ParticipantId == invitation.SponsorParticipantId);
            if (sponsorPlacement == null || !await _scopes.GetAll().AnyAsync(item =>
                    item.TenantId == participant.TenantId &&
                    item.Id == sponsorPlacement.PlacementTreeScopeId))
                throw new InvalidOperationException("V2 joining requires an established sponsor placement scope.");

            if (!isNew)
            {
                var existing = await _attributions.GetAll().AsNoTracking()
                    .SingleOrDefaultAsync(item => item.TenantId == participant.TenantId &&
                        item.ParticipantId == participant.Id);
                if (existing == null || existing.SourceReferenceId != invitation.InvitationId ||
                    existing.CreditedSponsorParticipantId != invitation.SponsorParticipantId ||
                    existing.AttributionKind != AQGreenRecruitmentAttributionKind.SponsoredParticipant ||
                    existing.AcquisitionSource != AQGreenAcquisitionSource.MemberInvitation)
                    throw new InvalidOperationException("Existing V2 joining attribution is missing or conflicts with acceptance.");
                var confirmation = await _confirmations.GetAll().AsNoTracking()
                    .SingleOrDefaultAsync(item => item.TenantId == participant.TenantId &&
                        item.AttributionId == existing.Id);
                if (confirmation == null || confirmation.ConfirmedByUserId != memberUserId ||
                    confirmation.EvidenceReferenceId != invitation.InvitationId ||
                    confirmation.ConfirmationMethod != AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance)
                    throw new InvalidOperationException("Existing V2 invitation acceptance evidence is missing or conflicts.");
                return;
            }

            var attribution = AQGreenRecruitmentAttribution.Create(
                participant.TenantId, participant.Id, invitation.SponsorParticipantId,
                AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                AQGreenAcquisitionSource.MemberInvitation, invitation.InvitationId,
                acceptedAt, memberUserId, null, AQGreenRecruitmentAttributionRules.CurrentVersion);
            await _attributions.InsertAsync(attribution);
            await _confirmations.InsertAsync(AQGreenRecruitmentAttributionConfirmation.Confirm(
                attribution, acceptedAt, memberUserId,
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                invitation.InvitationId, AQGreenRecruitmentAttributionRules.CurrentVersion));
        }
    }
}
