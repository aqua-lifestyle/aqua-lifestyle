using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Events.Bus;
using Abp.ObjectMapping;
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
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly IObjectMapper _objectMapper;
        private readonly IEventBus _eventBus;

        public AreaSpaceAppService(
            IAreaSpaceRepository areaSpaceRepository,
            IAreaLeaderRepository areaLeaderRepository,
            IObjectMapper objectMapper,
            IEventBus eventBus)
        {
            _areaSpaceRepository = areaSpaceRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _objectMapper = objectMapper;
            _eventBus = eventBus;
        }

        [AbpAuthorize(AquaPermissions.AreaSpaces.Apply)]
        public async Task<AreaSpaceDto> ApplyAsync(CreateAreaSpaceDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.AreaLeaderId, nameof(input.AreaLeaderId));
            AqualLifeStyleValidator.NotNullOrEmpty(input.AddressLine, nameof(input.AddressLine));
            AqualLifeStyleValidator.NotNullOrEmpty(input.Capacity, nameof(input.Capacity));
            var tenantId = GetRequiredTenantId("Area space application failed.");
            if (input.InterestedMembers < 0)
            {
                throw new UserFriendlyException("Area space application failed.", "Interested members cannot be negative.");
            }

            await GetAreaLeaderForCurrentTenantAsync(input.AreaLeaderId);

            var address = new Address(input.AddressLine, null, null, null);
            var space = AreaSpace.Apply(tenantId, input.AreaLeaderId, address, input.Capacity, input.InterestedMembers);
            await _areaSpaceRepository.InsertAndGetIdAsync(space);
            return _objectMapper.Map<AreaSpaceDto>(space);
        }

        [AbpAuthorize(AquaPermissions.AreaSpaces.Manage)]
        public async Task<AreaSpaceDto> StartReviewAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.StartReview();
            await _areaSpaceRepository.UpdateAsync(space);
            return _objectMapper.Map<AreaSpaceDto>(space);
        }

        [AbpAuthorize(AquaPermissions.AreaSpaces.Manage)]
        public async Task<AreaSpaceDto> RecordPresentationAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.RecordPresentation();
            await _areaSpaceRepository.UpdateAsync(space);
            return _objectMapper.Map<AreaSpaceDto>(space);
        }

        [AbpAuthorize(AquaPermissions.Orders.Process)]
        public async Task<AreaSpaceDto> RecordStartupOrderAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.RecordStartupOrder();
            await _areaSpaceRepository.UpdateAsync(space);
            return _objectMapper.Map<AreaSpaceDto>(space);
        }

        [AbpAuthorize(AquaPermissions.AreaSpaces.Approve)]
        public async Task<AreaSpaceDto> ApproveAsync(int id, DateTime? atUtc = null)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            var shouldPublishApprovedEvent = space.Status != AreaSpaceStatus.Approved;
            space.Approve(atUtc);
            await _areaSpaceRepository.UpdateAsync(space);

            if (shouldPublishApprovedEvent && _eventBus != null)
            {
                var approvedEvent = new AreaSpaceApprovedEvent(space.TenantId, space.Id, space.AreaLeaderId);
                var currentUow = CurrentUnitOfWork;
                if (currentUow != null)
                {
                    EventHandler completedHandler = null;
                    completedHandler = (sender, args) =>
                    {
                        currentUow.Completed -= completedHandler;
                        _eventBus.Trigger(approvedEvent);
                    };

                    currentUow.Completed += completedHandler;
                }
                else
                {
                    _eventBus.Trigger(approvedEvent);
                }
            }

            return _objectMapper.Map<AreaSpaceDto>(space);
        }

        [AbpAuthorize(AquaPermissions.AreaSpaces.Manage)]
        public async Task<AreaSpaceDto> SuspendAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            space.Suspend();
            await _areaSpaceRepository.UpdateAsync(space);
            return _objectMapper.Map<AreaSpaceDto>(space);
        }

        public async Task<IReadOnlyList<AreaSpaceDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Area space lookup failed.");
            var spaces = await _areaSpaceRepository.GetAllListAsync(s => s.TenantId == tenantId);
            return _objectMapper.Map<List<AreaSpaceDto>>(spaces);
        }

        public async Task<AreaSpaceDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var space = await GetSpaceForCurrentTenantAsync(id);
            return _objectMapper.Map<AreaSpaceDto>(space);
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

        private async Task<AreaLeader> GetAreaLeaderForCurrentTenantAsync(int id)
        {
            var tenantId = GetRequiredTenantId("Area leader lookup failed.");
            var leader = await _areaLeaderRepository.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
            if (leader == null)
            {
                throw new AqualLifeStyleNotFoundException("AreaLeader", id);
            }

            return leader;
        }

    }
}
