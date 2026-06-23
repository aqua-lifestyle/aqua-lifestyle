using System.Threading.Tasks;
using Abp.Dependency;
using Abp.IdentityFramework;
using Abp.Localization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Admin
{
    public interface IAdminUserRoleSynchronizer
    {
        Task SynchronizeAsync(User user, AquaUserRole role);
    }

    public class AdminUserRoleSynchronizer : IAdminUserRoleSynchronizer, ITransientDependency
    {
        private readonly UserManager _userManager;
        private readonly ILocalizationManager _localizationManager;

        public AdminUserRoleSynchronizer(UserManager userManager, ILocalizationManager localizationManager)
        {
            _userManager = userManager;
            _localizationManager = localizationManager;
        }

        public async Task SynchronizeAsync(User user, AquaUserRole role)
        {
            user.SetRole(role);
            (await _userManager.UpdateAsync(user)).CheckErrors(_localizationManager);
            (await _userManager.SetRolesAsync(user, new[] { role.ToString() })).CheckErrors(_localizationManager);
        }
    }
}
