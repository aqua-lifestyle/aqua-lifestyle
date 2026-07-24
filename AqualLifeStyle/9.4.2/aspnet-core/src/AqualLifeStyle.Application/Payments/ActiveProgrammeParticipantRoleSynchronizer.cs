using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.IdentityFramework;
using Abp.Localization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Payments
{
    /// <summary>
    /// Promotes a guest account when a provider-confirmed payment activates
    /// programme participation. Existing business roles are never downgraded.
    /// </summary>
    public class ActiveProgrammeParticipantRoleSynchronizer : ITransientDependency
    {
        private readonly IRepository<Customer> _customerRepository;
        private readonly UserManager _userManager;
        private readonly ILocalizationManager _localizationManager;

        public ActiveProgrammeParticipantRoleSynchronizer(
            IRepository<Customer> customerRepository,
            UserManager userManager,
            ILocalizationManager localizationManager)
        {
            _customerRepository = customerRepository;
            _userManager = userManager;
            _localizationManager = localizationManager;
        }

        public virtual async Task PromoteGuestToMemberAsync(int customerId)
        {
            var customer = await _customerRepository.GetAsync(customerId);
            var user = await _userManager.GetUserByIdAsync(customer.UserId);
            if (!user.IsGuest())
            {
                return;
            }

            user.SetRole(AquaUserRole.Member);
            (await _userManager.UpdateAsync(user)).CheckErrors(_localizationManager);
            (await _userManager.SetRolesAsync(
                user,
                new[] { AquaUserRole.Member.ToString() }))
                .CheckErrors(_localizationManager);
            (await _userManager.UpdateSecurityStampAsync(user))
                .CheckErrors(_localizationManager);
        }
    }
}
