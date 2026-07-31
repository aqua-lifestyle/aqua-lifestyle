using System;
using System.Linq;
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
            var expectedRoleName = role.ToString();
            var assignedRoles = await _userManager.GetRolesAsync(user);
            var authorityChanged = user.Role != role ||
                assignedRoles.Count != 1 ||
                !assignedRoles.Contains(expectedRoleName, StringComparer.OrdinalIgnoreCase);

            user.SetRole(role);
            (await _userManager.UpdateAsync(user)).CheckErrors(_localizationManager);
            (await _userManager.SetRolesAsync(user, new[] { expectedRoleName })).CheckErrors(_localizationManager);
            if (authorityChanged)
                (await _userManager.UpdateSecurityStampAsync(user)).CheckErrors(_localizationManager);
        }
    }
}
