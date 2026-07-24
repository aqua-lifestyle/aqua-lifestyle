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
    }
}
