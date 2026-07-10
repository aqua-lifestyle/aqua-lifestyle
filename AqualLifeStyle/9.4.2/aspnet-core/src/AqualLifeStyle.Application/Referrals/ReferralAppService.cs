using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.UI;
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

        public ReferralAppService(IReferralRepository referralRepository)
        {
            _referralRepository = referralRepository;
        }

        public async Task<IReadOnlyList<ReferralDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Referral lookup failed.");
            var referrals = await _referralRepository.GetAllListAsync(r => r.TenantId == tenantId);
            return referrals.Select(MapToDto).ToList();
        }

        public async Task<ReferralDto> GetByEnquiryAsync(int enquiryId)
        {
            AqualLifeStyleValidator.ValidId(enquiryId, nameof(enquiryId));
            var referral = await _referralRepository.GetBySourceEnquiryAsync(enquiryId);
            return referral == null ? null : MapToDto(referral);
        }

        [AbpAuthorize(PermissionNames.Pages_Referrals_Manage)]
        public async Task<ReferralDto> ConfirmAwardAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var referral = await _referralRepository.GetAsync(id);
            referral.ConfirmAward();
            await _referralRepository.UpdateAsync(referral);
            return MapToDto(referral);
        }

        private int GetRequiredTenantId(string operation)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException(operation, "A tenant context is required.");
            }

            return AbpSession.TenantId.Value;
        }

        private static ReferralDto MapToDto(Referral referral)
        {
            return new ReferralDto
            {
                Id = referral.Id,
                TenantId = referral.TenantId,
                ReferrerFacilitatorId = referral.ReferrerFacilitatorId,
                ReferrerAreaLeaderId = referral.ReferrerAreaLeaderId,
                ReferredCustomerId = referral.ReferredCustomerId,
                SourceEnquiryId = referral.SourceEnquiryId,
                Type = (int)referral.Type,
                AwardAmount = referral.AwardAmount,
                AwardIssued = referral.AwardIssued,
                ConfirmedAt = referral.ConfirmedAt,
                ConvertedAt = referral.ConvertedAt
            };
        }
    }
}
