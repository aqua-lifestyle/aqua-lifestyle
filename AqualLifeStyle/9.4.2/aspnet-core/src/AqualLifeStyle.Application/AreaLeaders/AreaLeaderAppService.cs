using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.ObjectMapping;
using Abp.UI;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.AreaLeaders
{
    [AbpAuthorize(PermissionNames.Pages_AreaLeaders)]
    public class AreaLeaderAppService : AqualLifeStyleAppServiceBase, IAreaLeaderAppService
    {
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly IObjectMapper _objectMapper;

        public AreaLeaderAppService(IAreaLeaderRepository areaLeaderRepository, IObjectMapper objectMapper)
        {
            _areaLeaderRepository = areaLeaderRepository;
            _objectMapper = objectMapper;
        }

        [AbpAuthorize(PermissionNames.Pages_AreaLeaders_Manage)]
        public async Task<AreaLeaderDto> ApplyAsync(RegisterAreaLeaderDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.CustomerId, nameof(input.CustomerId));

            if (!Enum.IsDefined(typeof(LicenseType), input.LicenseType))
            {
                throw new UserFriendlyException("Area leader application failed.", "A valid license type is required.");
            }

            var tenantId = GetRequiredTenantId("Area leader application failed.");
            var existing = await _areaLeaderRepository.GetByCustomerIdAsync(input.CustomerId, tenantId);
            if (existing != null)
            {
                throw new UserFriendlyException("Area leader application failed.", "An area leader for this customer already exists.");
            }

            var activeLeaderCount = await _areaLeaderRepository.CountActiveAsync();
            if (activeLeaderCount >= AreaSpaceApprovalRules.MaxAreaLeaders)
            {
                throw new UserFriendlyException(
                    "Area leader application failed.",
                    $"The maximum number of active area leaders ({AreaSpaceApprovalRules.MaxAreaLeaders}) has been reached.");
            }

            var leader = AreaLeader.Apply(tenantId, input.CustomerId, (LicenseType)input.LicenseType);
            await _areaLeaderRepository.InsertAndGetIdAsync(leader);
            return _objectMapper.Map<AreaLeaderDto>(leader);
        }

        public async Task<IReadOnlyList<AreaLeaderDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Area leader lookup failed.");
            var leaders = await _areaLeaderRepository.GetAllListAsync(l => l.TenantId == tenantId);
            return _objectMapper.Map<List<AreaLeaderDto>>(leaders);
        }

        public async Task<AreaLeaderDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await GetLeaderForCurrentTenantAsync(id);
            return _objectMapper.Map<AreaLeaderDto>(leader);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaLeaders_Manage)]
        public async Task<AreaLeaderDto> RecordStartupOrderAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await GetLeaderForCurrentTenantAsync(id);
            leader.RecordStartupOrder();
            await _areaLeaderRepository.UpdateAsync(leader);
            return _objectMapper.Map<AreaLeaderDto>(leader);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaLeaders_Manage)]
        public async Task<AreaLeaderDto> PromoteAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await GetLeaderForCurrentTenantAsync(id);
            leader.PromoteToCurrentRank(new RankProgressionPolicy());
            await _areaLeaderRepository.UpdateAsync(leader);
            return _objectMapper.Map<AreaLeaderDto>(leader);
        }

        private async Task<AreaLeader> GetLeaderForCurrentTenantAsync(int id)
        {
            var tenantId = GetRequiredTenantId("Area leader lookup failed.");
            var leader = await _areaLeaderRepository.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
            if (leader == null)
            {
                throw new AqualLifeStyleNotFoundException("AreaLeader", id);
            }

            return leader;
        }

    }
}
