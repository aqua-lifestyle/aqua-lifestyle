using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Runtime.Security;
using AqualLifeStyle.Authorization.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace AqualLifeStyle.Authentication.JwtBearer
{
    public static class JwtSessionSecurityStampValidator
    {
        public const string SecurityStampClaimType = "security_stamp";

        public static async Task ValidateAsync(TokenValidatedContext context)
        {
            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var securityStamp = context.Principal?.FindFirstValue(SecurityStampClaimType);
            var tenantIdValue = context.Principal?.FindFirstValue(AbpClaimTypes.TenantId);
            if (!long.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
            {
                context.Fail("This session is no longer valid.");
                return;
            }

            int? tenantId = null;
            if (!string.IsNullOrWhiteSpace(tenantIdValue))
            {
                if (!int.TryParse(tenantIdValue, out var parsedTenantId))
                {
                    context.Fail("This session is no longer valid.");
                    return;
                }
                tenantId = parsedTenantId;
            }

            var services = context.HttpContext.RequestServices;
            var unitOfWorkManager = services.GetRequiredService<IUnitOfWorkManager>();
            var userManager = services.GetRequiredService<UserManager>();
            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                await userManager.InitializeOptionsAsync(tenantId);
                var user = await userManager.FindByIdAsync(userId.ToString());
                if (user == null || !user.IsActive || user.IsDeleted || user.RequiresPasswordReset() ||
                    !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
                {
                    context.Fail("This session is no longer valid.");
                    return;
                }

                await unitOfWork.CompleteAsync();
            }
        }
    }
}
