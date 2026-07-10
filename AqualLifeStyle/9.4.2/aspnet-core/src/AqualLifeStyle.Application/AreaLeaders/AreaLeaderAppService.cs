using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
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

        public AreaLeaderAppService(IAreaLeaderRepository areaLeaderRepository)
        {
            _areaLeaderRepository = areaLeaderRepository;
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

            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException("Area leader application failed.", "A tenant context is required.");
            }

            var leader = AreaLeader.Apply(AbpSession.TenantId.Value, input.CustomerId, (LicenseType)input.LicenseType);
            await _areaLeaderRepository.InsertAndGetIdAsync(leader);
            return MapToDto(leader);
        }

        public async Task<IReadOnlyList<AreaLeaderDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Area leader lookup failed.");
            var leaders = await _areaLeaderRepository.GetAllListAsync(l => l.TenantId == tenantId);
            return leaders.Select(MapToDto).ToList();
        }

        public async Task<AreaLeaderDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await GetLeaderForCurrentTenantAsync(id);
            return MapToDto(leader);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaLeaders_Manage)]
        public async Task<AreaLeaderDto> RecordStartupOrderAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await GetLeaderForCurrentTenantAsync(id);
            leader.RecordStartupOrder();
            await _areaLeaderRepository.UpdateAsync(leader);
            return MapToDto(leader);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaLeaders_Manage)]
        public async Task<AreaLeaderDto> PromoteAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await GetLeaderForCurrentTenantAsync(id);
            leader.PromoteToCurrentRank(new RankProgressionPolicy());
            await _areaLeaderRepository.UpdateAsync(leader);
            return MapToDto(leader);
        }

        private int GetRequiredTenantId(string operation)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException(operation, "A tenant context is required.");
            }

            return AbpSession.TenantId.Value;
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

        private static AreaLeaderDto MapToDto(AreaLeader leader)
        {
            return new AreaLeaderDto
            {
                Id = leader.Id,
                TenantId = leader.TenantId,
                CustomerId = leader.CustomerId,
                LicenseType = (int)leader.LicenseType,
                LicenseFee = leader.LicenseFee,
                Rank = (int)leader.Rank,
                AreaSpaceId = leader.AreaSpaceId,
                MonthlySubscription = leader.MonthlySubscription,
                DirectReferrals = leader.DirectReferrals,
                IndirectReferrals = leader.IndirectReferrals,
                OrderTarget = leader.OrderTarget
            };
        }
    }
}
