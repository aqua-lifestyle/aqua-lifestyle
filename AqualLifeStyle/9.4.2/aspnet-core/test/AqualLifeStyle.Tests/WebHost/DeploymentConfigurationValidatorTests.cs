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
    /// public addresses, Redis, Yoco) or still has a placeholder value from appsettings.Production.json.
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
                ["Redis:Configuration"] = "redis:6379",
                ["Yoco:SecretKey"] = "sk_test_safe-test-placeholder",
                ["Yoco:WebhookSecret"] = "whsec_safe-test-placeholder",
                ["Yoco:Mode"] = "test",
                ["Bird:Enabled"] = "true",
                ["Bird:ApiKey"] = "bk_eu1_safe-test-placeholder",
                ["Bird:FromEmail"] = "hello@example.test",
                ["Bird:FromName"] = "Aqua Lifestyle Club",
                ["Bird:ReplyToEmail"] = "help@example.test"
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
        public void Validate_InProduction_WithRenderDatabaseUrl_DoesNotThrow()
        {
            var settings = CompleteProductionSettings();
            settings.Remove("ConnectionStrings:Default");
            settings["DATABASE_URL"] = "postgresql://user:password@postgres:5432/aqualifestyle";
            var services = BuildServiceProvider("Production", settings);

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
        public void Validate_InProduction_WithLiveModeAndTestKey_RejectsMismatch()
        {
            var settings = CompleteProductionSettings();
            settings["Yoco:Mode"] = "live";
            var services = BuildServiceProvider("Production", settings);

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("mode");
            ex.Message.ShouldContain("prefix");
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
            ex.Message.ShouldContain("Yoco__SecretKey");
            ex.Message.ShouldContain("Yoco__WebhookSecret");
            ex.Message.ShouldContain("Yoco__Mode");
            ex.Message.ShouldContain("Bird__Enabled=true");
            ex.Message.ShouldContain("Bird__ApiKey");
            ex.Message.ShouldContain("Bird__FromEmail");
        }

        [Fact]
        public void Validate_InProduction_WithMissingBirdApiKey_RedactsConfiguredSecrets()
        {
            var settings = CompleteProductionSettings();
            var secret = settings["Bird:ApiKey"];
            settings["Bird:ApiKey"] = "";
            var services = BuildServiceProvider("Production", settings);

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("Bird__ApiKey");
            ex.Message.ShouldNotContain(secret);
        }

        [Fact]
        public void Validate_InProduction_WithLegacyBirdKeyFormat_RejectsWithoutLeakingKey()
        {
            var settings = CompleteProductionSettings();
            const string secret = "legacy-bird-key-must-not-leak";
            settings["Bird:ApiKey"] = secret;
            var services = BuildServiceProvider("Production", settings);

            var ex = Should.Throw<InvalidOperationException>(
                () => DeploymentConfigurationValidator.Validate(services));

            ex.Message.ShouldContain("Bird__ApiKey");
            ex.Message.ShouldNotContain(secret);
        }
    }
}
