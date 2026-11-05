using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Web.Host.Startup;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public sealed class AccountBootstrapBoundaryTests
    {
        private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["App:AQGreenV2:Commissioning:AdmissionMode"] = "CLEAN_START",
            ["App:AQGreenV2:Commissioning:Enabled"] = "false", [YocoCutoverOptions.Prefix + "Enabled"] = "true"
        }).Build();

        [Theory]
        [InlineData("POST", "/api/services/app/Account/Register", true)]
        [InlineData("POST", "/api/services/app/Account/ConfirmEmail", true)]
        [InlineData("POST", "/api/services/app/Account/ResendEmailVerification", true)]
        [InlineData("POST", "/api/services/app/Account/IsTenantAvailable", true)]
        [InlineData("GET", "/api/services/app/Account/GetTenantSelfRegistrationAvailability", true)]
        [InlineData("GET", "/AntiForgery/GetToken", true)]
        [InlineData("GET", "/AntiForgery/SetCookie", true)]
        [InlineData("GET", "/api/services/app/Account/Register", false)]
        [InlineData("POST", "/api/services/app/Account/RegisterExtra", false)]
        [InlineData("POST", "/api/services/app/Account/RequestPasswordReset", false)]
        [InlineData("POST", "/api/services/app/AQGreenCleanStartRoot/AuthorizeRoot", false)]
        [InlineData("POST", "/api/services/app/ClubMemberProgrammeParticipation/StartEntry", false)]
        [InlineData("POST", "/api/services/app/ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout", false)]
        [InlineData("POST", "/api/services/app/AdminProgrammeParticipation/ApproveProgrammeParticipation", false)]
        [InlineData("POST", "/api/services/app/AdminCommission/Release", false)]
        public async Task BootstrapAllowsOnlyExistingNonfinancialAccountRoutes(string method, string path, bool expected)
        {
            var context = new DefaultHttpContext(); context.Request.Method = method; context.Request.Path = path;
            context.Response.Body = new MemoryStream(); var reached = false;
            var store = Substitute.For<IYocoCutoverStore>();
            await new ApplicationRecoveryMiddleware(_ =>
            {
                reached = true;
                YocoCutoverDispatch.IsAccountBootstrapAuthorized.ShouldBeTrue();
                YocoCutoverDispatch.IsAuthorized.ShouldBeFalse(); YocoCutoverDispatch.IsBusinessAuthorized.ShouldBeFalse();
                YocoCutoverDispatch.IsReadOnlyAuthorized.ShouldBeFalse();
                return Task.CompletedTask;
            }, Config(), store).InvokeAsync(context);
            reached.ShouldBe(expected); if (!expected) context.Response.StatusCode.ShouldBe(503);
            YocoCutoverDispatch.IsAccountBootstrapAuthorized.ShouldBeFalse();
            await store.DidNotReceive().EnterBusinessAsync();
        }

        [Theory]
        [InlineData("App:AQGreenV2:Commissioning:AdmissionMode", "HISTORICAL_B6")]
        [InlineData("App:AQGreenV2:Commissioning:Enabled", "true")]
        [InlineData("App:FinancialOperations:Enabled", "true")]
        [InlineData("App:BackgroundJobs:Enabled", "true")]
        [InlineData("App:WeeklyCommissions:Enabled", "true")]
        [InlineData("App:EntryMonthlyObligations:Enabled", "true")]
        [InlineData("App:AutomaticPayouts:Enabled", "true")]
        [InlineData("App:Recovery:Enabled", "true")]
        [InlineData("AQGreenV2Pilot:Enabled", "true")]
        [InlineData("AQGreenV2Demo:Enabled", "true")]
        [InlineData("SEED_DEMO_DATA", "true")]
        public void BootstrapRequiresCleanStartAndEveryFinancialWorkerHold(string key, string value)
        {
            var config = Config(); config[key] = value;
            AQGreenAccountBootstrap.IsEnabled(config).ShouldBeFalse();
        }

        [Theory]
        [InlineData("true")][InlineData("false")]
        public void BootstrapScopeCannotInvokeBusinessOperationsEvenWithoutTheCutoverProtocol(string cutover)
        {
            var config = Config(); config[YocoCutoverOptions.Prefix + "Enabled"] = cutover;
            var interceptor = new PilotApplicationBoundaryInterceptor(DisabledAQGreenPilotMode.Instance, config);
            using (YocoCutoverDispatch.EnterAccountBootstrap())
            {
                var register = Substitute.For<IInvocation>(); register.TargetType.Returns(typeof(AccountAppService));
                register.Method.Returns(typeof(AccountAppService).GetMethod("Register"));
                interceptor.Intercept(register); register.Received(1).Proceed();
                var root = Substitute.For<IInvocation>(); root.TargetType.Returns(typeof(AQGreenCleanStartRootAppService));
                root.Method.Returns(typeof(AQGreenCleanStartRootAppService).GetMethod("AuthorizeRootAsync"));
                Should.Throw<AbpAuthorizationException>(() => interceptor.Intercept(root)); root.DidNotReceive().Proceed();
                Should.Throw<AbpAuthorizationException>(() => AQGreenAccountBootstrap.RequireProgramme("ONYX"));
                config["App:AQGreenV2:Commissioning:Enabled"] = "true";
                Should.Throw<AbpAuthorizationException>(() => interceptor.Intercept(register));
            }
        }
    }
}
