using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Recruitment.Dto;

namespace AqualLifeStyle.Application.Recruitment
{
    public interface IProgrammeInvitationAppService : IApplicationService
    {
        Task<MyProgrammeInvitationsDto> GetMyInvitationsAsync();
        Task<ProgrammeInvitationPreviewDto> GetPreviewAsync(ProgrammeInvitationCodeInput input);
    }
}
