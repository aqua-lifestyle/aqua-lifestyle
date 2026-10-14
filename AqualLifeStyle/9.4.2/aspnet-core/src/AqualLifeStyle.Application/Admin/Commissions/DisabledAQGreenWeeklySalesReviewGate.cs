using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public sealed class DisabledAQGreenWeeklySalesReviewGate
        : IAQGreenWeeklySalesReviewGate, ISingletonDependency
    {
        public Task<bool> IsEnabledAsync(int tenantId) => Task.FromResult(false);
    }

    public interface IAQGreenWeeklySalesReviewScopePolicy
    {
        Task<bool> CanReviewAsync(int tenantId);
    }

    /// <summary>
    /// Tenant callers cannot cross Tenant boundaries. The service separately
    /// enforces the review permission and active Area assignments; host callers
    /// additionally require the existing AllTenants grant.
    /// </summary>
    public sealed class TenantBoundAQGreenWeeklySalesReviewScopePolicy
        : IAQGreenWeeklySalesReviewScopePolicy, ITransientDependency
    {
        private readonly Abp.Runtime.Session.IAbpSession _session;

        public TenantBoundAQGreenWeeklySalesReviewScopePolicy(
            Abp.Runtime.Session.IAbpSession session)
        {
            _session = session;
        }

        public Task<bool> CanReviewAsync(int tenantId) =>
            Task.FromResult(tenantId > 0 &&
                (!_session.TenantId.HasValue || _session.TenantId == tenantId));
    }
}
