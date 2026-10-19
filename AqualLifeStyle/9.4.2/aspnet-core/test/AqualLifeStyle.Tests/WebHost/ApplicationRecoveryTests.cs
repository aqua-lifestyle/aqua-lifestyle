using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Startup;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Xunit;
using Abp.Threading.Timers;
using System.Linq;
using System.Reflection;

namespace AqualLifeStyle.Tests.WebHost
{
    public class ApplicationRecoveryTests
    {
        private static IConfiguration Config(Dictionary<string, string> extra = null)
        {
            var settings = new Dictionary<string, string> { [ApplicationRecoveryMode.EnabledSetting] = "true" };
            if (extra != null) foreach (var item in extra) settings[item.Key] = item.Value;
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        [Theory]
        [InlineData("Bird:Enabled", "true")]
        [InlineData("App:WeeklyCommissions:Enabled", "true")]
        [InlineData("App:EntryMonthlyObligations:Enabled", "true")]
        [InlineData("App:FinancialOperations:Enabled", "true")]
        [InlineData("App:BackgroundJobs:Enabled", "true")]
        [InlineData("AQGreenV2Demo:Enabled", "true")]
        [InlineData("AQGreenV2Pilot:Enabled", "true")]
        [InlineData("SEED_DEMO_DATA", "true")]
        [InlineData("Yoco:Mode", "live")]
        [InlineData("Yoco:SecretKey", "sk_live_synthetic-never-use")]
        [InlineData("Yoco:WebhookSecret", "synthetic-never-use")]
        [InlineData("Bird:ApiKey", "synthetic-never-use")]
        public void Recovery_RejectsCommissioningAndProviderSettingsBeforeStartup(string key, string value)
        {
            var environment = Substitute.For<IWebHostEnvironment>(); environment.EnvironmentName.Returns("Production");
            var exception = Should.Throw<InvalidOperationException>(() => DeploymentConfigurationValidator.Validate(
                environment, Config(new() { [key] = value })));
            exception.Message.ShouldContain(key.Replace(":", "__"));
            exception.Message.ShouldNotContain("synthetic-never-use");
        }

        [Fact]
        public void Recovery_ProductionRetainsCommonSecurityRequirementsWithoutProviders()
        {
            var environment = Substitute.For<IWebHostEnvironment>(); environment.EnvironmentName.Returns("Production");
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=synthetic;Username=synthetic;Password=synthetic",
                ["App:ServerRootAddress"] = "https://api.example.test", ["App:ClientRootAddress"] = "https://app.example.test",
                ["App:CorsOrigins"] = "https://app.example.test", ["Authentication:JwtBearer:SecurityKey"] = "synthetic-key",
                ["DataProtection:CertificateBase64"] = "synthetic-certificate", ["DataProtection:CertificatePassword"] = "synthetic-password",
                ["Redis:Configuration"] = "localhost:6379"
            };
            Should.NotThrow(() => DeploymentConfigurationValidator.Validate(environment, Config(settings)));
            settings.Remove("Authentication:JwtBearer:SecurityKey");
            Should.Throw<InvalidOperationException>(() => DeploymentConfigurationValidator.Validate(environment, Config(settings)))
                .Message.ShouldContain("Authentication__JwtBearer__SecurityKey");
        }

        [Theory]
        [InlineData("POST", "/api/payments/yoco/webhook", false)]
        [InlineData("POST", "/api/services/app/AdminCommission/ReleaseAsync", false)]
        [InlineData("POST", "/api/services/app/AdminProgrammeParticipation/ApproveProgrammeParticipation", false)]
        [InlineData("POST", "/api/services/app/Account/Register", false)]
        [InlineData("GET", "/Home/TestNotification", false)]
        [InlineData("GET", "/new-controller", false)]
        [InlineData("GET", "/api/services/app/AdminProgrammeParticipation/GetLegacyAQGreenReconciliation", true)]
        [InlineData("POST", "/api/services/app/AdminProgrammeParticipation/ExportLegacyAQGreenReconciliationEvidence", true)]
        [InlineData("GET", "/api/services/app/AdminProgrammeParticipation/ExportLegacyAQGreenReconciliationEvidence", false)]
        [InlineData("GET", "/api/health", true)]
        public async Task Recovery_HttpBoundaryDeniesMutationsAndUnknownControllers(string method, string path, bool allowed)
        {
            var reached = false; var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
            context.Request.Method = method; context.Request.Path = path;
            var middleware = new ApplicationRecoveryMiddleware(_ => { reached = true; return Task.CompletedTask; }, Config());
            await middleware.InvokeAsync(context);
            reached.ShouldBe(allowed); context.Response.Headers["X-Aqua-Recovery-Suspended"].ToString().ShouldBe("true");
            if (!allowed) context.Response.StatusCode.ShouldBe(503);
        }

