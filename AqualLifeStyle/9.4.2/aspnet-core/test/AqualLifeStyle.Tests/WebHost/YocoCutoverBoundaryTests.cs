using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.AspNetCore.Mvc.Authorization;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Startup;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public sealed class YocoCutoverBoundaryTests
    {
        private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string>
            { [YocoCutoverOptions.Prefix+"Enabled"]="true",[YocoCutoverOptions.Prefix+"OperatorUserId"]="7" }).Build();
        [Fact]
        public void FullBackendStillRequiresPinnedOperator()
        {
            var c=Config();c[YocoCutoverOptions.Prefix+"OperatorUserId"]=null;
            Should.Throw<InvalidOperationException>(()=>new YocoCutoverOptions(c)).Message.ShouldBe("YOCO_CUTOVER_OPERATOR_REQUIRED");
        }
        [Fact]
        public void EnabledFenceAndOperatorCannotBeRemovedByConfigurationReload()
        {
            var c=Config();new YocoCutoverOptions(c).Enabled.ShouldBeTrue();c[YocoCutoverOptions.Prefix+"Enabled"]="false";
            new YocoCutoverOptions(c).Enabled.ShouldBeTrue();c[YocoCutoverOptions.Prefix+"OperatorUserId"]="8";
            Should.Throw<InvalidOperationException>(()=>new YocoCutoverOptions(c));
        }
        [Theory]
        [InlineData("App:BackgroundJobs:Enabled")][InlineData("App:WeeklyCommissions:Enabled")]
        [InlineData("App:EntryMonthlyObligations:Enabled")][InlineData("App:AutomaticPayouts:Enabled")]
        [InlineData("AQGreenV2Pilot:Enabled")][InlineData("AQGreenV2Demo:Enabled")][InlineData("SEED_DEMO_DATA")]
        public void ReceiverRejectsAutomationAndPayoutEnablement(string key)
        { var c=Config();c[key]="true";Should.Throw<InvalidOperationException>(()=>new YocoCutoverOptions(c)); }
        [Theory]
        [InlineData("POST","/api/payments/yoco/webhook",true)]
        [InlineData("GET","/api/payments/yoco/webhook",false)]
        [InlineData("GET","/api/health",true)]
        [InlineData("POST","/api/operations/yoco-cutover/phase",true)]
        [InlineData("GET","/api/operations/yoco-cutover/events",true)]
        [InlineData("POST","/api/services/app/ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout",false)]
        [InlineData("POST","/api/services/app/AdminProgrammeParticipation/ApproveProgrammeParticipation",false)]
        [InlineData("POST","/api/services/app/AdminCommission/RecordPayout",false)]
        [InlineData("POST","/api/operations/yoco-cutover/unknown",false)]
        public async Task ReceiveOnlyHttpBoundaryKeepsOnlyReceiptAndAuthorizedOperationsReachable(string method,string path,bool allowed)
        {
            var reached=false;var ctx=new DefaultHttpContext();ctx.Request.Method=method;ctx.Request.Path=path;ctx.Response.Body=new MemoryStream();
            await new ApplicationRecoveryMiddleware(_=>{reached=true;return Task.CompletedTask;},Config()).InvokeAsync(ctx);
            reached.ShouldBe(allowed);if(!allowed)ctx.Response.StatusCode.ShouldBe(503);
        }
        [Fact]
        public async Task DirectCheckoutCannotReachProviderAndDirectBusinessApplicationCallsAreDenied()
        {
            var c=Config();var network=new RejectNetwork();var gateway=new YocoCheckoutGateway(new HttpClient(network),c);
            await Should.ThrowAsync<InvalidOperationException>(()=>gateway.CreateAsync(null));network.Attempts.ShouldBe(0);
            var interceptor=new PilotApplicationBoundaryInterceptor(DisabledAQGreenPilotMode.Instance,c);
            var invocation=Substitute.For<IInvocation>();invocation.TargetType.Returns(typeof(AQGreenCleanStartRootAppService));
            invocation.Method.Returns(typeof(AQGreenCleanStartRootAppService).GetMethod("AuthorizeRootAsync"));
            Should.Throw<AbpAuthorizationException>(()=>interceptor.Intercept(invocation));invocation.DidNotReceive().Proceed();
            RuntimeOperationGuards.CanRunBackgroundWork(c).ShouldBeFalse();
        }
        [Theory]
        [InlineData(7L,1)][InlineData(8L,null)][InlineData(null,null)]
        public async Task OperatorOperationsDenyTenantSessionsAndOtherHostIdentities(long? user,int? tenant)
        {
            var controller=new YocoCutoverController(null,Config());var session=Substitute.For<IAbpSession>();
            session.UserId.Returns(user);session.TenantId.Returns(tenant);controller.AbpSession=session;
            (await controller.StatusAsync()).ShouldBeOfType<ForbidResult>();
            typeof(YocoCutoverController).GetCustomAttribute<AbpMvcAuthorizeAttribute>().Permissions.ShouldContain("Pages.Tenants");
        }
        [Fact]
        public void ReconciliationFileRequiresExactPinPrivateCustodyAndUnambiguousJson()
        {
            var directory=Path.Combine(Path.GetTempPath(),"aqua-yoco-proof-synthetic-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);var file=Path.Combine(directory,"accepted-synthetic.json");
            try
            {
                if(!OperatingSystem.IsWindows())File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
                var c=Config();c[YocoCutoverOptions.Prefix+"ReconciliationPath"]=file;
                void Write(string json)
                {
                    var bytes=System.Text.Encoding.UTF8.GetBytes(json);File.WriteAllBytes(file,bytes);
                    if(!OperatingSystem.IsWindows())File.SetUnixFileMode(file,UnixFileMode.UserRead|UnixFileMode.UserWrite);
                    c[YocoCutoverOptions.Prefix+"ReconciliationSha256"]=YocoCutoverService.Hash(bytes);
                }
                Write("{\"Contract\":\"Aqua.YocoCutover.ReconciliationV1\"}");var source=new YocoCutoverReconciliationSource(c);
                source.Read().Item1.Contract.ShouldBe("Aqua.YocoCutover.ReconciliationV1");
                File.AppendAllText(file," ");Should.Throw<InvalidOperationException>(()=>source.Read());
                Write("{\"Contract\":\"one\",\"Contract\":\"two\"}");Should.Throw<InvalidOperationException>(()=>source.Read());
                if(!OperatingSystem.IsWindows())
                {
                    Write("{}");File.SetUnixFileMode(file,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.GroupRead);
                    Should.Throw<InvalidOperationException>(()=>source.Read());
                }
            }
            finally {Directory.Delete(directory,true);}
        }
        private sealed class RejectNetwork : HttpMessageHandler
        { public int Attempts;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            { Attempts++;throw new InvalidOperationException("synthetic network prohibition"); } }
    }
}
