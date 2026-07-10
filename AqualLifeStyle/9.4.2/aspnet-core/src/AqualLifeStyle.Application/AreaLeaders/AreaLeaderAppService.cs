using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.UI;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.AreaLeaders
{
    public class AreaLeaderAppService : AqualLifeStyleAppServiceBase, IAreaLeaderAppService
    {
        private readonly IAreaLeaderRepository _areaLeaderRepository;

        public AreaLeaderAppService(IAreaLeaderRepository areaLeaderRepository)
        {
            _areaLeaderRepository = areaLeaderRepository;
        }

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
            await _areaLeaderRepository.InsertAsync(leader);
            return MapToDto(leader);
        }

        public async Task<IReadOnlyList<AreaLeaderDto>> GetAllAsync()
        {
            var leaders = await _areaLeaderRepository.GetAllListAsync();
            return leaders.Select(MapToDto).ToList();
        }

        public async Task<AreaLeaderDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await _areaLeaderRepository.GetAsync(id);
            return MapToDto(leader);
        }

        public async Task<AreaLeaderDto> RecordStartupOrderAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await _areaLeaderRepository.GetAsync(id);
            leader.RecordStartupOrder();
            await _areaLeaderRepository.UpdateAsync(leader);
            return MapToDto(leader);
        }

        public async Task<AreaLeaderDto> PromoteAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var leader = await _areaLeaderRepository.GetAsync(id);
            leader.PromoteToCurrentRank(new RankProgressionPolicy());
            await _areaLeaderRepository.UpdateAsync(leader);
            return MapToDto(leader);
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
