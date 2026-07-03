using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.AutoMapper;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Memberships
{
    public class MembershipAppService : AqualLifeStyleAppServiceBase, IMembershipAppService
    {
        private readonly IMembershipRepository _membershipRepository;

        public MembershipAppService(IMembershipRepository membershipRepository)
        {
            _membershipRepository = membershipRepository;
        }

        public async Task<IReadOnlyList<MembershipDto>> GetAllAsync()
        {
            var memberships = await _membershipRepository.GetAllListAsync();
            return memberships.Select(MapToDto).ToList();
        }

        public async Task<MembershipDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            
            var membership = await _membershipRepository.GetAsync(id);
            if (membership == null)
            {
                throw new AqualLifeStyleNotFoundException("Membership", id);
            }

            return MapToDto(membership);
        }

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

            return MapToDto(membership);
        }

        public async Task CreateAsync(CreateMembershipDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Name, nameof(input.Name));

            var membership = Membership.Create(input.Name, input.Description, input.MembershipType);
            await _membershipRepository.InsertAsync(membership);
        }

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

            return MapToDto(membership);
        }

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

            return MapToDto(membership);
        }

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

            return MapToDto(membership);
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
            return MapTierBenefitsToDto(benefits);
        }

        /// <summary>
        /// Get tier benefits for a specific membership type without requiring an instance.
        /// </summary>
        public TierBenefitsDto GetTierBenefitsByType(MembershipType membershipType)
        {
            var benefits = TierBenefits.ForTier(membershipType);
            return MapTierBenefitsToDto(benefits);
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

        private static MembershipDto MapToDto(Membership membership)
        {
            return new MembershipDto
            {
                Id = membership.Id,
                Name = membership.Name,
                Description = membership.Description,
                IsActive = membership.IsActive,
                MembershipType = membership.MembershipType,
                ActivationDate = membership.ActivationDate?.ToString("u"),
                MonthlyObligationAmount = membership.MonthlyObligationAmount,
                LastObligationMetDate = membership.LastObligationMetDate?.ToString("u")
            };
        }

        private static TierBenefitsDto MapTierBenefitsToDto(TierBenefits benefits)
        {
            return new TierBenefitsDto
            {
                Tier = (int)benefits.Tier,
                TierName = benefits.TierName,
                MonthlyObligation = benefits.MonthlyObligation,
                OrderWindowStartDay = benefits.OrderWindowStartDay,
                OrderWindowEndDay = benefits.OrderWindowEndDay,
                SavingsWindowOpenDay = benefits.SavingsWindowOpenDay,
                SavingsWindowCloseDay = benefits.SavingsWindowCloseDay,
                ProductPricingDiscount = benefits.ProductPricingDiscount,
                InterestRate = benefits.InterestRate,
                MaxConcurrentOrders = benefits.MaxConcurrentOrders,
                ReferralCommissionRate = benefits.ReferralCommissionRate,
                ProfitSharePercentage = benefits.ProfitSharePercentage,
                IsOrderWindowOpen = benefits.IsOrderWindowOpen(),
                IsSavingsWindowOpen = benefits.IsSavingsWindowOpen()
            };
        }
    }
}
