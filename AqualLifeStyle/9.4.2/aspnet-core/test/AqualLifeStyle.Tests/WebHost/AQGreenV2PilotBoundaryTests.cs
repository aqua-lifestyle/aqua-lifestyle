using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.AQGreenV2Pilot;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;

namespace AqualLifeStyle.Tests.WebHost
{
    public class AQGreenV2PilotBoundaryTests
    {
        [Theory]
        [InlineData("Production", true)]
        [InlineData("Development", true)]
        [InlineData("AQGreenV2Pilot", false)]
        public void EnvironmentAndOptInMustMatch(string environment, bool enabled)
        {
            var settings = Settings(); settings["AQGreenV2Pilot:Enabled"] = enabled.ToString();
            Should.Throw<InvalidOperationException>(() => Mode(settings, environment));
        }
        [Theory]
        [InlineData("ConnectionStrings:Default", "Host=example.test;Database=aqua_aqgreen_v2_pilot;Username=test")]
        [InlineData("ConnectionStrings:Default", "Host=localhost;Database=production;Username=test")]
        [InlineData("AQGreenV2Demo:Enabled", "true")]
        [InlineData("Bird:ApiKey", "non-secret-test-marker")]
        [InlineData("Yoco:SecretKey", "non-secret-test-marker")]
        [InlineData("Yoco:WebhookSecret", "non-secret-test-marker")]
        [InlineData("Bird:Enabled", "true")]
        [InlineData("App:WeeklyCommissions:Enabled", "true")]
        [InlineData("App:EntryMonthlyObligations:Enabled", "true")]
        [InlineData("App:ClientRootAddress", "https://example.test/")]
        [InlineData("App:ClientRootAddress", "file://localhost/tmp")]
        [InlineData("Redis:Configuration", "localhost:6379")]
        public void ProductionAndExternalCapabilitiesCannotEnterPilot(string key, string value)
        {
            var settings = Settings(); settings[key] = value;
            Should.Throw<InvalidOperationException>(() => Mode(settings));
        }
        [Fact]
        public async Task ExactPilotSelectsOnlyV2AndTestJoiningCheckout()
        {
            var mode = Mode(Settings());
            mode.IsEnabled.ShouldBeTrue();
            Should.Throw<AbpAuthorizationException>(() => mode.DenyOutsidePilot("Payout"));
            var selectors = new AQGreenV2PilotSelectors(mode);
            (await selectors.IsEnabledAsync(1, Guid.NewGuid())).ShouldBeTrue();
            (await selectors.SelectAsync(1, DateTime.UtcNow)).ShouldBe(AQGreenCommissionStructuralModel.PlacementV2);
            var gateway = new PilotCheckoutGateway(mode);
            var id = Guid.NewGuid();
            var checkout = await gateway.CreateAsync(new CreateYocoCheckout { ReferenceId = id, Purpose = "AQGreenJoining" });
            checkout.Id.ShouldBe("PILOT-TEST-" + id.ToString("N"));
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.CreateAsync(new CreateYocoCheckout { ReferenceId = id, Purpose = "DirectOnyx" }));
        }
        [Fact]
        public async Task DisabledPilotCannotEnableSelectorsOrTestGateway()
        {
            var selectors = new AQGreenV2PilotSelectors(DisabledAQGreenPilotMode.Instance);
            await Should.ThrowAsync<InvalidOperationException>(() => selectors.SelectAsync(1, DateTime.UtcNow));
            await Should.ThrowAsync<InvalidOperationException>(() => new PilotCheckoutGateway(DisabledAQGreenPilotMode.Instance)
                .CreateAsync(new CreateYocoCheckout { ReferenceId = Guid.NewGuid(), Purpose = "AQGreenJoining" }));
        }
        [Fact]
        public void TestMoneyDatabaseCannotBeReopenedAsProduction()
        {
            var settings = Settings(); settings["AQGreenV2Pilot:Enabled"] = "false";
            Should.Throw<InvalidOperationException>(() => Mode(settings, "Production"));
        }
        [Fact]
        public async Task PilotVersionSelectionRejectsUnknownScopeAndCutoff()
        {
            var selectors = new AQGreenV2PilotSelectors(Mode(Settings()));
            (await selectors.IsEnabledAsync(null, Guid.NewGuid())).ShouldBeTrue(); // Authorized host session.
            await Should.ThrowAsync<ArgumentException>(() => selectors.IsEnabledAsync(0, Guid.NewGuid()));
            await Should.ThrowAsync<ArgumentException>(() => selectors.IsEnabledAsync(1, Guid.Empty));
            await Should.ThrowAsync<ArgumentOutOfRangeException>(() => selectors.IsEnabledAsync(0));
            await Should.ThrowAsync<ArgumentException>(() => selectors.SelectAsync(1, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)));
        }
        private static AQGreenV2PilotMode Mode(Dictionary<string, string> settings, string name = "AQGreenV2Pilot")
        {
            var environment = Substitute.For<IWebHostEnvironment>(); environment.EnvironmentName.Returns(name);
            return new AQGreenV2PilotMode(environment, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        }
        private static Dictionary<string, string> Settings() => new()
        {
            ["AQGreenV2Pilot:Enabled"] = "true",
            ["ConnectionStrings:Default"] = "Host=127.0.0.1;Database=aqua_aqgreen_v2_pilot;Username=test",
            ["App:ClientRootAddress"] = "http://127.0.0.1:3000/",
            ["App:ServerRootAddress"] = "http://127.0.0.1:5000/"
        };
    }
}
