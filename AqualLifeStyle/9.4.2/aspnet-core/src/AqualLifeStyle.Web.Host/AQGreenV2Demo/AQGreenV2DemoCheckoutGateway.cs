using System;
using System.Threading.Tasks;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.AQGreenV2Demo
{
    /// <summary>No network or provider client. Registered only inside the validated demo host.</summary>
    public sealed class AQGreenV2DemoCheckoutGateway : IYocoCheckoutGateway
    {
        private readonly IConfiguration _configuration;

        public AQGreenV2DemoCheckoutGateway(IConfiguration configuration) =>
            _configuration = configuration;

        public Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout)
        {
            if (checkout == null || checkout.ReferenceId == Guid.Empty ||
                checkout.ReferenceMetadataKey != YocoCheckoutMetadata.AQGreenJoiningCheckoutId ||
                checkout.Amount <= 0 || checkout.Currency != "ZAR")
                throw new InvalidOperationException(
                    "Only AQGreen joining test checkouts are supported in the demo.");

            var client = AQGreenV2DemoConfiguration.ValidateLoopbackClient(
                _configuration["App:ClientRootAddress"]);
            return Task.FromResult(new YocoCheckout
            {
                Id = $"aqgreen_v2_demo_{checkout.ReferenceId:N}",
                RedirectUrl = new Uri(client, "test/aqgreen-checkout").ToString()
            });
        }
    }
}
