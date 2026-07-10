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

namespace AqualLifeStyle.Application.AreaLeaders
{
    [AbpAuthorize(PermissionNames.Pages_AreaSpaces)]
    public class AreaSpaceAppService : AqualLifeStyleAppServiceBase, IAreaSpaceAppService
    {
        private readonly IAreaSpaceRepository _areaSpaceRepository;

        public AreaSpaceAppService(IAreaSpaceRepository areaSpaceRepository)
        {
            _areaSpaceRepository = areaSpaceRepository;
        }

        [AbpAuthorize(PermissionNames.Pages_AreaSpaces_Manage)]
        public async Task<AreaSpaceDto> ApplyAsync(CreateAreaSpaceDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.AreaLeaderId, nameof(input.AreaLeaderId));
            AqualLifeStyleValidator.NotNullOrEmpty(input.AddressLine, nameof(input.AddressLine));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Capacity, nameof(input.Capacity));
            if (input.InterestedMembers < 0)
            {
                throw new UserFriendlyException("Area space application failed.", "Interested members cannot be negative.");
            }

            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException("Area space application failed.", "A tenant context is required.");
            }

            var address = new Address(input.AddressLine, null, null, null);
            var space = AreaSpace.Apply(AbpSession.TenantId.Value, input.AreaLeaderId, address, input.Capacity, input.InterestedMembers);
            await _areaSpaceRepository.InsertAndGetIdAsync(space);
            return MapToDto(space);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaSpaces_Manage)]
        public async Task<AreaSpaceDto> StartReviewAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.StartReview();
            await _areaSpaceRepository.UpdateAsync(space);
            return MapToDto(space);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaSpaces_Manage)]
        public async Task<AreaSpaceDto> RecordPresentationAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.RecordPresentation();
            await _areaSpaceRepository.UpdateAsync(space);
            return MapToDto(space);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaSpaces_Manage)]
        public async Task<AreaSpaceDto> RecordStartupOrderAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.RecordStartupOrder();
            await _areaSpaceRepository.UpdateAsync(space);
            return MapToDto(space);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaSpaces_Manage)]
        public async Task<AreaSpaceDto> ApproveAsync(int id, DateTime? atUtc = null)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.Approve(atUtc);
            await _areaSpaceRepository.UpdateAsync(space);
            return MapToDto(space);
        }

        [AbpAuthorize(PermissionNames.Pages_AreaSpaces_Manage)]
        public async Task<AreaSpaceDto> SuspendAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.Suspend();
            await _areaSpaceRepository.UpdateAsync(space);
            return MapToDto(space);
        }

        public async Task<IReadOnlyList<AreaSpaceDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Area space lookup failed.");
            var spaces = await _areaSpaceRepository.GetAllListAsync(s => s.TenantId == tenantId);
            return spaces.Select(MapToDto).ToList();
        }

        public async Task<AreaSpaceDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            return MapToDto(space);
        }

        private int GetRequiredTenantId(string operation)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                throw new UserFriendlyException(operation, "A tenant context is required.");
            }

            return AbpSession.TenantId.Value;
        }

        private async Task<AreaSpace> GetSpaceForCurrentTenantAsync(int id)
        {
            var tenantId = GetRequiredTenantId("Area space lookup failed.");
            var space = await _areaSpaceRepository.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
            if (space == null)
            {
                throw new AqualLifeStyleNotFoundException("AreaSpace", id);
            }

            return space;
        }

        private static AreaSpaceDto MapToDto(AreaSpace space)
        {
            return new AreaSpaceDto
            {
                Id = space.Id,
                TenantId = space.TenantId,
                AreaLeaderId = space.AreaLeaderId,
                AddressLine = space.AddressLine,
                Capacity = space.Capacity,
                InterestedMembers = space.InterestedMembers,
                Status = (int)space.Status,
                ReviewStartedAt = space.ReviewStartedAt,
                PresentationsCompleted = space.PresentationsCompleted,
                StartupOrdersCompleted = space.StartupOrdersCompleted,
                ApprovedAt = space.ApprovedAt
            };
        }
    }
}
