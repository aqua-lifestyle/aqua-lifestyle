using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
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

        public CustomerAppService(ICustomerRepository customerRepository, IMembershipRepository membershipRepository)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
        }

        public async Task<IReadOnlyList<CustomerDto>> GetAllAsync()
        {
            var customers = await _customerRepository.GetAllListAsync();
            return customers.Select(c => new CustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email?.Value,
                MembershipId = c.MembershipId,
                IsActive = c.IsActive
            }).ToList();
        }

        public async Task<CustomerDto> GetAsync(int id)
        {
            var customer = await _customerRepository.GetAsync(id);
            return MapToDto(customer);
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
                var customer = await _customerRepository.GetAsync(input.Id);

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
                return MapToDto(customer);
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
                var customer = Customer.Create(AbpSession.TenantId, input.Name, email, input.MembershipId);
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

        private static CustomerDto MapToDto(Customer customer)
        {
            return new CustomerDto
            {
                Id = customer.Id,
                Name = customer.Name,
                Email = customer.Email?.Value,
                MembershipId = customer.MembershipId,
                IsActive = customer.IsActive
            };
        }
    }
}
