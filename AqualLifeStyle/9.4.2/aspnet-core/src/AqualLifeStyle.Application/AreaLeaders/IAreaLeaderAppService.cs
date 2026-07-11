using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Application.AreaLeaders.Dto;

namespace AqualLifeStyle.Application.AreaLeaders
{
    public interface IAreaLeaderAppService : IApplicationService
    {
        Task<AreaLeaderDto> ApplyAsync(RegisterAreaLeaderDto input);

        Task<IReadOnlyList<AreaLeaderDto>> GetAllAsync();

        Task<AreaLeaderDto> GetAsync(int id);

        Task<AreaLeaderDto> RecordStartupOrderAsync(int id);

        Task<AreaLeaderDto> PromoteAsync(int id);
    }
}
