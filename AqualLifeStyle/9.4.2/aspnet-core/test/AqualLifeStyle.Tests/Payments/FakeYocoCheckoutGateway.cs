using System.Threading.Tasks;
using AqualLifeStyle.Payments.Yoco;

namespace AqualLifeStyle.Tests.Payments
{
    internal sealed class FakeYocoCheckoutGateway : IYocoCheckoutGateway
    {
        public Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout) =>
            Task.FromResult(new YocoCheckout
            {
                Id = $"checkout_{checkout.ReferenceId:N}",
                RedirectUrl = $"https://payments.example.test/checkout/{checkout.ReferenceId:N}"
            });
    }
}
