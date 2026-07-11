using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.ObjectMapping;
using Abp.UI;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Application.Validation;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Facilitators
{
    [AbpAuthorize(PermissionNames.Pages_Facilitators)]
    public class FacilitatorAppService : AqualLifeStyleAppServiceBase, IFacilitatorAppService
    {
        private readonly IFacilitatorRepository _facilitatorRepository;
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IObjectMapper _objectMapper;

        public FacilitatorAppService(
            IFacilitatorRepository facilitatorRepository,
            IAreaLeaderRepository areaLeaderRepository,
            ICustomerRepository customerRepository,
            IUnitOfWorkManager unitOfWorkManager,
            IObjectMapper objectMapper)
        {
            _facilitatorRepository = facilitatorRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _customerRepository = customerRepository;
            _unitOfWorkManager = unitOfWorkManager;
            _objectMapper = objectMapper;
        }

        [AbpAuthorize(PermissionNames.Pages_Facilitators_Manage)]
        public async Task<FacilitatorDto> RegisterAsync(RegisterFacilitatorDto input)
        {
            AqualLifeStyleValidator.NotNull(input, nameof(input));
            AqualLifeStyleValidator.ValidId(input.CustomerId, nameof(input.CustomerId));
            AqualLifeStyleValidator.ValidId(input.AreaLeaderId, nameof(input.AreaLeaderId));

            var tenantId = GetRequiredTenantId("Facilitator registration failed.");

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = System.Transactions.IsolationLevel.Serializable
            }))
            {
                if (!await CustomerBelongsToCurrentTenantAsync(input.CustomerId))
                {
                    throw new AqualLifeStyleNotFoundException("Customer", input.CustomerId);
                }

                var existing = await _facilitatorRepository.GetByCustomerIdAsync(input.CustomerId, tenantId);
                if (existing != null)
                {
                    throw new UserFriendlyException("Facilitator registration failed.", "A facilitator for this customer already exists.");
                }

                var leader = await GetAreaLeaderForCurrentTenantAsync(input.AreaLeaderId);

                var facilitator = Facilitator.Register(tenantId, input.CustomerId, input.AreaLeaderId);
                await _facilitatorRepository.InsertAndGetIdAsync(facilitator);

                leader.RecordFacilitator();
                await _areaLeaderRepository.UpdateAsync(leader);

                await uow.CompleteAsync();

                return _objectMapper.Map<FacilitatorDto>(facilitator);
            }
        }

        public async Task<IReadOnlyList<FacilitatorDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Facilitator lookup failed.");
            var facilitators = await _facilitatorRepository.GetAllListAsync(f => f.TenantId == tenantId);
            return _objectMapper.Map<List<FacilitatorDto>>(facilitators);
        }

        public async Task<FacilitatorDto> GetAsync(int id)
        {
            AqualLifeStyleValidator.ValidId(id);
            var facilitator = await GetFacilitatorForCurrentTenantAsync(id);
            return _objectMapper.Map<FacilitatorDto>(facilitator);
        }

        public async Task<FacilitatorDto> GetByCustomerAsync(int customerId)
        {
            AqualLifeStyleValidator.ValidId(customerId, nameof(customerId));

            if (!await CustomerBelongsToCurrentTenantAsync(customerId))
            {
                return null;
            }

            var facilitator = await GetFacilitatorByCustomerForCurrentTenantAsync(customerId);
            return facilitator == null ? null : _objectMapper.Map<FacilitatorDto>(facilitator);
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

        private async Task<bool> CustomerBelongsToCurrentTenantAsync(int customerId)
        {
            var tenantId = GetRequiredTenantId("Facilitator lookup failed.");
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);
            return customer != null;
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
