using System;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Web.Host.AQGreenV2Demo
{
    /// <summary>
    /// Environment-scoped implementations used only after the dedicated demo
    /// environment and explicit opt-in have both been validated.
    /// </summary>
    public sealed class AQGreenV2DemoProgressGate
        : IAQGreenPlacementV2ProgressGate
    {
        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId) =>
            Task.FromResult(true);
    }

    public sealed class AQGreenV2DemoCommissionSelector
        : IAQGreenCommissionStructuralModelSelector
    {
        public Task<AQGreenCommissionStructuralModel> SelectAsync(
            int tenantId,
            DateTime commissionCutoffUtc) =>
            Task.FromResult(AQGreenCommissionStructuralModel.PlacementV2);
    }

    public sealed class AQGreenV2DemoSalesReviewGate
        : IAQGreenWeeklySalesReviewGate
    {
        public Task<bool> IsEnabledAsync(int tenantId) => Task.FromResult(true);
    }
}
