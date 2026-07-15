using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.ObjectMapping;
using Abp.UI;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Customers
{
    public class CustomerAppService : AqualLifeStyleAppServiceBase, ICustomerAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IObjectMapper _objectMapper;

        public CustomerAppService(ICustomerRepository customerRepository, IMembershipRepository membershipRepository, IObjectMapper objectMapper)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _objectMapper = objectMapper;
        }

        [AbpAuthorize(AquaPermissions.Members.View)]
        public async Task<IReadOnlyList<CustomerDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Customer lookup failed.");
            var customers = await _customerRepository.GetAllListAsync(c => c.TenantId == tenantId);
            return _objectMapper.Map<List<CustomerDto>>(customers);
        }

        [AbpAuthorize]
        public async Task<CustomerDto> GetAsync(int id)
        {
            var customer = await GetCustomerForCurrentTenantAsync(id);
            if (!await CurrentUserCanAccessCustomerAsync(customer))
            {
                throw new UserFriendlyException("Customer lookup failed.", "You do not have permission to access this customer.");
            }

            return _objectMapper.Map<CustomerDto>(customer);
        }

        [AbpAuthorize(AquaPermissions.Members.ViewSelf)]
        public async Task<CustomerDto> GetMyCustomerAsync()
        {
            if (!AbpSession.UserId.HasValue)
            {
                throw new UserFriendlyException("Customer lookup failed.", "No user context is available.");
            }

            var tenantId = GetRequiredTenantId("Customer lookup failed.");
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.UserId == AbpSession.UserId.Value && c.TenantId == tenantId);
            if (customer == null)
            {
                throw new UserFriendlyException("Customer lookup failed.", "No customer profile is linked to your account.");
            }

            return _objectMapper.Map<CustomerDto>(customer);
        }

        [AbpAuthorize(AquaPermissions.Memberships.Upgrade)]
        public async Task<CustomerDto> ChangeMembershipAsync(ChangeMembershipDto input)
        {
            if (input == null)
            {
                throw new UserFriendlyException("Membership change failed.", "The request body was empty.");
            }

            if (!AbpSession.UserId.HasValue)
            {
                throw new UserFriendlyException("Membership change failed.", "No user context is available.");
            }

            var tenantId = GetRequiredTenantId("Membership change failed.");
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.UserId == AbpSession.UserId.Value && c.TenantId == tenantId);
            if (customer == null)
            {
                throw new UserFriendlyException("Membership change failed.", "No customer profile is linked to your account.");
            }

            if (!input.MembershipId.HasValue)
            {
                customer.ChangeMembership(null);
                await _customerRepository.UpdateAsync(customer);
                return _objectMapper.Map<CustomerDto>(customer);
            }

            var membership = await _membershipRepository.GetAsync(input.MembershipId.Value);
            if (membership == null)
            {
                throw new UserFriendlyException("Membership change failed.", "The selected membership does not exist.");
            }

            membership.EnsureCanBeAssignedToCustomer();
            customer.ChangeMembership(input.MembershipId.Value);
            await _customerRepository.UpdateAsync(customer);

            return _objectMapper.Map<CustomerDto>(customer);
        }

        [AbpAuthorize(AquaPermissions.Members.Edit)]
        public async Task<CustomerDto> UpdateAsync(CustomerDto input)
        {
            if (input == null)
            {
                throw new UserFriendlyException("Customer update failed.", "The request body was empty.");
            }

            if (input.Id <= 0)
            {
                throw new UserFriendlyException("Customer update failed.", "A valid customer Id is required.");
            }

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                throw new UserFriendlyException("Customer update failed.", "Customer name is required.");
            }

            if (string.IsNullOrWhiteSpace(input.Email))
            {
                throw new UserFriendlyException("Customer update failed.", "Customer email is required.");
            }

            try
            {
                var customer = await GetCustomerForCurrentTenantAsync(input.Id);
                if (!await CurrentUserCanAccessCustomerAsync(customer))
                {
                    throw new UserFriendlyException("Customer update failed.", "You do not have permission to update this customer.");
                }

                if (await _customerRepository.ExistsByEmailAsync(input.Email, input.Id))
                {
                    throw new UserFriendlyException("Customer update failed.", "A customer with that email already exists.");
                }

                if (input.MembershipId.HasValue)
                {
                    var membership = await _membershipRepository.GetAsync(input.MembershipId.Value);
                    membership.EnsureCanBeAssignedToCustomer();
                }

                customer.Rename(input.Name);
                customer.ChangeEmail(new EmailAddress(input.Email));
                customer.ChangeMembership(input.MembershipId);

                if (input.IsActive)
                {
                    customer.Activate();
                }
                else
                {
                    customer.Deactivate();
                }

                await _customerRepository.UpdateAsync(customer);
                return _objectMapper.Map<CustomerDto>(customer);
            }
            catch (UserFriendlyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException("Customer update failed.", ex.Message);
            }
        }

        [AbpAuthorize(AquaPermissions.Members.Create)]
        public async Task CreateAsync(CreateCustomerDto input)
        {
            if (input == null)
            {
                throw new UserFriendlyException("Customer creation failed.", "The request body was empty.");
            }

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                throw new UserFriendlyException("Customer creation failed.", "Customer name is required.");
            }

            if (string.IsNullOrWhiteSpace(input.Email))
            {
                throw new UserFriendlyException("Customer creation failed.", "Customer email is required.");
            }

            var tenantId = GetRequiredTenantId("Customer creation failed.");

            if (await _customerRepository.ExistsByEmailAsync(input.Email))
            {
                throw new UserFriendlyException("Customer creation failed.", "A customer with that email already exists.");
            }

            try
            {
                if (!AbpSession.UserId.HasValue)
                {
                    throw new UserFriendlyException("Customer creation failed.", "A user context is required to create a customer.");
                }

                if (input.MembershipId.HasValue)
                {
                    var membership = await _membershipRepository.GetAsync(input.MembershipId.Value);
                    membership.EnsureCanBeAssignedToCustomer();
                }

                var email = new EmailAddress(input.Email);
                var customer = Customer.Create(tenantId, AbpSession.UserId.Value, input.Name, email, input.MembershipId);
                await _customerRepository.InsertAsync(customer);
            }
            catch (UserFriendlyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException("Customer creation failed.", ex.Message);
            }
        }

        private async Task<Customer> GetCustomerForCurrentTenantAsync(int id)
        {
            var tenantId = GetRequiredTenantId("Customer lookup failed.");
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (customer == null)
            {
                throw new UserFriendlyException("Customer lookup failed.", $"Customer with id {id} was not found.");
            }

            return customer;
        }

        protected override Exception CreateMissingTenantContextException(string operation)
        {
            return new AbpAuthorizationException($"{operation} A tenant context is required.");
        }
    }
}
