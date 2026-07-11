using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.ObjectMapping;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Referrals.Dto;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Referrals
{
    [AbpAuthorize(PermissionNames.Pages_Referrals)]
    public class ReferralAppService : AqualLifeStyleAppServiceBase, IReferralAppService
    {
        private readonly IReferralRepository _referralRepository;
        private readonly IObjectMapper _objectMapper;

        public ReferralAppService(IReferralRepository referralRepository, IObjectMapper objectMapper)
        {
            _referralRepository = referralRepository;
            _objectMapper = objectMapper;
        }

        public async Task<IReadOnlyList<ReferralDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Referral lookup failed.");
            var referrals = await _referralRepository.GetAllListAsync(r => r.TenantId == tenantId);
            return _objectMapper.Map<List<ReferralDto>>(referrals);
        }

        public async Task<ReferralDto> GetByEnquiryAsync(int enquiryId)
        {
            AqualLifeStyleValidator.ValidId(enquiryId, nameof(enquiryId));
            var tenantId = GetRequiredTenantId("Referral lookup failed.");
            var referral = await _referralRepository.GetBySourceEnquiryAsync(enquiryId, tenantId);
            return referral == null ? null : _objectMapper.Map<ReferralDto>(referral);
        }

        [AbpAuthorize(PermissionNames.Pages_Referrals_Manage)]
        public async Task<ReferralDto> ConfirmAwardAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var tenantId = GetRequiredTenantId("Referral lookup failed.");
            var referral = await _referralRepository.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
            if (referral == null)
            {
                throw new AqualLifeStyleNotFoundException("Referral", id);
            }

            referral.ConfirmAward();
            await _referralRepository.UpdateAsync(referral);
            return _objectMapper.Map<ReferralDto>(referral);
        }

    }
}
