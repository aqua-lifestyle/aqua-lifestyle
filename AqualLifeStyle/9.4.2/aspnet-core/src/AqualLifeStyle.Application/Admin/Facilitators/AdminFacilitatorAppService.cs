using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Facilitators.Dto;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Facilitators
{
    [Audited]
    public class AdminFacilitatorAppService : AdminAppServiceBase, IAdminFacilitatorAppService
    {
        private readonly IFacilitatorRepository _facilitatorRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IAdminUserRoleSynchronizer _userRoleSynchronizer;
        private readonly ICustomerFallbackRoleResolver _fallbackRoleResolver;

        public AdminFacilitatorAppService(
            IFacilitatorRepository facilitatorRepository,
            ICustomerRepository customerRepository,
            IAdminUserRoleSynchronizer userRoleSynchronizer,
            ICustomerFallbackRoleResolver fallbackRoleResolver)
        {
            _facilitatorRepository = facilitatorRepository;
            _customerRepository = customerRepository;
            _userRoleSynchronizer = userRoleSynchronizer;
            _fallbackRoleResolver = fallbackRoleResolver;
        }

        [AbpAuthorize(AquaPermissions.Admin.Facilitators.View)]
        public async Task<PagedResultDto<AdminFacilitatorDto>> GetAllAsync(AdminFacilitatorListInput input)
        {
            input ??= new AdminFacilitatorListInput();
            ValidateRequestedTenant(input.TenantId, "Facilitator");
            using (DisableTenantFilterForHost())
            {
                var query = _facilitatorRepository.GetAll();
                if (AbpSession.TenantId.HasValue) query = query.Where(item => item.TenantId == AbpSession.TenantId.Value);
                else if (input.TenantId.HasValue) query = query.Where(item => item.TenantId == input.TenantId.Value);
                if (input.IsApproved.HasValue) query = query.Where(item => item.IsApproved == input.IsApproved.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    var matchingCustomerIds = _customerRepository.GetAll()
                        .Where(customer => customer.Name.ToLower().Contains(keyword) || customer.Email.Value.ToLower().Contains(keyword))
                        .Select(customer => customer.Id);
                    query = query.Where(item => matchingCustomerIds.Contains(item.CustomerId));
                }
                var total = await query.CountAsync();
                var facilitators = await query.OrderByDescending(item => item.CreationTime)
                    .Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
                var customerIds = facilitators.Select(item => item.CustomerId).ToArray();
                var customers = await _customerRepository.GetAll().Where(customer => customerIds.Contains(customer.Id)).ToListAsync();
                var customersById = customers.ToDictionary(customer => customer.Id);
                return new PagedResultDto<AdminFacilitatorDto>(total, facilitators
                    .Where(item => customersById.ContainsKey(item.CustomerId))
                    .Select(item => Map(item, customersById[item.CustomerId])).ToList());
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Facilitators.View)]
        public async Task<AdminFacilitatorDto> GetAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Facilitator");
            var facilitator = await GetFacilitatorAsync(input.Id);
            return Map(facilitator, await GetCustomerAsync(facilitator));
        }

        [AbpAuthorize(AquaPermissions.Admin.Facilitators.Approve)]
        public async Task<AdminFacilitatorDto> ApproveAsync(ApproveFacilitatorInput input)
        {
            ValidateMutation(input, "approval");
            var facilitator = await GetFacilitatorAsync(input.Id);
            var customer = await GetCustomerAsync(facilitator);
            using (CurrentUnitOfWork.SetTenantId(facilitator.TenantId))
            {
                facilitator.ApproveApplication();
                await _userRoleSynchronizer.SynchronizeAsync(customer.User, AquaUserRole.Facilitator);
                await _facilitatorRepository.UpdateAsync(facilitator);
            }
            LogAdminMutation("Facilitator", "approved", facilitator.Id, facilitator.TenantId, input.Justification);
            return Map(facilitator, customer);
        }

        [AbpAuthorize(AquaPermissions.Admin.Facilitators.Promote)]
        public async Task<AdminFacilitatorDto> PromoteAsync(PromoteFacilitatorInput input)
        {
            ValidateMutation(input, "promotion");
            var facilitator = await GetFacilitatorAsync(input.Id);
            facilitator.PromoteToEarnedRank(new RankProgressionPolicy(), new CommissionCalculator());
            await _facilitatorRepository.UpdateAsync(facilitator);
            LogAdminMutation("Facilitator", "promoted", facilitator.Id, facilitator.TenantId, input.Justification);
            return Map(facilitator, await GetCustomerAsync(facilitator));
        }

        [AbpAuthorize(AquaPermissions.Admin.Facilitators.Demote)]
        public async Task<AdminFacilitatorDto> DemoteAsync(DemoteFacilitatorInput input)
        {
            ValidateMutation(input, "demotion");
            var facilitator = await GetFacilitatorAsync(input.Id);
            facilitator.DemoteOneRank();
            await _facilitatorRepository.UpdateAsync(facilitator);
            LogAdminMutation("Facilitator", "demoted", facilitator.Id, facilitator.TenantId, input.Justification);
            return Map(facilitator, await GetCustomerAsync(facilitator));
        }

        [AbpAuthorize(AquaPermissions.Admin.Facilitators.Remove)]
        public async Task RemoveAsync(RemoveFacilitatorInput input)
        {
            ValidateMutation(input, "removal");
            var facilitator = await GetFacilitatorAsync(input.Id);
            var customer = await GetCustomerAsync(facilitator);
            using (CurrentUnitOfWork.SetTenantId(facilitator.TenantId))
            {
                await _userRoleSynchronizer.SynchronizeAsync(
                    customer.User,
                    await _fallbackRoleResolver.ResolveAsync(customer));
                await _facilitatorRepository.DeleteAsync(facilitator);
            }
            LogAdminMutation("Facilitator", "removed", facilitator.Id, facilitator.TenantId, input.Justification);
        }

        private async Task<Facilitator> GetFacilitatorAsync(int id)
        {
            using (DisableTenantFilterForHost())
            {
                var query = _facilitatorRepository.GetAll().Where(item => item.Id == id);
                if (AbpSession.TenantId.HasValue) query = query.Where(item => item.TenantId == AbpSession.TenantId.Value);
                var facilitator = await query.SingleOrDefaultAsync();
                if (facilitator == null) throw Failed("Facilitator lookup", "The facilitator was not found.");
                return facilitator;
            }
        }

        private async Task<Customer> GetCustomerAsync(Facilitator facilitator)
        {
            using (DisableTenantFilterForHost())
            {
                var customer = await _customerRepository.GetAllIncluding(item => item.User)
                    .SingleOrDefaultAsync(item => item.Id == facilitator.CustomerId && item.TenantId == facilitator.TenantId);
                if (customer == null) throw Failed("Facilitator lookup", "The linked customer was not found.");
                return customer;
            }
        }

        private static void ValidateMutation(FacilitatorAdminMutationInput input, string operation)
        {
            if (input == null) throw Failed($"Facilitator {operation}", "The request body was empty.");
            ValidatePositiveId(input.Id, "Facilitator");
        }
        private static AdminFacilitatorDto Map(Facilitator facilitator, Customer customer) => new AdminFacilitatorDto
        {
            Id = facilitator.Id, TenantId = facilitator.TenantId, CustomerId = facilitator.CustomerId,
            CustomerName = customer.Name, Email = customer.Email.Value, AreaLeaderId = facilitator.AreaLeaderId,
            Rank = (int)facilitator.Rank, IsApproved = facilitator.IsApproved, ApprovedAt = facilitator.ApprovedAt,
            DirectReferrals = facilitator.DirectReferrals, IndirectReferrals = facilitator.IndirectReferrals,
            AwardBalance = facilitator.AwardBalance, CreationTime = facilitator.CreationTime
        };
    }
}
