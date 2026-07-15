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
        private readonly IMembershipRepository _membershipRepository;
        private readonly IRepository<User, long> _userRepository;
        private readonly UserManager _userManager;
        private readonly IObjectMapper _objectMapper;
        private readonly IAdminUserRoleSynchronizer _userRoleSynchronizer;

        public AdminCustomerAppService(
            IAdminCustomerAccountManager accountManager,
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IRepository<User, long> userRepository,
            UserManager userManager,
            IObjectMapper objectMapper,
            IAdminUserRoleSynchronizer userRoleSynchronizer)
        {
            _accountManager = accountManager;
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _userRepository = userRepository;
            _userManager = userManager;
            _objectMapper = objectMapper;
            _userRoleSynchronizer = userRoleSynchronizer;
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
                return new PagedResultDto<AdminCustomerDto>(total, _objectMapper.Map<List<AdminCustomerDto>>(customers));
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.View)]
        public async Task<AdminCustomerDto> GetAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Customer");
            return _objectMapper.Map<AdminCustomerDto>(await GetCustomerAsync(input.Id));
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Create)]
        public async Task<AdminCustomerDto> CreateAsync(AdminCreateCustomerInput input)
        {
            if (input == null) throw new UserFriendlyException("Customer creation failed.", "The request body was empty.");
            var tenantId = ResolveTargetTenant(input.TenantId, "Customer", "creation");
            await EnsureTenantExistsAsync(tenantId);
            var customer = await _accountManager.CreateAsync(new AdminCustomerAccountInput
            {
                TenantId = tenantId,
                FirstName = input.FirstName,
                LastName = input.LastName,
                Email = input.Email,
                MembershipId = input.MembershipId,
                IsActive = input.IsActive
            });
            await CurrentUnitOfWork.SaveChangesAsync();
            LogAdminMutation("Customer", "created", customer.Id, tenantId, input.Justification);
            return _objectMapper.Map<AdminCustomerDto>(customer);
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
                await ValidateUpdateAsync(customer, input);
                var user = customer.User;
                user.Name = input.FirstName.Trim();
                user.Surname = input.LastName.Trim();
                user.EmailAddress = input.Email.Trim();
                user.UserName = input.Email.Trim();
                user.IsActive = input.IsActive;
                user.SetNormalizedNames();
                await _userRoleSynchronizer.SynchronizeAsync(
                    user,
                    input.MembershipId.HasValue ? AquaUserRole.Member : AquaUserRole.Guest);

                customer.Rename($"{user.Name} {user.Surname}");
                customer.ChangeEmail(new EmailAddress(input.Email));
                customer.ChangeMembership(input.MembershipId);
                if (input.IsActive) customer.Activate(); else customer.Deactivate();
                await _customerRepository.UpdateAsync(customer);
                await CurrentUnitOfWork.SaveChangesAsync();
            }

            LogAdminMutation("Customer", "updated", customer.Id, tenantId, input.Justification);
            return _objectMapper.Map<AdminCustomerDto>(customer);
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
                CheckErrors(await _userManager.UpdateAsync(customer.User));
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

        private async Task ValidateUpdateAsync(Customer customer, AdminUpdateCustomerInput input)
        {
            if (string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName))
                throw new UserFriendlyException("Customer update failed.", "First name and last name are required.");
            var normalizedEmail = new EmailAddress(input.Email).Value.ToUpperInvariant();
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete))
            {
                if (await _customerRepository.GetAll().AnyAsync(item => item.Id != customer.Id && item.Email.Value.ToUpper() == normalizedEmail))
                    throw new UserFriendlyException("Customer update failed.", "The email address is unavailable.");
            }
            if (await _userRepository.GetAll().AnyAsync(user => user.Id != customer.UserId && user.NormalizedEmailAddress == normalizedEmail))
                throw new UserFriendlyException("Customer update failed.", "The email address is unavailable.");
            if (input.MembershipId.HasValue && await _membershipRepository.FirstOrDefaultAsync(membership =>
                    membership.Id == input.MembershipId.Value && membership.TenantId == customer.TenantId && membership.IsActive) == null)
                throw new UserFriendlyException("Customer update failed.", "The selected membership is missing or inactive.");
        }

        private async Task EnsureTenantExistsAsync(int tenantId)
        {
            var tenant = await TenantManager.GetByIdAsync(tenantId);
            if (!tenant.IsActive) throw new UserFriendlyException("Customer creation failed.", "The selected tenant is inactive.");
        }

    }
}
