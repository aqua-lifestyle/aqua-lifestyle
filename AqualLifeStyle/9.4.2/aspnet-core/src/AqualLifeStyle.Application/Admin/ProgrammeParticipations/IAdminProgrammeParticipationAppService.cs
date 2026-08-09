using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public interface IAdminProgrammeParticipationAppService : IApplicationService
    {
        Task<PagedResultDto<AdminProgrammeParticipationDto>> GetAllAsync(
            AdminProgrammeParticipationListInput input);
        Task<PendingProgrammeApprovalSummaryDto> GetPendingApprovalSummaryAsync(
            PendingProgrammeApprovalSummaryInput input);
        Task CorrectRecruiterAsync(CorrectProgrammeRecruiterInput input);
        Task<OnyxGraduationDecisionDto> GraduateAQGreenToOnyxAsync(
            GraduateAQGreenToOnyxInput input);
        Task TerminateAQGreenJoiningCheckoutAsync(
            TerminateAQGreenJoiningCheckoutInput input);
        Task<PagedResultDto<AQGreenJoiningCheckoutRecoveryDto>>
            GetAQGreenJoiningCheckoutsAsync(AQGreenJoiningCheckoutListInput input);
        Task<PagedResultDto<LegacyAQGreenReconciliationDto>>
            GetLegacyAQGreenReconciliationAsync(
                LegacyAQGreenReconciliationListInput input);
        Task ApproveProgrammeParticipationAsync(
            ApproveProgrammeParticipationInput input);
        Task RejectProgrammeParticipationAsync(
            RejectProgrammeParticipationInput input);
    }
}
