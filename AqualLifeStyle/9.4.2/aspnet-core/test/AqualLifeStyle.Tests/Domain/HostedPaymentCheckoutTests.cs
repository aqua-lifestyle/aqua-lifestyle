using System;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class HostedPaymentCheckoutTests
    {
        [Fact]
        public void AdministratorTerminationDuringPreparationCannotBeReopenedByProviderResponse()
        {
            var createdAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
            var checkout = AQGreenJoiningCheckout.Create(
                1,
                Guid.NewGuid(),
                10,
                AQGreenJoiningPaymentSchedule.Full,
                AQGreenJoiningPaymentStage.Full,
                1200m,
                "ZAR",
                createdAt);

            checkout.TerminateByAdministrator(
                42,
                createdAt.AddMinutes(1),
                "Provider support confirmed that this preparation is not payable.");

            Should.Throw<InvalidOperationException>(() => checkout.RecordCheckout(
                "ch_late_response",
                "https://payments.example.test/ch_late_response",
                createdAt.AddMinutes(2)));
            checkout.Status.ShouldBe(
                HostedPaymentCheckoutStatus.AdministrativelyTerminated);
            checkout.ProviderCheckoutId.ShouldBeNull();
        }
    }
}
