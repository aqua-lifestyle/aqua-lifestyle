using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface IClubMemberProgrammeProgressAppService
        : IApplicationService
    {
        Task<MyProgrammeJourneyDto> GetMyJourneyAsync();
        Task<MyProgrammeProgressDto> GetMyProgressAsync();
    }
}
