using System;
using System.Threading.Tasks;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Web.Host.AQGreenV2Pilot
{
    // No provider call, signature claim or actual money. The URL is a test-money
    // representation; confirmation remains a separately authorized application action.
    public sealed class PilotCheckoutGateway : IYocoCheckoutGateway
    {
        private readonly IAQGreenPilotMode _mode;
        public PilotCheckoutGateway(IAQGreenPilotMode mode) => _mode = mode;
        public Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout)
        {
            _mode.RequirePilot();
            if (checkout == null || checkout.ReferenceId == Guid.Empty || checkout.Purpose != "AQGreenJoining")
                throw new InvalidOperationException("Only a recorded AQGreen joining checkout is allowed in the pilot.");
            return Task.FromResult(new YocoCheckout
            {
                Id = "PILOT-TEST-" + checkout.ReferenceId.ToString("N"),
                RedirectUrl = "https://pilot.invalid/test-money/" + checkout.ReferenceId.ToString("D")
            });
        }
    }
}
