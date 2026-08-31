using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Tests.Application
{
    /// <summary>
    /// Explicit test-only gate. It is not convention-registered in production;
    /// focused PostgreSQL application fixtures may replace the disabled gate
    /// with this implementation for Tenant 1 only.
    /// </summary>
    internal sealed class AQGreenWeeklySalesReviewTestGate
        : IAQGreenWeeklySalesReviewGate
    {
        public Task<bool> IsEnabledAsync(int tenantId) =>
            Task.FromResult(tenantId == 1);
    }
}
