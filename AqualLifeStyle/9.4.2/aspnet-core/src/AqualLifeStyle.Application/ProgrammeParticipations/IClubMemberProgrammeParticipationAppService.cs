using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface IClubMemberProgrammeParticipationAppService : IApplicationService
    {
        Task<MyProgrammeParticipationsDto> GetMyParticipationsAsync();
        Task<ProgrammeParticipationDto> StartEntryAsync(StartEntryParticipationInput input);
        Task<ProgrammeCheckoutDto> CreateAQGreenJoiningCheckoutAsync(
            CreateAQGreenJoiningCheckoutInput input);
        Task<ProgrammeCheckoutDto> CreateDirectOnyxCheckoutAsync(CreateDirectOnyxCheckoutInput input);
    }
}
