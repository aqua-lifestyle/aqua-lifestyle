using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abp.Threading.Timers;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Email;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Web.Host.Commissions;
using AqualLifeStyle.Web.Host.Email;
using AqualLifeStyle.Web.Host.Payments.Yoco;
using AqualLifeStyle.Web.Host.ProgrammeEngine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using AqualLifeStyle.Web.Host.AQGreenV2Pilot;
using Abp.Authorization;

namespace AqualLifeStyle.Tests.WebHost
{
    public class RuntimeAuthorityTests
    {
        private static IConfiguration Configuration(string deniedSetting)
        {
            var settings = new Dictionary<string, string>
            {
                ["App:Recovery:Enabled"] = "false",
                ["App:BackgroundJobs:Enabled"] = "true",
                ["App:FinancialOperations:Enabled"] = "true",
                ["App:WeeklyCommissions:Enabled"] = "true",
                ["App:EntryMonthlyObligations:Enabled"] = "true",
                ["Bird:Enabled"] = "true",
                ["Bird:DeliveryNotBeforeUtc"] = "1970-01-01T00:00:00Z"
            };
            settings[deniedSetting] = deniedSetting == ApplicationRecoveryMode.EnabledSetting ? "true" : "false";
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        [Theory]
        [InlineData(typeof(TransactionalEmailOutboxWorker))]
        [InlineData(typeof(YocoPaymentOperationsMonitor))]
        [InlineData(typeof(WeeklyCommissionCalculationWorker))]
        [InlineData(typeof(EntryMonthlyObligationWorker))]
        public async Task DisabledBackgroundExecution_HoldsEachIndependentWorkerAcrossFreshInstances(Type workerType)
        {
            for (var restart = 0; restart < 3; restart++)
            {
                var configuration = Configuration("App:BackgroundJobs:Enabled");
                var worker = Construct(workerType, configuration);
                // All effectful dependencies are absent. A held worker must return
                // before touching any of them, including on its RunOnStart path.
                await InvokeWorkerAsync(workerType, worker);
            }
        }

        [Theory]
        [InlineData(typeof(WeeklyCommissionCalculationWorker))]
        [InlineData(typeof(EntryMonthlyObligationWorker))]
        public async Task IndividualFinancialFlag_CannotOverrideGlobalFinancialHold(Type workerType)
        {
            await InvokeWorkerAsync(workerType, Construct(workerType, Configuration("App:FinancialOperations:Enabled")));
        }

        [Fact]
        public async Task Recovery_HoldsDirectMonitorExecution()
        {
            await InvokeWorkerAsync(typeof(YocoPaymentOperationsMonitor),
                Construct(typeof(YocoPaymentOperationsMonitor), Configuration(ApplicationRecoveryMode.EnabledSetting)));
        }

        [Theory]
        [InlineData(typeof(TransactionalEmailOutboxWorker))]
        [InlineData(typeof(YocoPaymentOperationsMonitor))]
        [InlineData(typeof(WeeklyCommissionCalculationWorker))]
        [InlineData(typeof(EntryMonthlyObligationWorker))]
        public async Task PilotAuthorizationDenial_IsPreservedEvenWhenBackgroundExecutionIsHeld(Type workerType)
        {
            var configuration = PilotConfiguration();
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(AQGreenV2PilotConfiguration.EnvironmentName);
            var worker = Construct(workerType, configuration);
            workerType.GetProperty("PilotMode").SetValue(worker,
                new AQGreenV2PilotMode(environment, configuration));
            await Should.ThrowAsync<AbpAuthorizationException>(() => InvokeWorkerAsync(workerType, worker));
        }

        [Fact]
        public async Task PilotAuthorizationDenial_IsPreservedBeforeMonthlySchedulingFinancialHold()
        {
            var configuration = PilotConfiguration();
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(AQGreenV2PilotConfiguration.EnvironmentName);
            var scheduler = (EntryMonthlyObligationScheduler)Construct(typeof(EntryMonthlyObligationScheduler), configuration);
            scheduler.PilotMode = new AQGreenV2PilotMode(environment, configuration);
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                scheduler.EnsureObligationsForPeriodAsync(2026, 10, DateTime.UtcNow, "synthetic"));
            await Should.ThrowAsync<AbpAuthorizationException>(() => scheduler.AssessObligationsAsync(DateTime.UtcNow));
        }

