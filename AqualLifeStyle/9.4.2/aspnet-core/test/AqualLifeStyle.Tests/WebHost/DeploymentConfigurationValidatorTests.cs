using System;
using System.Collections.Generic;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace AqualLifeStyle.Tests.WebHost
{
    /// <summary>
    /// Covers <see cref="DeploymentConfigurationValidator"/>, which fails fast at startup when a
    /// Production deployment is missing required configuration (connection string, JWT key,
    /// public addresses, Redis) or still has a placeholder value from appsettings.Production.json.
    /// </summary>
    public class DeploymentConfigurationValidatorTests
    {
        private static IServiceProvider BuildServiceProvider(string environmentName, Dictionary<string, string> settings)
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IWebHostEnvironment>(environment);
            services.AddSingleton<IConfiguration>(configuration);
            return services.BuildServiceProvider();
        }

        private static Dictionary<string, string> CompleteProductionSettings()
        {
            return new Dictionary<string, string>
            {
                ["ConnectionStrings:Default"] = "Host=host;Database=db;Username=user;Password=pass;",
                ["App:ServerRootAddress"] = "https://api.example.com/",
                ["App:ClientRootAddress"] = "https://app.example.com/",
                ["App:CorsOrigins"] = "https://app.example.com",
                ["Authentication:JwtBearer:SecurityKey"] = "a-real-secret-key",
                ["Redis:Configuration"] = "redis:6379"
            };
        }

        [Fact]
        public void Validate_InDevelopment_DoesNotThrow_EvenWithoutAnySettings()
        {
            var services = BuildServiceProvider("Development", new Dictionary<string, string>());

            Should.NotThrow(() => DeploymentConfigurationValidator.Validate(services));
        }

        [Fact]
        public void Validate_InStaging_DoesNotThrow()
        {
            var services = BuildServiceProvider("Staging", new Dictionary<string, string>());

            Should.NotThrow(() => DeploymentConfigurationValidator.Validate(services));
        }

        [Fact]
        public void Validate_InProduction_WithAllSettingsPresent_DoesNotThrow()
        {
            var services = BuildServiceProvider("Production", CompleteProductionSettings());

            Should.NotThrow(() => DeploymentConfigurationValidator.Validate(services));
        }

        [Fact]
        public void Validate_InProduction_WithMissingConnectionString_ThrowsWithDescriptiveMessage()
        {
            var settings = CompleteProductionSettings();
            settings.Remove("ConnectionStrings:Default");
            var services = BuildServiceProvider("Production", settings);

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("ConnectionStrings__Default or DATABASE_URL");
        }

        [Fact]
        public void Validate_InProduction_WithPlaceholderValue_TreatsSettingAsMissing()
        {
            var settings = CompleteProductionSettings();
            settings["Authentication:JwtBearer:SecurityKey"] =
                "<set-via-user-secret-or-env:Authentication__JwtBearer__SecurityKey>";
            var services = BuildServiceProvider("Production", settings);

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("Authentication__JwtBearer__SecurityKey");
        }

        [Fact]
        public void Validate_InProduction_WithBlankValue_TreatsSettingAsMissing()
        {
            var settings = CompleteProductionSettings();
            settings["Redis:Configuration"] = "   ";
            var services = BuildServiceProvider("Production", settings);

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("Redis__Configuration");
        }

        [Fact]
        public void Validate_InProduction_WithNothingConfigured_ListsEveryMissingSetting()
        {
            var services = BuildServiceProvider("Production", new Dictionary<string, string>());

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("ConnectionStrings__Default or DATABASE_URL");
            ex.Message.ShouldContain("App__ServerRootAddress");
            ex.Message.ShouldContain("App__ClientRootAddress");
            ex.Message.ShouldContain("App__CorsOrigins");
            ex.Message.ShouldContain("Authentication__JwtBearer__SecurityKey");
            ex.Message.ShouldContain("Redis__Configuration");
        }
    }
}