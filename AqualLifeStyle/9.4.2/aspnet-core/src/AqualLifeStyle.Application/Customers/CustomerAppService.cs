using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.ObjectMapping;
using Abp.UI;
using AqualLifeStyle.Application.Customers.Dto;
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

        public async Task<IReadOnlyList<CustomerDto>> GetAllAsync()
        {
            var tenantId = GetRequiredTenantId("Customer lookup failed.");
            var customers = await _customerRepository.GetAllListAsync(c => c.TenantId == tenantId);
            return _objectMapper.Map<List<CustomerDto>>(customers);
        }

        public async Task<CustomerDto> GetAsync(int id)
        {
            var customer = await GetCustomerForCurrentTenantAsync(id);
            return _objectMapper.Map<CustomerDto>(customer);
        }

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
                if (input.MembershipId.HasValue)
                {
                    var membership = await _membershipRepository.GetAsync(input.MembershipId.Value);
                    membership.EnsureCanBeAssignedToCustomer();
                }

                var email = new EmailAddress(input.Email);
                var customer = Customer.Create(tenantId, input.Name, email, input.MembershipId);
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
    }
}
