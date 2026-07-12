using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.ObjectMapping;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships
{
    [AbpAuthorize(PermissionNames.Pages_Memberships)]
    public class MembershipAppService : AqualLifeStyleAppServiceBase, IMembershipAppService
    {
        private readonly IMembershipRepository _membershipRepository;
        private readonly IObjectMapper _objectMapper;

        public MembershipAppService(IMembershipRepository membershipRepository, IObjectMapper objectMapper)
        {
            _membershipRepository = membershipRepository;
            _objectMapper = objectMapper;
        }

        public async Task<IReadOnlyList<MembershipDto>> GetAllAsync()
        {
            var memberships = await _membershipRepository.GetAllListAsync();
            return _objectMapper.Map<List<MembershipDto>>(memberships);
        }

        public async Task<MembershipDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            
            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            return _objectMapper.Map<MembershipDto>(membership);
        }

        [AbpAuthorize(AquaPermissions.Members.Edit)]
        public async Task<MembershipDto> UpdateAsync(MembershipDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.Id);
            AqualLifeStyleValidator.NotNullOrEmpty(input.Name, nameof(input.Name));

            var membership = await _membershipRepository.GetAsync(input.Id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", input.Id);
            }

            membership.Rename(input.Name);
            membership.UpdateDescription(input.Description);
            membership.ChangeType(input.MembershipType);
            await _membershipRepository.UpdateAsync(membership);

            return _objectMapper.Map<MembershipDto>(membership);
        }

        [AbpAuthorize(AquaPermissions.Members.Create)]
        public async Task CreateAsync(CreateMembershipDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Name, nameof(input.Name));

            var tenantId = GetRequiredTenantId("Membership creation failed.");
            var membership = Membership.Create(tenantId, input.Name, input.Description, input.MembershipType);
            await _membershipRepository.InsertAsync(membership);
        }

        [AbpAuthorize(AquaPermissions.Members.Edit)]
        public async Task<MembershipDto> SetActivationDateAsync(int id, SetMembershipActivationDto input)
        {
            AqualLifeStyleValidator.ValidId(id);
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NotNullOrEmpty(input.ActivationDate, nameof(input.ActivationDate));

            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }
            
            if (!DateTime.TryParse(input.ActivationDate, out var activationDate))
            {
                throw new AqualLifeStyleValidationException(nameof(input.ActivationDate), "Invalid activation date format.");
            }

            membership.SetActivationDate(activationDate);
            await _membershipRepository.UpdateAsync(membership);

            return _objectMapper.Map<MembershipDto>(membership);
        }

        [AbpAuthorize(AquaPermissions.Members.Edit)]
        public async Task<MembershipDto> SetMonthlyObligationAsync(int id, SetMonthlyObligationDto input)
        {
            AqualLifeStyleValidator.ValidId(id);
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NonNegative(input.Amount, nameof(input.Amount));

            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            membership.SetMonthlyObligation(input.Amount);
            await _membershipRepository.UpdateAsync(membership);

            return _objectMapper.Map<MembershipDto>(membership);
        }

        [AbpAuthorize(AquaPermissions.Members.Edit)]
        public async Task<MembershipDto> MarkObligationMetAsync(int id, MarkObligationMetDto input)
        {
            AqualLifeStyleValidator.ValidId(id);
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NotNullOrEmpty(input.AsOfDate, nameof(input.AsOfDate));

            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            if (!DateTime.TryParse(input.AsOfDate, out var asOfDate))
            {
                throw new AqualLifeStyleValidationException(nameof(input.AsOfDate), "Invalid date format.");
            }

            membership.MarkObligationMet(asOfDate);
            await _membershipRepository.UpdateAsync(membership);

            return _objectMapper.Map<MembershipDto>(membership);
        }

        /// <summary>
        /// Get tier-specific benefits for a membership including order windows, discounts, and commission rates.
        /// </summary>
        public async Task<TierBenefitsDto> GetTierBenefitsAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            var benefits = membership.GetTierBenefits();
            return _objectMapper.Map<TierBenefitsDto>(benefits);
        }

        /// <summary>
        /// Get tier benefits for a specific membership type without requiring an instance.
        /// </summary>
        public TierBenefitsDto GetTierBenefitsByType(MembershipType membershipType)
        {
            var benefits = TierBenefits.ForTier(membershipType);
            return _objectMapper.Map<TierBenefitsDto>(benefits);
        }

        /// <summary>
        /// Check if it's currently within the order window for a membership.
        /// </summary>
        public async Task<bool> IsOrderWindowOpenAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            return membership.IsOrderWindowOpen();
        }

        /// <summary>
        /// Check if it's currently within the savings window for a membership.
        /// </summary>
        public async Task<bool> IsSavingsWindowOpenAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);

            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            return membership.IsSavingsWindowOpen();
        }

        /// <summary>
        /// Get read-only savings window status for each membership tier.
        /// </summary>
        public IReadOnlyList<SavingsWindowStatusDto> GetSavingsWindowStatuses(string asOfDate = null)
        {
            var date = ParseOptionalDate(asOfDate);

            return Enum.GetValues(typeof(MembershipType))
                .Cast<MembershipType>()
                .Select(membershipType => MapSavingsWindowStatusToDto(
                    TierBenefits.ForTier(membershipType),
                    date))
                .ToList();
        }

        private static DateTime ParseOptionalDate(string asOfDate)
        {
            if (string.IsNullOrWhiteSpace(asOfDate))
            {
                return DateTime.UtcNow.Date;
            }

            if (!DateTime.TryParse(asOfDate, out var parsedDate))
            {
                throw new AqualLifeStyleValidationException(nameof(asOfDate), "Invalid as-of date format.");
            }

            return parsedDate.Date;
        }

        private static SavingsWindowStatusDto MapSavingsWindowStatusToDto(TierBenefits benefits, DateTime date)
        {
            var isOpen = benefits.IsSavingsWindowOpen(date);

            return new SavingsWindowStatusDto
            {
                Tier = (int)benefits.Tier,
                TierName = benefits.TierName,
                SavingsWindowOpenDay = benefits.SavingsWindowOpenDay,
                SavingsWindowCloseDay = benefits.SavingsWindowCloseDay,
                CurrentDay = date.Day,
                AsOfDate = date.ToString("yyyy-MM-dd"),
                IsSavingsWindowOpen = isOpen,
                StatusLabel = isOpen ? "Open" : "Closed"
            };
        }
    }
}
