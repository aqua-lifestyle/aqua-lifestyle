using System;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using Abp.UI;

namespace AqualLifeStyle.Application.Admin
{
    public abstract class AdminAppServiceBase : AqualLifeStyleAppServiceBase
    {
        protected IDisposable DisableTenantFilterForHost() =>
            AbpSession.TenantId.HasValue
                ? NoopDisposable.Instance
                : CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant);

        protected void ValidateRequestedTenant(int? tenantId, string resource)
        {
            if (tenantId.HasValue && tenantId <= 0)
                throw Failed($"{resource} lookup", "TenantId must be positive.");
            if (AbpSession.TenantId.HasValue && tenantId.HasValue && tenantId != AbpSession.TenantId)
                throw new AbpAuthorizationException($"Cross-tenant {resource.ToLowerInvariant()} access is not allowed.");
        }

        protected int ResolveTargetTenant(int tenantId, string resource, string action)
        {
            if (tenantId <= 0)
                throw Failed($"{resource} {action}", "A valid tenant is required.");
            if (AbpSession.TenantId.HasValue && tenantId != AbpSession.TenantId.Value)
                throw new AbpAuthorizationException($"Cross-tenant {resource.ToLowerInvariant()} {action} is not allowed.");
            return tenantId;
        }

        protected static void ValidatePositiveId(long id, string resource)
        {
            if (id <= 0) throw Failed($"{resource} operation", $"A valid {resource.ToLowerInvariant()} Id is required.");
        }

        protected static UserFriendlyException Failed(string operation, string details) =>
            new UserFriendlyException($"{operation} failed.", details);

        protected void LogAdminMutation(string resource, string action, long targetId, int? tenantId, string justification) =>
            Logger.Info($"Admin {resource.ToLowerInvariant()} {action} actor={AbpSession.GetUserId()} tenant={tenantId} {resource.ToLowerInvariant()}={targetId} justification={SanitizeJustification(justification)}");

        protected static string SanitizeJustification(string value) =>
            (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();
            public void Dispose() { }
        }
    }
}
