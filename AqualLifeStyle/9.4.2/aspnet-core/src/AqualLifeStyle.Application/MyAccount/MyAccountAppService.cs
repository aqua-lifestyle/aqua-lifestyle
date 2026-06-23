using System;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Application.MyAccount.Dto;

namespace AqualLifeStyle.Application.MyAccount
{
    [AbpAuthorize]
    [Audited]
    public class MyAccountAppService : AqualLifeStyleAppServiceBase, IMyAccountAppService
    {
        [DisableAuditing]
        public async Task ChangePasswordAsync(ChangeMyPasswordInput input)
        {
            if (input == null)
            {
                throw new UserFriendlyException("Password change failed.", "The request was empty.");
            }

            if (string.Equals(input.CurrentPassword, input.NewPassword, StringComparison.Ordinal))
            {
                throw new UserFriendlyException(
                    "Password change failed.",
                    "Choose a new password that is different from your current password.");
            }

            await UserManager.InitializeOptionsAsync(AbpSession.TenantId);
            var user = await GetCurrentUserAsync();
            if (!await UserManager.CheckPasswordAsync(user, input.CurrentPassword))
            {
                throw new UserFriendlyException(
                    "Password change failed.",
                    "Your current password is incorrect. No changes were made.");
            }

            CheckErrors(await UserManager.ChangePasswordAsync(user, input.NewPassword));
            CheckErrors(await UserManager.UpdateSecurityStampAsync(user));
            await CurrentUnitOfWork.SaveChangesAsync();

            Logger.Info($"Account password changed tenant={user.TenantId?.ToString() ?? "host"} user={user.Id}");
        }
    }
}
