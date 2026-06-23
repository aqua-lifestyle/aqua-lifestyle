using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.Memberships.Dto.Benefits;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Memberships
{
    [AbpAuthorize(PermissionNames.Pages_MembershipBenefits)]
    public class MembershipBenefitAppService : AqualLifeStyleAppServiceBase, IMembershipBenefitAppService
    {
        private readonly IMembershipBenefitRepository _benefitRepository;

        public MembershipBenefitAppService(IMembershipBenefitRepository benefitRepository)
        {
            _benefitRepository = benefitRepository;
        }

        public async Task<List<MembershipBenefitDto>> GetByMembershipTypeAsync(MembershipType membershipType)
        {
            var benefits = await _benefitRepository.GetByMembershipTypeAsync(membershipType);
            return benefits.Select(b => new MembershipBenefitDto
            {
                Id = b.Id,
                MembershipType = b.MembershipType,
                BenefitName = b.BenefitName,
                Description = b.Description,
                BenefitValue = b.BenefitValue,
                IsActive = b.IsActive
            }).ToList();
        }

        public async Task<MembershipBenefitDto> GetAsync(int id)
        {
            var benefit = await _benefitRepository.GetAsync(id);
            return new MembershipBenefitDto
            {
                Id = benefit.Id,
                MembershipType = benefit.MembershipType,
                BenefitName = benefit.BenefitName,
                Description = benefit.Description,
                BenefitValue = benefit.BenefitValue,
                IsActive = benefit.IsActive
            };
        }

        [AbpAuthorize(PermissionNames.Pages_MembershipBenefits_Manage)]
        public async Task<MembershipBenefitDto> CreateAsync(CreateMembershipBenefitDto input)
        {
            var benefit = MembershipBenefit.Create(
                input.MembershipType,
                input.BenefitName,
                input.Description,
                input.BenefitValue
            );

            await _benefitRepository.InsertAsync(benefit);

            return new MembershipBenefitDto
            {
                Id = benefit.Id,
                MembershipType = benefit.MembershipType,
                BenefitName = benefit.BenefitName,
                Description = benefit.Description,
                BenefitValue = benefit.BenefitValue,
                IsActive = benefit.IsActive
            };
        }

        [AbpAuthorize(PermissionNames.Pages_MembershipBenefits_Manage)]
        public async Task<MembershipBenefitDto> UpdateAsync(int id, MembershipBenefitDto input)
        {
            var benefit = await _benefitRepository.GetAsync(id);
            benefit.UpdateBenefit(input.BenefitName, input.Description, input.BenefitValue);
            await _benefitRepository.UpdateAsync(benefit);

            return new MembershipBenefitDto
            {
                Id = benefit.Id,
                MembershipType = benefit.MembershipType,
                BenefitName = benefit.BenefitName,
                Description = benefit.Description,
                BenefitValue = benefit.BenefitValue,
                IsActive = benefit.IsActive
            };
        }

        [AbpAuthorize(PermissionNames.Pages_MembershipBenefits_Manage)]
        public async Task DeleteAsync(int id)
        {
            await _benefitRepository.DeleteAsync(id);
        }
    }
}
