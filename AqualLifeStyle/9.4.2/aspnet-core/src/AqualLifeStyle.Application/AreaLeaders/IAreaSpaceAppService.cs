using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.AreaLeaders.Dto;

namespace AqualLifeStyle.Application.AreaLeaders
{
    public interface IAreaSpaceAppService : IApplicationService
    {
        Task<AreaSpaceDto> ApplyAsync(CreateAreaSpaceDto input);

        Task<AreaSpaceDto> StartReviewAsync(int id);

        Task<AreaSpaceDto> RecordPresentationAsync(int id);

        Task<AreaSpaceDto> RecordStartupOrderAsync(int id);

        Task<AreaSpaceDto> ApproveAsync(int id, DateTime? atUtc = null);

        Task<AreaSpaceDto> SuspendAsync(int id);

        Task<IReadOnlyList<AreaSpaceDto>> GetAllAsync();

        Task<AreaSpaceDto> GetAsync(int id);
    }
}
