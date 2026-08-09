using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.Admin.Tenants.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Tenants
{
    [Audited]
    public class AdminTenantAppService : AdminAppServiceBase, IAdminTenantAppService
    {
        private readonly IRepository<Tenant, int> _tenantRepository;
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ITenantAccountProvisioner _tenantAccountProvisioner;
        private readonly IRepository<AreaActivationStateRecord, Guid>
            _activationStateRepository;
        private readonly IAreaActivationStateRecorder _activationStateRecorder;
        private readonly IAreaActivationStateLock _activationStateLock;

        public AdminTenantAppService(IRepository<Tenant, int> tenantRepository,
            IAreaLeaderRepository areaLeaderRepository, ICustomerRepository customerRepository,
            ITenantAccountProvisioner tenantAccountProvisioner,
            IRepository<AreaActivationStateRecord, Guid> activationStateRepository,
            IAreaActivationStateRecorder activationStateRecorder,
            IAreaActivationStateLock activationStateLock)
        {
            _tenantRepository = tenantRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _customerRepository = customerRepository;
            _tenantAccountProvisioner = tenantAccountProvisioner;
            _activationStateRepository = activationStateRepository;
            _activationStateRecorder = activationStateRecorder;
            _activationStateLock = activationStateLock;
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.View)]
        public async Task<PagedResultDto<AdminTenantDto>> GetAllAsync(AdminTenantListInput input)
        {
            input ??= new AdminTenantListInput();
            var query = _tenantRepository.GetAll();
            if (input.IsActive.HasValue) query = query.Where(tenant => tenant.IsActive == input.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var keyword = input.Keyword.Trim().ToLower();
                query = query.Where(tenant => tenant.Name.ToLower().Contains(keyword) || tenant.TenancyName.ToLower().Contains(keyword));
            }
            var total = await query.CountAsync();
            var tenants = await query.OrderBy(tenant => tenant.Name).Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
            return new PagedResultDto<AdminTenantDto>(total, await MapAsync(tenants));
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.View)]
        public async Task<AdminTenantDto> GetAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Tenant");
            return (await MapAsync(new[] { await GetTenantAsync(input.Id) })).Single();
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.Create)]
        public async Task<AdminTenantDto> CreateAsync(CreateAdminTenantInput input)
        {
            if (input == null) throw Failed("Tenant creation", "The request body was empty.");
            var tenant = await _tenantAccountProvisioner.ProvisionAsync(new TenantProvisioningRequest
            {
                TenancyName = input.TenancyName, Name = input.Name, AdminEmailAddress = input.AdminEmailAddress,
                ConnectionString = input.ConnectionString, IsActive = input.IsActive,
                Justification = input.Justification
            });
            LogAdminMutation("Tenant", "created", tenant.Id, tenant.Id, input.Justification);
            return (await MapAsync(new[] { tenant })).Single();
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.Edit)]
        public async Task<AdminTenantDto> EditAsync(EditAdminTenantInput input)
        {
            if (input == null) throw Failed("Tenant update", "The request body was empty.");
            ValidatePositiveId(input.Id, "Tenant");
            var tenant = await GetTenantAsync(input.Id);
            tenant.Name = input.Name.Trim();
            tenant.TenancyName = input.TenancyName.Trim();
            await _tenantRepository.UpdateAsync(tenant);
            LogAdminMutation("Tenant", "profile updated", tenant.Id, tenant.Id, input.Justification);
            return (await MapAsync(new[] { tenant })).Single();
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.Activate)]
        public async Task<AdminTenantDto> SetActivationAsync(SetTenantActivationInput input)
        {
            if (input == null) throw Failed("Tenant activation update", "The request body was empty.");
            ValidatePositiveId(input.Id, "Tenant");
            var effectiveAt = await _activationStateLock.AcquireAsync(input.Id);
            var tenant = await GetTenantAsync(input.Id);
            if (tenant.IsActive != input.IsActive)
            {
                tenant.IsActive = input.IsActive;
                await _tenantRepository.UpdateAsync(tenant);
                await _activationStateRecorder.RecordAsync(
                    tenant.Id,
                    tenant.IsActive,
                    AreaActivationStateRecordKind.Changed,
                    input.Justification,
                    effectiveAt);
            }
            else if (!await _activationStateRecorder.HasHistoryAsync(tenant.Id))
            {
                await _activationStateRecorder.RecordAsync(
                    tenant.Id,
                    tenant.IsActive,
                    AreaActivationStateRecordKind.ObservedBaseline,
                    input.Justification,
                    effectiveAt);
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            LogAdminMutation("Tenant", input.IsActive ? "activated" : "deactivated", tenant.Id, tenant.Id, input.Justification);
            return (await MapAsync(new[] { tenant })).Single();
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.Activate)]
        public async Task<AdminTenantDto> ObserveActivationStateAsync(
            ObserveTenantActivationStateInput input)
        {
            if (input == null)
            {
                throw Failed(
                    "Tenant activation observation",
                    "The request body was empty.");
            }

            ValidatePositiveId(input.Id, "Tenant");
            var effectiveAt = await _activationStateLock.AcquireAsync(input.Id);
            var tenant = await GetTenantAsync(input.Id);
            if (!await _activationStateRecorder.HasHistoryAsync(tenant.Id))
            {
                await _activationStateRecorder.RecordAsync(
                    tenant.Id,
                    tenant.IsActive,
                    AreaActivationStateRecordKind.ObservedBaseline,
                    input.Justification,
                    effectiveAt);
                await CurrentUnitOfWork.SaveChangesAsync();
                LogAdminMutation(
                    "Tenant",
                    "activation state observed",
                    tenant.Id,
                    tenant.Id,
                    input.Justification);
            }

            return (await MapAsync(new[] { tenant })).Single();
        }

        [AbpAuthorize(AquaPermissions.Admin.Tenants.AssignLeader)]
        public async Task<AdminTenantDto> AssignAreaLeaderAsync(AssignTenantAreaLeaderInput input)
        {
            if (input == null) throw Failed("Tenant leader assignment", "The request body was empty.");
            ValidatePositiveId(input.Id, "Tenant");
            ValidatePositiveId(input.AreaLeaderId, "Area leader");
            var tenant = await GetTenantAsync(input.Id);
            using (DisableTenantFilterForHost())
            {
                var leader = await _areaLeaderRepository.FirstOrDefaultAsync(candidate => candidate.Id == input.AreaLeaderId &&
                    candidate.TenantId == tenant.Id && candidate.IsApproved);
                if (leader == null) throw Failed("Tenant leader assignment", "The approved area leader was not found in this tenant.");
            }
            tenant.AssignAreaLeader(input.AreaLeaderId);
            await _tenantRepository.UpdateAsync(tenant);
            LogAdminMutation("Tenant", $"area leader {input.AreaLeaderId} assigned", tenant.Id, tenant.Id, input.Justification);
            return (await MapAsync(new[] { tenant })).Single();
        }

        private async Task<Tenant> GetTenantAsync(int id)
        {
            var tenant = await _tenantRepository.FirstOrDefaultAsync(id);
            if (tenant == null) throw Failed("Tenant lookup", "The tenant was not found.");
            return tenant;
        }

        private async Task<List<AdminTenantDto>> MapAsync(IEnumerable<Tenant> tenantSequence)
        {
            var tenants = tenantSequence.ToList();
            var tenantIds = tenants.Select(tenant => tenant.Id).ToArray();
            var activationHistoryStarts = tenantIds.Length == 0
                ? new Dictionary<int, AreaActivationStateRecord>()
                : (await _activationStateRepository.GetAll()
                    .Where(record => tenantIds.Contains(record.TenantId))
                    .OrderBy(record => record.EffectiveAt)
                    .ToListAsync())
                    .GroupBy(record => record.TenantId)
                    .ToDictionary(group => group.Key, group => group.First());
            var leaderIds = tenants.Where(tenant => tenant.AreaLeaderId.HasValue).Select(tenant => tenant.AreaLeaderId.Value).Distinct().ToArray();
            using (DisableTenantFilterForHost())
            {
                var leaderCustomers = await (from leader in _areaLeaderRepository.GetAll()
                    join customer in _customerRepository.GetAll() on leader.CustomerId equals customer.Id
                    where leaderIds.Contains(leader.Id)
                    select new { leader.Id, customer.Name }).ToDictionaryAsync(item => item.Id, item => item.Name);
                return tenants.Select(tenant => new AdminTenantDto
                {
                    Id = tenant.Id, TenancyName = tenant.TenancyName, Name = tenant.Name, IsActive = tenant.IsActive,
                    HasActivationHistory = activationHistoryStarts.TryGetValue(tenant.Id, out var activationHistoryStart),
                    ActivationHistoryBeginsAt = activationHistoryStart?.EffectiveAt,
                    AreaLeaderId = tenant.AreaLeaderId,
                    AreaLeaderName = tenant.AreaLeaderId.HasValue && leaderCustomers.TryGetValue(tenant.AreaLeaderId.Value, out var name) ? name : null
                }).ToList();
            }
        }
    }
}
