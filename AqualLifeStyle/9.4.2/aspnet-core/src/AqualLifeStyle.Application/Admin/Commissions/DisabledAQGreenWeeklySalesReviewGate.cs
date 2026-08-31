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
    /// The Area ownership rule is unresolved. The current policy therefore
    /// admits host-side callers only; authorization separately requires the
    /// dedicated review permission and AllTenants.
    /// </summary>
    public sealed class HostOnlyAQGreenWeeklySalesReviewScopePolicy
        : IAQGreenWeeklySalesReviewScopePolicy, ITransientDependency
    {
        private readonly Abp.Runtime.Session.IAbpSession _session;

        public HostOnlyAQGreenWeeklySalesReviewScopePolicy(
            Abp.Runtime.Session.IAbpSession session)
        {
            _session = session;
        }

        public Task<bool> CanReviewAsync(int tenantId) =>
            Task.FromResult(!_session.TenantId.HasValue && tenantId > 0);
    }
}
