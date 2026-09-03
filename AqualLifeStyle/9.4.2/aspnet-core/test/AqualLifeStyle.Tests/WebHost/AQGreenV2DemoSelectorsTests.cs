using System;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using Shouldly;

namespace AqualLifeStyle.Tests.WebHost
{
    public class AQGreenV2DemoSelectorsTests
    {
        [Fact]
        public async Task DedicatedDemoSelectors_EnableOnlyTheRequiredV2Seams()
        {
            var participantId = Guid.NewGuid();

            (await new AQGreenV2DemoProgressGate()
                .IsEnabledAsync(1, participantId)).ShouldBeTrue();
            (await new AQGreenV2DemoSalesReviewGate()
                .IsEnabledAsync(1)).ShouldBeTrue();
            (await new AQGreenV2DemoCommissionSelector()
                .SelectAsync(1, DateTime.UtcNow)).ShouldBe(
                    AQGreenCommissionStructuralModel.PlacementV2);
        }
    }
}
