using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.MultiTenancy;
using Abp.ObjectMapping;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Customers.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Customers
{
    [Audited]
    public class AdminCustomerAppService : AdminAppServiceBase, IAdminCustomerAppService
    {
        private readonly IAdminCustomerAccountManager _accountManager;
        private readonly ICustomerRepository _customerRepository;
        private readonly UserManager _userManager;
        private readonly IObjectMapper _objectMapper;
        private readonly IAdminCustomerProfileUpdater _customerProfileUpdater;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IUserPasswordSetupLinkGenerator _passwordSetupLinkGenerator;

        public AdminCustomerAppService(
            IAdminCustomerAccountManager accountManager,
            ICustomerRepository customerRepository,
            UserManager userManager,
            IObjectMapper objectMapper,
            IAdminCustomerProfileUpdater customerProfileUpdater,
            IMembershipRepository membershipRepository,
            IUserPasswordSetupLinkGenerator passwordSetupLinkGenerator)
        {
            _accountManager = accountManager;
            _customerRepository = customerRepository;
            _userManager = userManager;
            _objectMapper = objectMapper;
            _customerProfileUpdater = customerProfileUpdater;
            _membershipRepository = membershipRepository;
            _passwordSetupLinkGenerator = passwordSetupLinkGenerator;
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.View)]
        public async Task<PagedResultDto<AdminCustomerDto>> GetAllAsync(AdminCustomerListInput input)
        {
            input ??= new AdminCustomerListInput();
            ValidateRequestedTenant(input.TenantId, "Customer");

            using (DisableTenantFilterForHost())
            {
                var query = _customerRepository.GetAllIncluding(customer => customer.User)
                    .Where(customer => customer.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue)
                    query = query.Where(customer => customer.TenantId == AbpSession.TenantId.Value);
                else if (input.TenantId.HasValue)
                    query = query.Where(customer => customer.TenantId == input.TenantId.Value);
                if (input.IsActive.HasValue)
                    query = query.Where(customer => customer.IsActive == input.IsActive.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    query = query.Where(customer =>
                        customer.Name.ToLower().Contains(keyword) ||
                        customer.Email.Value.ToLower().Contains(keyword));
                }

                var total = await query.CountAsync();
                var customers = await query.OrderByDescending(customer => customer.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
                var customerDtos = _objectMapper.Map<List<AdminCustomerDto>>(customers);
                await PopulateMembershipNamesAsync(customerDtos);
                return new PagedResultDto<AdminCustomerDto>(total, customerDtos);
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.View)]
        public async Task<List<AdminMembershipOptionDto>> GetMembershipOptionsAsync(AdminCustomerMembershipOptionsInput input)
        {
            if (input == null) throw Failed("Membership plan lookup", "The request body was empty.");
            var tenantId = ResolveTargetTenant(input.TenantId, "Membership plan", "lookup");
            using (DisableTenantDataFilter())
            {
                return await _membershipRepository.GetAll()
                    .Where(plan =>
                        plan.IsActive &&
                        plan.MembershipType != MembershipType.Onyx &&
                        (!plan.TenantId.HasValue || plan.TenantId == tenantId))
                    .OrderBy(plan => plan.MembershipType).ThenBy(plan => plan.Name)
                    .Select(plan => new AdminMembershipOptionDto { Id = plan.Id, Name = plan.Name, MembershipType = (int)plan.MembershipType })
                    .ToListAsync();
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.View)]
        public async Task<AdminCustomerDto> GetAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Customer");
            return await MapCustomerAsync(await GetCustomerAsync(input.Id));
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Create)]
        public async Task<AdminCustomerOnboardingResultDto> CreateAsync(AdminCreateCustomerInput input)
        {
            if (input == null) throw new UserFriendlyException("Customer creation failed.", "The request body was empty.");
            var tenantId = ResolveTargetTenant(input.TenantId, "Customer", "creation");
            await EnsureTenantExistsAsync(tenantId);
            var accountResult = await _accountManager.CreateOrFindRemovedAsync(new AdminCustomerAccountInput
            {
                TenantId = tenantId,
                FirstName = input.FirstName,
                LastName = input.LastName,
                Email = input.Email,
                Password = input.Password,
                MembershipId = input.MembershipId,
                IsActive = input.IsActive
            });
            if (accountResult.RemovedCustomer != null)
            {
                return new AdminCustomerOnboardingResultDto
                {
                    RequiresRestoreConfirmation = true,
                    RemovedCustomer = new AdminRemovedCustomerCandidateDto
                    {
                        CustomerId = accountResult.RemovedCustomer.Id,
                        Name = accountResult.RemovedCustomer.Name,
                        Email = accountResult.RemovedCustomer.Email.Value,
                        RemovalTime = accountResult.RemovedCustomer.DeletionTime
                    }
                };
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            var customer = accountResult.Customer;
            LogAdminMutation("Customer", "created", customer.Id, tenantId, input.Justification);
            return new AdminCustomerOnboardingResultDto
            {
                Customer = await MapCustomerAsync(customer)
            };
        }

        [DisableAuditing]
        [AbpAuthorize(AquaPermissions.Admin.Customers.Create)]
        public async Task<AdminCustomerOnboardingResultDto> RestoreAsync(AdminRestoreCustomerInput input)
        {
            if (input == null) throw new UserFriendlyException("Customer restoration failed.", "The request body was empty.");
            ValidatePositiveId(input.CustomerId, "Customer");
            var removedCustomer = await GetRemovedCustomerAsync(input.CustomerId);
            var tenantId = removedCustomer.TenantId.Value;
            var customer = await _accountManager.RestoreAsync(input.CustomerId, new AdminCustomerAccountInput
            {
                TenantId = tenantId,
                FirstName = input.FirstName,
                LastName = input.LastName,
                Email = input.Email,
                MembershipId = input.MembershipId,
                IsActive = input.IsActive
            });
            await CurrentUnitOfWork.SaveChangesAsync();
            var tenant = await TenantManager.GetByIdAsync(tenantId);
            var passwordSetupUrl = await _passwordSetupLinkGenerator.GenerateAsync(customer.User, tenant.TenancyName);
            LogAdminMutation("Customer", "restored with password setup required", customer.Id, tenantId, input.Justification);
            return new AdminCustomerOnboardingResultDto
            {
                Customer = await MapCustomerAsync(customer),
                PasswordSetupUrl = passwordSetupUrl
            };
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Edit)]
        public async Task<AdminCustomerDto> UpdateAsync(AdminUpdateCustomerInput input)
        {
            if (input == null) throw new UserFriendlyException("Customer update failed.", "The request body was empty.");
            ValidatePositiveId(input.Id, "Customer");
            var customer = await GetCustomerAsync(input.Id);
            var tenantId = customer.TenantId.Value;
            using (CurrentUnitOfWork.SetTenantId(tenantId))
            {
                await _customerProfileUpdater.UpdateAsync(customer, new AdminCustomerProfileUpdate
                {
                    FirstName = input.FirstName, LastName = input.LastName, Email = input.Email,
                    MembershipId = input.MembershipId, IsActive = input.IsActive
                });
                await CurrentUnitOfWork.SaveChangesAsync();
            }

            LogAdminMutation("Customer", "updated", customer.Id, tenantId, input.Justification);
            return await MapCustomerAsync(customer);
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Delete)]
        public async Task DeleteAsync(AdminDeleteCustomerInput input)
        {
            if (input == null) throw new UserFriendlyException("Customer removal failed.", "The request body was empty.");
            ValidatePositiveId(input.Id, "Customer");
            var customer = await GetCustomerAsync(input.Id);
            var tenantId = customer.TenantId.Value;
            using (CurrentUnitOfWork.SetTenantId(tenantId))
            {
                customer.Deactivate();
                customer.User.IsActive = false;
                CheckErrors(await _userManager.UpdateSecurityStampAsync(customer.User));
                await _customerRepository.DeleteAsync(customer);
            }
            LogAdminMutation("Customer", "removed", customer.Id, tenantId, input.Justification);
        }

        private async Task<Customer> GetCustomerAsync(int id)
        {
            using (DisableTenantFilterForHost())
            {
                var query = _customerRepository.GetAllIncluding(customer => customer.User)
                    .Where(customer => customer.Id == id && customer.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue)
                    query = query.Where(customer => customer.TenantId == AbpSession.TenantId.Value);
                var customer = await query.SingleOrDefaultAsync();
                if (customer == null) throw new UserFriendlyException("Customer lookup failed.", "The customer was not found.");
                return customer;
            }
        }

        private async Task<Customer> GetRemovedCustomerAsync(int id)
        {
            using (DisableTenantDataFilter())
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete))
            {
                var query = _customerRepository.GetAllIncluding(customer => customer.User)
                    .Where(customer => customer.Id == id && customer.IsDeleted && customer.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue)
                    query = query.Where(customer => customer.TenantId == AbpSession.TenantId.Value);
                var customer = await query.SingleOrDefaultAsync();
                if (customer == null)
                    throw new UserFriendlyException("Customer restoration failed.", "The removed customer account was not found.");
                return customer;
            }
        }

        private async Task EnsureTenantExistsAsync(int tenantId)
        {
            var tenant = await TenantManager.GetByIdAsync(tenantId);
            if (!tenant.IsActive) throw new UserFriendlyException("Customer creation failed.", "The selected tenant is inactive.");
        }

        private async Task<AdminCustomerDto> MapCustomerAsync(Customer customer)
        {
            var customerDto = _objectMapper.Map<AdminCustomerDto>(customer);
            await PopulateMembershipNamesAsync(new[] { customerDto });
            return customerDto;
        }

        private async Task PopulateMembershipNamesAsync(IEnumerable<AdminCustomerDto> customerDtos)
        {
            var customers = customerDtos.ToList();
            var membershipIds = customers.Where(customer => customer.MembershipId.HasValue)
                .Select(customer => customer.MembershipId.Value)
                .Distinct()
                .ToList();
            if (membershipIds.Count == 0) return;

            using (DisableTenantDataFilter())
            {
                var membershipNames = await _membershipRepository.GetAll()
                    .Where(membership => membershipIds.Contains(membership.Id))
                    .ToDictionaryAsync(membership => membership.Id, membership => membership.Name);
                foreach (var customer in customers)
                {
                    if (customer.MembershipId.HasValue && membershipNames.TryGetValue(customer.MembershipId.Value, out var membershipName))
                        customer.MembershipName = membershipName;
                }
            }
        }

    }
}
