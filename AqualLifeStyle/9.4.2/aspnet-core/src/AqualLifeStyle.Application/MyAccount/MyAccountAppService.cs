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
        public async Task<ChangeMyPasswordResult> ChangePasswordAsync(ChangeMyPasswordInput input)
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
            var isLockedOut = await UserManager.IsLockedOutAsync(user);
            if (isLockedOut)
            {
                Logger.Warn($"Failed password change attempt tenant={user.TenantId?.ToString() ?? "host"} user={user.Id} lockedOut={isLockedOut}");
                return ChangeMyPasswordResult.Failure(
                    "Your account is temporarily locked. Please try again later or contact support.");
            }

            if (!await UserManager.CheckPasswordAsync(user, input.CurrentPassword))
            {
                CheckErrors(await UserManager.AccessFailedAsync(user));

                isLockedOut = await UserManager.IsLockedOutAsync(user);
                Logger.Warn($"Failed password change attempt tenant={user.TenantId?.ToString() ?? "host"} user={user.Id} lockedOut={isLockedOut}");

                return ChangeMyPasswordResult.Failure(isLockedOut
                    ? "Your account is temporarily locked. Please try again later or contact support."
                    : "Your current password is incorrect. No changes were made.");
            }

            CheckErrors(await UserManager.ChangePasswordAsync(user, input.NewPassword));
            CheckErrors(await UserManager.UpdateSecurityStampAsync(user));
            CheckErrors(await UserManager.ResetAccessFailedCountAsync(user));
            await CurrentUnitOfWork.SaveChangesAsync();

            Logger.Info($"Account password changed tenant={user.TenantId?.ToString() ?? "host"} user={user.Id}");
            return ChangeMyPasswordResult.Success();
        }
    }
}