        private static IConfiguration PilotConfiguration() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AQGreenV2Pilot:Enabled"] = "true",
                ["ConnectionStrings:Default"] = "Host=localhost;Database=aqua_aqgreen_v2_pilot;Username=synthetic",
                ["App:ClientRootAddress"] = "http://localhost:3000",
                ["App:ServerRootAddress"] = "http://localhost:21021"
            }).Build();

        [Theory]
        [InlineData("App:BackgroundJobs:Enabled")]
        [InlineData("Bird:Enabled")]
        [InlineData(ApplicationRecoveryMode.EnabledSetting)]
        public async Task DirectOutboxExecution_RejectsBeforeAnyClaimOrProviderDependency(string heldSetting)
        {
            var processor = (TransactionalEmailOutboxProcessor)Construct(
                typeof(TransactionalEmailOutboxProcessor), Configuration(heldSetting));
            await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessPendingAsync());
        }

        [Theory]
        [InlineData("App:BackgroundJobs:Enabled")]
        [InlineData("Bird:Enabled")]
        [InlineData(ApplicationRecoveryMode.EnabledSetting)]
        public async Task ProviderGateway_RejectsAbsentExecutionAuthorityEvenWithAConfiguredKey(string heldSetting)
        {
            var handler = new CountingProvider();
            var configuration = Configuration(heldSetting);
            for (var restart = 0; restart < 3; restart++)
            {
                var gateway = (BirdTransactionalEmailDeliveryGateway)Construct(
                    typeof(BirdTransactionalEmailDeliveryGateway), configuration,
                    new HttpClient(handler, disposeHandler: false));
                await Should.ThrowAsync<InvalidOperationException>(() => gateway.SendAsync(
                    new TransactionalEmail("synthetic@example.test", "Synthetic", "Synthetic", "Synthetic", "held")));
            }
            handler.Attempts.ShouldBe(0);
        }

        [Fact]
        public async Task MissingConfiguration_DoesNotGrantWorkerOrDeliveryAuthority()
        {
            var empty = new ConfigurationBuilder().Build();
            foreach (var worker in new[] { typeof(TransactionalEmailOutboxWorker), typeof(YocoPaymentOperationsMonitor),
                typeof(WeeklyCommissionCalculationWorker), typeof(EntryMonthlyObligationWorker) })
                await InvokeWorkerAsync(worker, Construct(worker, empty));
            var handler = new CountingProvider();
            var gateway = (BirdTransactionalEmailDeliveryGateway)Construct(
                typeof(BirdTransactionalEmailDeliveryGateway), empty, new HttpClient(handler));
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.SendAsync(
                new TransactionalEmail("synthetic@example.test", "Synthetic", "Synthetic", "Synthetic", "held")));
            handler.Attempts.ShouldBe(0);
        }

        [Fact]
        public async Task FinancialHold_RejectsLegacyReferralAwardsBeforeTheHandlerOpensATransaction()
        {
            var handler = (EnquiryConvertedEventHandler)Construct(typeof(EnquiryConvertedEventHandler),
                Configuration("App:FinancialOperations:Enabled"));
            await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleEventAsync(
                new EnquiryConvertedEvent(1, 1, 1, 1, DateTime.UtcNow, tenantId: 1)));
        }

        [Fact]
        public async Task FinancialHold_RejectsLivePaymentNotificationBeforeDependencies()
        {
            var configuration = Configuration("App:FinancialOperations:Enabled");
            configuration["Yoco:Mode"] = "live";
            var processor = (YocoPaymentNotificationProcessor)Construct(
                typeof(YocoPaymentNotificationProcessor), configuration);
            await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(null));
        }

        [Fact]
        public async Task FinancialHold_RejectsLiveCheckoutWithCredentialsBeforeProviderRequest()
        {
            var configuration = Configuration("App:FinancialOperations:Enabled");
            configuration["Yoco:Mode"] = "live";
            configuration["Yoco:SecretKey"] = "sk_live_synthetic-no-network";
            var handler = new CountingProvider();
            var gateway = new YocoCheckoutGateway(new HttpClient(handler), configuration);
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.CreateAsync(new CreateYocoCheckout
            {
                ReferenceId = Guid.NewGuid(), ReferenceMetadataKey = YocoCheckoutMetadata.DirectOnyxCheckoutIntentId,
                Purpose = "OnyxDirectEntry", Amount = 6120m, Currency = "ZAR", Description = "Synthetic",
                SuccessUrl = "https://app.example.test/success", CancelUrl = "https://app.example.test/cancel",
                FailureUrl = "https://app.example.test/failure"
            }));
            handler.Attempts.ShouldBe(0);
        }

        [Fact]
        public async Task DirectFinancialServices_CannotBypassTheHoldAtAWorkerOrWebhook()
        {
            var configuration = Configuration("App:FinancialOperations:Enabled");
            var calculator = (WeeklyCommissionCalculator)Construct(typeof(WeeklyCommissionCalculator), configuration);
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => calculator.CalculateEntryAsync(1, null, DateTime.UtcNow));
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => calculator.CalculateOnyxAsync(1, null, DateTime.UtcNow));
            var scheduler = (EntryMonthlyObligationScheduler)Construct(typeof(EntryMonthlyObligationScheduler), configuration);
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => scheduler.EnsureObligationsForPeriodAsync(2026, 10, DateTime.UtcNow, "synthetic"));
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => scheduler.AssessObligationsAsync(DateTime.UtcNow));
            var travel = (OnyxTravelBenefitSynchronizer)Construct(typeof(OnyxTravelBenefitSynchronizer), configuration);
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => travel.SynchronizeAsync(1, null, DateTime.UtcNow));
            var processor = (ProgrammePaymentConfirmationProcessor)Construct(typeof(ProgrammePaymentConfirmationProcessor), configuration);
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => processor.ProcessAsync(null));
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => processor.ProcessDirectOnyxCheckoutAsync(Guid.NewGuid(), "synthetic", "synthetic", "synthetic", 1m, "ZAR", DateTime.UtcNow));
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => processor.ProcessAQGreenJoiningCheckoutAsync(Guid.NewGuid(), "synthetic", "synthetic", "synthetic", 1m, "ZAR", DateTime.UtcNow));
            await Should.ThrowAsync<RuntimeOperationDeniedException>(() => processor.ProcessAQGreenMonthlyObligationCheckoutAsync(Guid.NewGuid(), "synthetic", "synthetic", "synthetic", 1m, "ZAR", DateTime.UtcNow));
        }

        [Theory]
        [InlineData("Production", "Host=localhost;Database=aqua_aqgreen_v2_demo;Username=synthetic")]
        [InlineData("AQGreenV2Demo", "Host=database.example.test;Database=aqua_aqgreen_v2_demo;Username=synthetic")]
        [InlineData("AQGreenV2Demo", "Host=localhost;Database=ordinary_database;Username=synthetic")]
        public void DemoCalculation_CannotGrantAnExemptionFromAnOrdinaryOrRemoteHost(string environmentName, string connection)
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);
            var configuration = Configuration("App:FinancialOperations:Enabled");
            configuration["AQGreenV2Demo:Enabled"] = "true";
            configuration["ConnectionStrings:Default"] = connection;
            configuration["App:ClientRootAddress"] = "http://localhost:3000";
            Should.Throw<InvalidOperationException>(() => new AQGreenV2DemoCalculationMode(environment, configuration));
        }

        [Fact]
        public void DemoCalculation_RequiresAndRechecksTheDisposableComposition()
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns("AQGreenV2Demo");
            var configuration = Configuration("App:FinancialOperations:Enabled");
            configuration["AQGreenV2Demo:Enabled"] = "true";
            configuration["ConnectionStrings:Default"] = "Host=localhost;Database=aqua_aqgreen_v2_demo;Username=synthetic";
            configuration["App:ClientRootAddress"] = "http://localhost:3000";
            var mode = new AQGreenV2DemoCalculationMode(environment, configuration);
            mode.IsEnabled.ShouldBeTrue();
            configuration["ConnectionStrings:Default"] = "Host=remote.example.test;Database=aqua_aqgreen_v2_demo;Username=synthetic";
            Should.Throw<InvalidOperationException>(() => _ = mode.IsEnabled);
        }

        private static object Construct(Type type, IConfiguration configuration, HttpClient http = null)
            => type.GetConstructors().Single().Invoke(type.GetConstructors().Single().GetParameters().Select(p =>
                p.ParameterType == typeof(IConfiguration) ? (object)configuration :
                p.ParameterType == typeof(AbpAsyncTimer) ? new AbpAsyncTimer() :
                p.ParameterType == typeof(HttpClient) ? http :
                p.ParameterType == typeof(IOptions<BirdOptions>) ? Options.Create(new BirdOptions
                { Enabled = true, ApiKey = "bk_eu1_synthetic-no-network", FromEmail = "synthetic@example.test" }) : null).ToArray());

        private static Task InvokeWorkerAsync(Type type, object worker)
            => (Task)type.GetMethod("DoWorkAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(worker, null);

        private sealed class CountingProvider : HttpMessageHandler
        {
            public int Attempts { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Attempts++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
                { Content = new StringContent("{\"id\":\"synthetic-acceptance\"}") });
            }
        }
    }
}
