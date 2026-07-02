using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.AutoMapper;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Domain.Memberships;

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
            var membership = await _membershipRepository.GetAsync(id);
            return MapToDto(membership);
        }

        public async Task<MembershipDto> UpdateAsync(MembershipDto input)
        {
            var membership = await _membershipRepository.GetAsync(input.Id);
            membership.Rename(input.Name);
            membership.UpdateDescription(input.Description);
            membership.ChangeType(input.MembershipType);
            await _membershipRepository.UpdateAsync(membership);

            return MapToDto(membership);
        }

        public async Task CreateAsync(CreateMembershipDto input)
        {
            var membership = Membership.Create(input.Name, input.Description, input.MembershipType);
            await _membershipRepository.InsertAsync(membership);
        }

        public async Task<MembershipDto> SetActivationDateAsync(int id, SetMembershipActivationDto input)
        {
            var membership = await _membershipRepository.GetAsync(id);
            
            if (!DateTime.TryParse(input.ActivationDate, out var activationDate))
            {
                throw new ArgumentException("Invalid activation date format.", nameof(input.ActivationDate));
            }

            membership.SetActivationDate(activationDate);
            await _membershipRepository.UpdateAsync(membership);

            return MapToDto(membership);
        }

        public async Task<MembershipDto> SetMonthlyObligationAsync(int id, SetMonthlyObligationDto input)
        {
            var membership = await _membershipRepository.GetAsync(id);
            membership.SetMonthlyObligation(input.Amount);
            await _membershipRepository.UpdateAsync(membership);

            return MapToDto(membership);
        }

        public async Task<MembershipDto> MarkObligationMetAsync(int id, MarkObligationMetDto input)
        {
            var membership = await _membershipRepository.GetAsync(id);

            if (!DateTime.TryParse(input.AsOfDate, out var asOfDate))
            {
                throw new ArgumentException("Invalid date format.", nameof(input.AsOfDate));
            }

            membership.MarkObligationMet(asOfDate);
            await _membershipRepository.UpdateAsync(membership);

            return MapToDto(membership);
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
    }
}
