using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Recruitment;

namespace AqualLifeStyle.Application.Recruitment
{
    public interface IAQGreenProspectiveJoiningRecorder
    {
        Task RecordAsync(EntryParticipation participation, ProgrammeInvitationPlacement invitation,
            long memberUserId, DateTime recordedAt);
        Task ValidateReplayAsync(EntryParticipation participation, string invitationCode);
    }

    public sealed class AQGreenProspectiveJoiningRecorder
        : IAQGreenProspectiveJoiningRecorder, ITransientDependency
    {
        private readonly IRepository<AQGreenRecruitmentAttribution, Guid> _attributions;
        private readonly IRepository<AQGreenRecruitmentAttributionConfirmation, Guid> _confirmations;
        private readonly IRepository<ProgrammeInvitation, Guid> _invitations;
        public AQGreenProspectiveJoiningRecorder(
            IRepository<AQGreenRecruitmentAttribution, Guid> attributions,
            IRepository<AQGreenRecruitmentAttributionConfirmation, Guid> confirmations,
            IRepository<ProgrammeInvitation, Guid> invitations)
        { _attributions = attributions; _confirmations = confirmations; _invitations = invitations; }

        public async Task RecordAsync(EntryParticipation participation, ProgrammeInvitationPlacement invitation,
            long memberUserId, DateTime recordedAt)
        {
            if (invitation?.InvitationId == null || invitation.RecruiterParticipationId == null ||
                invitation.TenantId != participation.TenantId ||
                invitation.RecruiterCustomerId != participation.RecruiterCustomerId)
                throw new InvalidOperationException("Exact authoritative invitation facts are required for V2 joining.");
            var attribution = AQGreenRecruitmentAttribution.Create(
                participation.TenantId, participation.Id, invitation.RecruiterParticipationId,
                AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                AQGreenAcquisitionSource.MemberInvitation, invitation.InvitationId.Value,
                recordedAt, memberUserId, null, AQGreenRecruitmentAttributionRules.CurrentVersion);
            await _attributions.InsertAsync(attribution);
            await _confirmations.InsertAsync(AQGreenRecruitmentAttributionConfirmation.Confirm(
                attribution, recordedAt, memberUserId,
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                invitation.InvitationId.Value, AQGreenRecruitmentAttributionRules.CurrentVersion));
        }

        public async Task ValidateReplayAsync(EntryParticipation participation, string invitationCode)
        {
            var attribution = await _attributions.FirstOrDefaultAsync(item =>
                item.TenantId == participation.TenantId && item.ParticipantId == participation.Id);
            if (attribution == null || attribution.AttributionKind != AQGreenRecruitmentAttributionKind.SponsoredParticipant)
                throw new InvalidOperationException("Joining cannot repair missing or substitute existing attribution.");
            var invitation = await _invitations.FirstOrDefaultAsync(attribution.SourceReferenceId);
            var confirmation = await _confirmations.FirstOrDefaultAsync(item => item.AttributionId == attribution.Id);
            if (invitation == null || invitation.TenantId != participation.TenantId ||
                invitation.ProgrammeParticipationId != attribution.CreditedSponsorParticipantId ||
                invitation.Code != ProgrammeInvitationResolver.NormalizeCode(invitationCode) ||
                confirmation == null || confirmation.TenantId != participation.TenantId ||
                confirmation.ConfirmationMethod != AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance ||
                confirmation.EvidenceReferenceId != invitation.Id)
                throw new InvalidOperationException("The joining replay conflicts with immutable invitation acceptance.");
        }
    }
}
