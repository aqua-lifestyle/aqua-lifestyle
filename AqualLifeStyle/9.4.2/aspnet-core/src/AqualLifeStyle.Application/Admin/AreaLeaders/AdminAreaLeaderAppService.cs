using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.IdentityFramework;
using AqualLifeStyle.Application.Admin.AreaLeaders.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.AreaLeaders
{
    [Audited]
    public class AdminAreaLeaderAppService : AdminAppServiceBase, IAdminAreaLeaderAppService
    {
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly UserManager _userManager;

        public AdminAreaLeaderAppService(
            IAreaLeaderRepository areaLeaderRepository,
            ICustomerRepository customerRepository,
            UserManager userManager)
        {
            _areaLeaderRepository = areaLeaderRepository;
            _customerRepository = customerRepository;
            _userManager = userManager;
        }

        [AbpAuthorize(AquaPermissions.Admin.AreaLeaders.View)]
        public async Task<PagedResultDto<AdminAreaLeaderDto>> GetAllAsync(AdminAreaLeaderListInput input)
        {
            input ??= new AdminAreaLeaderListInput();
            ValidateRequestedTenant(input.TenantId, "Area leader");
            using (DisableTenantFilterForHost())
            {
                var query = _areaLeaderRepository.GetAll();
                if (AbpSession.TenantId.HasValue) query = query.Where(leader => leader.TenantId == AbpSession.TenantId.Value);
                else if (input.TenantId.HasValue) query = query.Where(leader => leader.TenantId == input.TenantId.Value);
                if (input.IsApproved.HasValue) query = query.Where(leader => leader.IsApproved == input.IsApproved.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    var matchingCustomerIds = _customerRepository.GetAll()
                        .Where(customer => customer.Name.ToLower().Contains(keyword) || customer.Email.Value.ToLower().Contains(keyword))
                        .Select(customer => customer.Id);
                    query = query.Where(leader => matchingCustomerIds.Contains(leader.CustomerId));
                }

                var total = await query.CountAsync();
                var leaders = await query.OrderByDescending(leader => leader.CreationTime)
                    .Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
                var customerIds = leaders.Select(leader => leader.CustomerId).ToArray();
                var customers = await _customerRepository.GetAll().Where(customer => customerIds.Contains(customer.Id)).ToListAsync();
                var customersById = customers.ToDictionary(customer => customer.Id);
                var mapped = leaders.Where(leader => customersById.ContainsKey(leader.CustomerId))
                    .Select(leader => Map(leader, customersById[leader.CustomerId])).ToList();
                return new PagedResultDto<AdminAreaLeaderDto>(total, mapped);
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.AreaLeaders.View)]
        public async Task<AdminAreaLeaderDto> GetAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Area leader");
            var leader = await GetAreaLeaderAsync(input.Id);
            return Map(leader, await GetCustomerAsync(leader));
        }

        [AbpAuthorize(AquaPermissions.Admin.AreaLeaders.Approve)]
        public async Task<AdminAreaLeaderDto> ApproveAsync(ApproveAreaLeaderInput input)
        {
            ValidateMutation(input, "approval");
            var leader = await GetAreaLeaderAsync(input.Id);
            var customer = await GetCustomerAsync(leader);
            using (CurrentUnitOfWork.SetTenantId(leader.TenantId))
            {
                leader.ApproveApplication();
                customer.User.SetRole(AquaUserRole.AreaLeader);
                (await _userManager.UpdateAsync(customer.User)).CheckErrors(LocalizationManager);
                (await _userManager.SetRolesAsync(customer.User, new[] { AquaUserRole.AreaLeader.ToString() })).CheckErrors(LocalizationManager);
                await _areaLeaderRepository.UpdateAsync(leader);
            }
            LogAdminMutation("Area leader", "approved", leader.Id, leader.TenantId, input.Justification);
            return Map(leader, customer);
        }

        [AbpAuthorize(AquaPermissions.Admin.AreaLeaders.Promote)]
        public async Task<AdminAreaLeaderDto> PromoteAsync(PromoteAreaLeaderInput input)
        {
            ValidateMutation(input, "promotion");
            var leader = await GetAreaLeaderAsync(input.Id);
            leader.PromoteToCurrentRank(new RankProgressionPolicy());
            await _areaLeaderRepository.UpdateAsync(leader);
            LogAdminMutation("Area leader", "promoted", leader.Id, leader.TenantId, input.Justification);
            return Map(leader, await GetCustomerAsync(leader));
        }

        [AbpAuthorize(AquaPermissions.Admin.AreaLeaders.Demote)]
        public async Task<AdminAreaLeaderDto> DemoteAsync(DemoteAreaLeaderInput input)
        {
            ValidateMutation(input, "demotion");
            var leader = await GetAreaLeaderAsync(input.Id);
            leader.DemoteOneRank();
            await _areaLeaderRepository.UpdateAsync(leader);
            LogAdminMutation("Area leader", "demoted", leader.Id, leader.TenantId, input.Justification);
            return Map(leader, await GetCustomerAsync(leader));
        }

        [AbpAuthorize(AquaPermissions.Admin.AreaLeaders.Remove)]
        public async Task RemoveAsync(RemoveAreaLeaderInput input)
        {
            ValidateMutation(input, "removal");
            var leader = await GetAreaLeaderAsync(input.Id);
            var customer = await GetCustomerAsync(leader);
            var replacementRole = customer.MembershipId.HasValue ? AquaUserRole.Member : AquaUserRole.Guest;
            using (CurrentUnitOfWork.SetTenantId(leader.TenantId))
            {
                customer.User.SetRole(replacementRole);
                (await _userManager.UpdateAsync(customer.User)).CheckErrors(LocalizationManager);
                (await _userManager.SetRolesAsync(customer.User, new[] { replacementRole.ToString() })).CheckErrors(LocalizationManager);
                await _areaLeaderRepository.DeleteAsync(leader);
            }
            LogAdminMutation("Area leader", "removed", leader.Id, leader.TenantId, input.Justification);
        }

        private async Task<AreaLeader> GetAreaLeaderAsync(int id)
        {
            using (DisableTenantFilterForHost())
            {
                var query = _areaLeaderRepository.GetAll().Where(leader => leader.Id == id);
                if (AbpSession.TenantId.HasValue) query = query.Where(leader => leader.TenantId == AbpSession.TenantId.Value);
                var leader = await query.SingleOrDefaultAsync();
                if (leader == null) throw Failed("Area leader lookup", "The area leader was not found.");
                return leader;
            }
        }

        private async Task<Customer> GetCustomerAsync(AreaLeader leader)
        {
            using (DisableTenantFilterForHost())
            {
                var customer = await _customerRepository.GetAllIncluding(item => item.User)
                    .SingleOrDefaultAsync(item => item.Id == leader.CustomerId && item.TenantId == leader.TenantId);
                if (customer == null) throw Failed("Area leader lookup", "The linked customer was not found.");
                return customer;
            }
        }

        private static void ValidateMutation(AreaLeaderAdminMutationInput input, string operation)
        {
            if (input == null) throw Failed($"Area leader {operation}", "The request body was empty.");
            ValidatePositiveId(input.Id, "Area leader");
        }

        private static AdminAreaLeaderDto Map(AreaLeader leader, Customer customer) => new AdminAreaLeaderDto
        {
            Id = leader.Id, TenantId = leader.TenantId, CustomerId = leader.CustomerId,
            CustomerName = customer.Name, Email = customer.Email.Value, LicenseType = (int)leader.LicenseType,
            Rank = (int)leader.Rank, IsApproved = leader.IsApproved, ApprovedAt = leader.ApprovedAt,
            DirectReferrals = leader.DirectReferrals, IndirectReferrals = leader.IndirectReferrals,
            OrderTarget = leader.OrderTarget, CreationTime = leader.CreationTime
        };
    }
}
