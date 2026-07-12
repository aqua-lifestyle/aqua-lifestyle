using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Referrals.Dto;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Referrals
{
    public interface IReferralAppService : IApplicationService
    {
        Task<IReadOnlyList<ReferralDto>> GetAllAsync();

        Task<ReferralDto> GetByEnquiryAsync(int enquiryId);

        Task<ReferralDto> ConfirmAwardAsync(int id);
    }
}