        [Fact]
        public void Recovery_DirectApplicationCallsAreDeniedWhileExportStillRequiresItsNormalInterceptors()
        {
            var interceptor = new PilotApplicationBoundaryInterceptor(DisabledAQGreenPilotMode.Instance, Config());
            var invocation = Substitute.For<IInvocation>();
            invocation.TargetType.Returns(typeof(AdminCommissionAppService));
            invocation.Method.Returns(typeof(AdminCommissionAppService).GetMethod("CalculateLatestClosedWeekAsync"));
            Should.Throw<AbpAuthorizationException>(() => interceptor.Intercept(invocation)); invocation.DidNotReceive().Proceed();
            invocation.TargetType.Returns(typeof(AdminProgrammeParticipationAppService));
            invocation.Method.Returns(typeof(AdminProgrammeParticipationAppService).GetMethod("ExportLegacyAQGreenReconciliationEvidenceAsync"));
            interceptor.Intercept(invocation); invocation.Received(1).Proceed();
        }

        [Fact]
        public async Task Recovery_DirectRealCheckoutCannotReachNetworkEvenWithCredentials()
        {
            var handler = new RejectNetworkHandler();
            var gateway = new YocoCheckoutGateway(new HttpClient(handler), Config(new()
                { ["Yoco:Mode"] = "live", ["Yoco:SecretKey"] = "sk_live_synthetic-never-use" }));
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.CreateAsync(null));
            handler.Attempts.ShouldBe(0);
        }

        private sealed class RejectNetworkHandler : HttpMessageHandler
        {
            public int Attempts { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            { Attempts++; throw new InvalidOperationException("Network must not be reached."); }
        }

        [Theory]
        [InlineData(typeof(AqualLifeStyle.Web.Host.Commissions.WeeklyCommissionCalculationWorker))]
        [InlineData(typeof(AqualLifeStyle.Web.Host.ProgrammeEngine.EntryMonthlyObligationWorker))]
        [InlineData(typeof(AqualLifeStyle.Web.Host.Email.TransactionalEmailOutboxWorker))]
        public async Task Recovery_WorkersReturnBeforeDependenciesEvenWhenIndividualSwitchesAreEnabled(Type workerType)
        {
            var config = Config(new() { ["App:WeeklyCommissions:Enabled"] = "true",
                ["App:EntryMonthlyObligations:Enabled"] = "true", ["Bird:Enabled"] = "true" });
            var constructor = workerType.GetConstructors().Single();
            var worker = constructor.Invoke(constructor.GetParameters().Select(p =>
                p.ParameterType == typeof(IConfiguration) ? (object)config :
                p.ParameterType == typeof(AbpAsyncTimer) ? new AbpAsyncTimer() : null).ToArray());
            await (Task)workerType.GetMethod("DoWorkAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(worker, null);
        }

        [Fact]
        public async Task Recovery_WebhookProcessorRejectsBeforePaymentOrReceiptWrites()
        {
            var constructor = typeof(YocoPaymentNotificationProcessor).GetConstructors().Single();
            var processor = (YocoPaymentNotificationProcessor)constructor.Invoke(constructor.GetParameters()
                .Select(p => p.ParameterType == typeof(IConfiguration) ? (object)Config() : null).ToArray());
            await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(null));
        }

        [Fact]
        public async Task RecoveryDisabled_PreservesV1BoundaryWithoutEnablingV2()
        {
            var configuration = new ConfigurationBuilder().Build();
            ApplicationRecoveryMode.IsEnabled(configuration).ShouldBeFalse();
            var interceptor = new PilotApplicationBoundaryInterceptor(DisabledAQGreenPilotMode.Instance, configuration);
            var invocation = Substitute.For<IInvocation>();
            invocation.TargetType.Returns(typeof(AdminCommissionAppService));
            invocation.Method.Returns(typeof(AdminCommissionAppService).GetMethod("CalculateLatestClosedWeekAsync"));
            interceptor.Intercept(invocation); invocation.Received(1).Proceed();
            (await new DisabledAQGreenPlacementV2ApprovalGate().IsEnabledAsync(1, Guid.NewGuid())).ShouldBeFalse();
            (await new DisabledAQGreenPlacementV2ProgressGate().IsEnabledAsync(1, Guid.NewGuid())).ShouldBeFalse();
            (await new DisabledAQGreenPlacementV2JoiningGate().IsEnabledAsync(1)).ShouldBeFalse();
            (await new LegacyV1AQGreenCommissionStructuralModelSelector().SelectAsync(1, DateTime.UtcNow))
                .ShouldBe(AqualLifeStyle.Domain.Onyx.AQGreenCommissionStructuralModel.LegacyV1);
            (await new LegacyV1AQGreenGraduationStructuralModelSelector().SelectAsync(1, Guid.NewGuid()))
                .ShouldBe(AqualLifeStyle.Domain.Onyx.AQGreenGraduationStructuralModel.LegacyV1);
        }
    }
}
