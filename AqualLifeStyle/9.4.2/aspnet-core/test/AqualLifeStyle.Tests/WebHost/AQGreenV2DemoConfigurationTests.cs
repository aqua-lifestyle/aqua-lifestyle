using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public class AQGreenV2DemoConfigurationTests
    {
        [Theory]
        [InlineData("Production", "true", "aqua_aqgreen_v2_demo", "http://127.0.0.1:3000/")]
        [InlineData("Staging", "true", "aqua_aqgreen_v2_demo", "http://127.0.0.1:3000/")]
        [InlineData("AQGreenV2Demo", "false", "aqua_aqgreen_v2_demo", "http://127.0.0.1:3000/")]
        [InlineData("AQGreenV2Demo", "true", "production", "http://127.0.0.1:3000/")]
        [InlineData("AQGreenV2Demo", "true", "aqua_aqgreen_v2_demo", "https://club.example.test/")]
        public void InvalidClassificationOrTarget_RejectsDemoCheckout(
            string environmentName, string enabled, string database, string clientRoot)
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);

            Should.Throw<InvalidOperationException>(() =>
                AQGreenV2DemoConfiguration.Validate(environment,
                    Configuration(enabled, database, clientRoot)));
        }

        [Fact]
        public void ExactDemoAndLoopbackTargets_EnableDemoCheckout()
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns("AQGreenV2Demo");

            AQGreenV2DemoConfiguration.Validate(environment,
                Configuration("true", "aqua_aqgreen_v2_demo", "http://127.0.0.1:3000/"))
                .ShouldBeTrue();
        }

        [Fact]
        public void DemoRegistration_RemovesAnAccidentallyRegisteredRealGateway()
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns("AQGreenV2Demo");
            var configuration = Configuration("true", "aqua_aqgreen_v2_demo",
                "http://127.0.0.1:3000/");
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddHttpClient<IYocoCheckoutGateway, YocoCheckoutGateway>();

            AQGreenV2DemoConfiguration.RegisterCheckoutGateway(
                services, environment, configuration);

            using var provider = services.BuildServiceProvider();
            provider.GetServices<IYocoCheckoutGateway>().Single()
                .ShouldBeOfType<AQGreenV2DemoCheckoutGateway>();
        }

        [Fact]
        public async Task DemoGateway_RejectsNonAQGreenCheckoutWithoutProvider()
        {
            var gateway = new AQGreenV2DemoCheckoutGateway(
                Configuration("true", "aqua_aqgreen_v2_demo", "http://127.0.0.1:3000/"));
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.CreateAsync(
                new CreateYocoCheckout
                {
                    ReferenceId = Guid.NewGuid(),
                    ReferenceMetadataKey = YocoCheckoutMetadata.DirectOnyxCheckoutIntentId,
                    Amount = 6120m,
                    Currency = "ZAR"
                }));
        }

        private static IConfiguration Configuration(string enabled, string database,
            string clientRoot) => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string>
            {
                ["AQGreenV2Demo:Enabled"] = enabled,
                ["ConnectionStrings:Default"] =
                    $"Host=127.0.0.1;Database={database};Username=test;Password=synthetic",
                ["App:ClientRootAddress"] = clientRoot
            }).Build();
    }
}
