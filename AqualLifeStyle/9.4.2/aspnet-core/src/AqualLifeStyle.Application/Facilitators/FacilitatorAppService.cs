using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Facilitators
{
    [AbpAuthorize(PermissionNames.Pages_Facilitators)]
    public class FacilitatorAppService : AqualLifeStyleAppServiceBase, IFacilitatorAppService
    {
        private readonly IFacilitatorRepository _facilitatorRepository;
        private readonly IAreaLeaderRepository _areaLeaderRepository;

        public FacilitatorAppService(IFacilitatorRepository facilitatorRepository, IAreaLeaderRepository areaLeaderRepository)
        {
            _facilitatorRepository = facilitatorRepository;
            _areaLeaderRepository = areaLeaderRepository;
        }

        [AbpAuthorize(PermissionNames.Pages_Facilitators_Manage)]
        public async Task<FacilitatorDto> RegisterAsync(RegisterFacilitatorDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.CustomerId, nameof(input.CustomerId));
            AqualLifeStyleValidator.ValidId(input.AreaLeaderId, nameof(input.AreaLeaderId));

            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException("Facilitator registration failed.", "A tenant context is required.");
            }

            var facilitator = Facilitator.Register(AbpSession.TenantId.Value, input.CustomerId, input.AreaLeaderId);
            await _facilitatorRepository.InsertAndGetIdAsync(facilitator);

            var leader = await _areaLeaderRepository.FirstOrDefaultAsync(l => l.Id == input.AreaLeaderId);
            if (leader != null)
            {
                leader.RecordFacilitator();
                await _areaLeaderRepository.UpdateAsync(leader);
            }

            return MapToDto(facilitator);
        }

        public async Task<IReadOnlyList<FacilitatorDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Facilitator lookup failed.");
            var facilitators = await _facilitatorRepository.GetAllListAsync(f => f.TenantId == tenantId);
            return facilitators.Select(MapToDto).ToList();
        }

        public async Task<FacilitatorDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var facilitator = await GetFacilitatorForCurrentTenantAsync(id);
            return MapToDto(facilitator);
        }

        public async Task<FacilitatorDto> GetByCustomerAsync(int customerId)
        {
            AqualLifeStyleValidator.ValidId(customerId, nameof(customerId));
            var facilitator = await GetFacilitatorByCustomerForCurrentTenantAsync(customerId);
            return facilitator == null ? null : MapToDto(facilitator);
        }

        private int GetRequiredTenantId(string operation)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException(operation, "A tenant context is required.");
            }

            return AbpSession.TenantId.Value;
        }

        private async Task<Facilitator> GetFacilitatorForCurrentTenantAsync(int id)
        {
            var tenantId = GetRequiredTenantId("Facilitator lookup failed.");
            var facilitator = await _facilitatorRepository.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);
            if (facilitator == null)
            {
                throw new AqualLifeStyleNotFoundException("Facilitator", id);
            }

            return facilitator;
        }

        private async Task<Facilitator> GetFacilitatorByCustomerForCurrentTenantAsync(int customerId)
        {
            var tenantId = GetRequiredTenantId("Facilitator lookup failed.");
            return await _facilitatorRepository.FirstOrDefaultAsync(f => f.CustomerId == customerId && f.TenantId == tenantId);
        }

        private static FacilitatorDto MapToDto(Facilitator facilitator)
        {
            return new FacilitatorDto
            {
                Id = facilitator.Id,
                TenantId = facilitator.TenantId,
                CustomerId = facilitator.CustomerId,
                AreaLeaderId = facilitator.AreaLeaderId,
                Rank = (int)facilitator.Rank,
                DirectReferrals = facilitator.DirectReferrals,
                IndirectReferrals = facilitator.IndirectReferrals,
                AwardBalance = facilitator.AwardBalance
            };
        }
    }
}
