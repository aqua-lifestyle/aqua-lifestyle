using System.Threading.Tasks;
using System;
using Abp.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.AQGreenV2Pilot
{
    public sealed class AQGreenV2PilotMode : IAQGreenPilotMode
    {
        public bool IsEnabled { get; }
        public AQGreenV2PilotMode(IWebHostEnvironment environment, IConfiguration configuration)
            => IsEnabled = AQGreenV2PilotConfiguration.Validate(environment, configuration);
        public void DenyOutsidePilot(string capability)
        {
            if (IsEnabled) throw new AbpAuthorizationException(
                capability + " is closed in the isolated TEST-MONEY AQGreen pilot.");
        }
        public void RequirePilot()
        {
            if (!IsEnabled) throw new AbpAuthorizationException("This operation is available only in the isolated TEST-MONEY pilot.");
        }
    }

    public class AQGreenV2PilotSelectors : IAQGreenPlacementV2ApprovalGate,
        IAQGreenPlacementV2ProgressGate, IAQGreenWeeklySalesReviewGate,
        IAQGreenCommissionStructuralModelSelector
    {
        private readonly IAQGreenPilotMode _mode;
        public AQGreenV2PilotSelectors(IAQGreenPilotMode mode) => _mode = mode;
        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId)
        {
            _mode.RequirePilot();
            // Null is the existing authorized host-session scope, not a missing
            // participant Tenant. Approval resolves and checks that Tenant itself.
            if (tenantId.HasValue && tenantId.Value <= 0 || participantId == Guid.Empty)
                throw new ArgumentException("A valid session scope and exact participant are required.");
            return Task.FromResult(true);
        }
        public Task<bool> IsEnabledAsync(int tenantId)
        {
            _mode.RequirePilot();
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            return Task.FromResult(true);
        }
        public Task<AQGreenCommissionStructuralModel> SelectAsync(int tenantId, DateTime commissionCutoffUtc)
        {
            _mode.RequirePilot();
            if (tenantId <= 0 || commissionCutoffUtc == default || commissionCutoffUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("An exact Tenant and UTC cutoff are required.");
            return Task.FromResult(AQGreenCommissionStructuralModel.PlacementV2);
        }
    }
    public sealed class AQGreenV2PilotApprovalGate : AQGreenV2PilotSelectors
    { public AQGreenV2PilotApprovalGate(IAQGreenPilotMode mode) : base(mode) { } }
    public sealed class AQGreenV2PilotProgressGate : AQGreenV2PilotSelectors
    { public AQGreenV2PilotProgressGate(IAQGreenPilotMode mode) : base(mode) { } }
    public sealed class AQGreenV2PilotCommissionSelector : AQGreenV2PilotSelectors
    { public AQGreenV2PilotCommissionSelector(IAQGreenPilotMode mode) : base(mode) { } }
    public sealed class AQGreenV2PilotSalesReviewGate : AQGreenV2PilotSelectors
    { public AQGreenV2PilotSalesReviewGate(IAQGreenPilotMode mode) : base(mode) { } }
}
