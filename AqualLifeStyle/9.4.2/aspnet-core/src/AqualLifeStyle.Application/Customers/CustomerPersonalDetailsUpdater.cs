using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.MultiTenancy;
using Abp.UI;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Customers
{
    public class CustomerPersonalDetailsUpdate
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string HomeAddress { get; set; }
    }

    public interface ICustomerPersonalDetailsUpdater
    {
        Task UpdateAsync(Customer customer, CustomerPersonalDetailsUpdate update, string operationName);
    }

    public class CustomerPersonalDetailsUpdater : ICustomerPersonalDetailsUpdater, ITransientDependency
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<User, long> _userRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public CustomerPersonalDetailsUpdater(
            ICustomerRepository customerRepository,
            IRepository<User, long> userRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _customerRepository = customerRepository;
            _userRepository = userRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task UpdateAsync(Customer customer, CustomerPersonalDetailsUpdate update, string operationName)
        {
            var title = $"{operationName} failed.";
            if (customer == null) throw new UserFriendlyException(title, "The customer was not found.");
            if (update == null) throw new UserFriendlyException(title, "The update was empty.");
            if (string.IsNullOrWhiteSpace(update.FirstName) || string.IsNullOrWhiteSpace(update.LastName))
                throw new UserFriendlyException(title, "First name and surname are required.");

            EmailAddress email;
            try
            {
                email = new EmailAddress(update.Email);
                if (update.ContactNumber != null || update.HomeAddress != null)
                    customer.User.UpdateContactDetails(update.ContactNumber, update.HomeAddress);
            }
            catch (ArgumentException exception)
            {
                throw new UserFriendlyException(title, exception.Message);
            }

            var normalizedEmail = email.Value.ToUpperInvariant();
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                if (await _customerRepository.GetAll().AnyAsync(item => item.Id != customer.Id && item.Email.Value.ToUpper() == normalizedEmail))
                    throw new UserFriendlyException(title, "The email address is unavailable.");
            }

            if (await _userRepository.GetAll().AnyAsync(user => user.Id != customer.UserId && user.NormalizedEmailAddress == normalizedEmail))
                throw new UserFriendlyException(title, "The email address is unavailable.");

            var user = customer.User;
            user.Name = update.FirstName.Trim();
            user.Surname = update.LastName.Trim();
            user.EmailAddress = email.Value;
            user.UserName = email.Value;
            user.SetNormalizedNames();
            customer.Rename($"{user.Name} {user.Surname}");
            customer.ChangeEmail(email);
        }
    }
}
