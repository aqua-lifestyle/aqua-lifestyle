using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Authorization.Roles;
using Abp.Authorization.Users;
using Abp.Authorization.Roles;
using AqualLifeStyle.Authentication.JwtBearer;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public sealed partial class AQGreenCleanStartPostgreSqlTests
    {
        [Fact]
        public async Task YocoReceiverArtifact54MigrationCompatibility_RealProductionHostCommitsReceiptsSurvivesRestartAndCannotUnfence()
        {
            var directory=Environment.GetEnvironmentVariable("YOCO_RECEIVER54_ARTIFACT");
            if(string.IsNullOrWhiteSpace(directory)||!File.Exists(Path.Combine(directory,"AqualLifeStyle.Web.Host.dll")))
                throw new InvalidOperationException("Build the pinned schema-54 receiver with tools/operations/build-yoco-receiver54.sh and set YOCO_RECEIVER54_ARTIFACT. This proof cannot be skipped.");
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            await SeedReceiverOperators(c);
            // The allocator fixture intentionally omits lifecycle dates. Supply
            // explicit synthetic activation facts for this member-read scenario.
            await SqlAsync(c,"UPDATE \"EntryParticipations\" SET \"ActivatedAt\"=\"StartedAt\" WHERE \"Status\"=2");
            var before=await ProtectedDigestAsync(c);using var redis=await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.StartAsync();
            using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=Synthetic cutover receiver",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddHours(1));
            var cert64=Convert.ToBase64String(cert.Export(X509ContentType.Pfx,"synthetic"));
            using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};var body=CutoverBody();
            // The forced artifact needs no application operator to receive on
            // schema 54. No host, including the former fixture operator, gains
            // privileged access when the optional identity is absent.
            using(var host=await ReceiverHost.Start(directory,db.ConnectionString(),redis.Endpoint,cert64,true,client,false))
            {
                await host.Ready(client);
                await VerifyReceiverMemberReads(client,host,c,before);
                (await ReceiverPost(client,host.Address,body,false)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                (await ReceiverPost(client,host.Address,body,true)).StatusCode.ShouldBe(HttpStatusCode.OK,host.Log);
                (await ReceiverPost(client,host.Address,body,true)).StatusCode.ShouldBe(HttpStatusCode.OK);
                client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(7));
                foreach(var route in new[]{"status","events"})
                    (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/"+route)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                foreach(var route in new[]{"phase","replay"})
                    (await client.PostAsync(host.Address+"/api/operations/yoco-cutover/"+route,new StringContent("{}",Encoding.UTF8,"application/json")))
                        .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                client.DefaultRequestHeaders.Authorization=null;
                (await client.PostAsync(host.Address+"/api/services/app/Account/Register",new StringContent("{}",Encoding.UTF8,"application/json")))
                    .StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ManualHeldEventV1'")).ShouldBe(1);
                (await ProtectedDigestAsync(c)).ShouldBe(before);host.Kill();
            }
            using(var host=await ReceiverHost.Start(directory,db.ConnectionString(),redis.Endpoint,cert64,true,client))
            {
                await host.Ready(client);
                var fenced=await client.PostAsync(host.Address+"/api/services/app/Account/Register",new StringContent("{}",Encoding.UTF8,"application/json"));
                fenced.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);fenced.Headers.GetValues("X-Aqua-Yoco-Receiver").Single().ShouldBe("Schema54");
                (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/status")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized,host.Log);
                client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(8));
                (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/status")).StatusCode.ShouldBe(HttpStatusCode.Forbidden,host.Log);
                client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(3001,1));
                (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/status")).StatusCode.ShouldBe(HttpStatusCode.Forbidden,host.Log);
                client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(7));
                (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/status")).StatusCode.ShouldBe(HttpStatusCode.OK,host.Log);
                (await client.PostAsync(host.Address+"/api/operations/yoco-cutover/phase",new StringContent("{\"phase\":\"ReceiveOnly\"}",Encoding.UTF8,"application/json")))
                    .StatusCode.ShouldBe(HttpStatusCode.OK,host.Log);
                (await client.PostAsync(host.Address+"/api/operations/yoco-cutover/phase",new StringContent("{\"phase\":\"Replay\"}",Encoding.UTF8,"application/json")))
                    .StatusCode.ShouldBe(HttpStatusCode.Conflict,host.Log);
                client.DefaultRequestHeaders.Authorization=null;
                (await ReceiverPost(client,host.Address,body,false)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                (await ReceiverPost(client,host.Address,body,true)).StatusCode.ShouldBe(HttpStatusCode.OK,host.Log);
                Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ManualHeldEventV1'")).ShouldBe(1);
                (await ProtectedDigestAsync(c)).ShouldBe(before);
                // Abrupt process death after acknowledgement: the committed inbox
                // remains independent of process memory and the HTTP response.
                host.Kill();
            }
            using(var host=await ReceiverHost.Start(directory,db.ConnectionString(),redis.Endpoint,cert64,true,client))
            {
                await host.Ready(client);(await ReceiverPost(client,host.Address,body,true)).StatusCode.ShouldBe(HttpStatusCode.OK);
                Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ManualHeldEventV1'")).ShouldBe(1);
                // Receipt-only artifact must also survive applying the unchanged
                // migration while it continues receiving callbacks.
                await Apply55(c);(await ReceiverPost(client,host.Address,CutoverBody(),true,"evt_after55")).StatusCode.ShouldBe(HttpStatusCode.OK);
                host.Kill();
            }
            using(var host=await ReceiverHost.Start(directory,db.ConnectionString(),redis.Endpoint,cert64,true,client))
            { await host.Ready(client);(await ReceiverPost(client,host.Address,body,true)).StatusCode.ShouldBe(HttpStatusCode.OK); }
            using(var host=await ReceiverHost.Start(directory,db.ConnectionString(),redis.Endpoint,cert64,false,client))
            { await host.Exited();host.ExitCode.ShouldNotBe(0);host.Log.ShouldContain("YOCO_RECEIVER_MUST_REMAIN_ENABLED"); }
            (await ProtectedDigestAsync(c)).ShouldBe(before);
        }
        private async Task SeedReceiverOperators(NpgsqlConnection connection)
        {
            await using var context=_fixture.CreateDbContext(connection);
            new AqualLifeStyle.EntityFrameworkCore.Seed.Host.DefaultLanguagesCreator(context).Create();
            var role=new Role(null,"SyntheticReceiverOperator","Synthetic receiver operator");context.Roles.Add(role);await context.SaveChangesAsync();
            foreach(var id in new[]{7L,8L})
            {
                var user=new User {Id=id,TenantId=null,UserName="synthetic-receiver-"+id,Name="Synthetic",Surname="Operator",
                    EmailAddress="synthetic-receiver-"+id+"@example.test",IsActive=true,IsEmailConfirmed=true,
                    SecurityStamp="synthetic-cutover-stamp-"+id,ConcurrencyStamp=Guid.NewGuid().ToString("N"),Password="synthetic-unused-jwt-only"};
                user.SetNormalizedNames();context.Users.Add(user);await context.SaveChangesAsync();context.UserRoles.Add(new UserRole(null,id,role.Id));
            }
            var tenantUser=await context.Users.SingleAsync(x=>x.Id==3001);tenantUser.SecurityStamp="synthetic-cutover-stamp-3001";
            var memberRole=new Role(1,"Member","Member");context.Roles.Add(memberRole);await context.SaveChangesAsync();
            context.UserRoles.Add(new UserRole(1,3001,memberRole.Id));
            foreach(var permission in AqualLifeStyle.Authorization.AquaRolePermissions.GetFor(AqualLifeStyle.Domain.Enums.AquaUserRole.Member)
                .Concat(new[]{"Pages.Products","Pages.Memberships"}))
                context.Permissions.Add(new RolePermissionSetting {TenantId=1,RoleId=memberRole.Id,Name=permission,IsGranted=true});
            context.Permissions.Add(new RolePermissionSetting {TenantId=null,RoleId=role.Id,Name="Pages.Tenants",IsGranted=true});await context.SaveChangesAsync();
        }
        private static async Task VerifyReceiverMemberReads(HttpClient client,ReceiverHost host,NpgsqlConnection c,string before)
        {
            var status=await client.GetAsync(host.Address+"/api/services/app/AQGreenPilot/GetStatus");
            status.StatusCode.ShouldBe(HttpStatusCode.OK,host.Log);
            var statusBody=await status.Content.ReadAsStringAsync();statusBody.ShouldContain("\"isPilot\":false");
            var health=await (await client.GetAsync(host.Address+"/api/health")).Content.ReadAsStringAsync();
            health.ShouldContain("member-programme-journey-v1");health.ShouldNotContain("aqgreen-flexible-joining-v1");
            health.ShouldNotContain("direct-onyx-checkout-v1");
            var routes=new[]{"Customer/GetMyCustomer","ClubMemberProgrammeParticipation/GetMyParticipations",
                "ClubMemberProgrammeProgress/GetMyJourney","ClubMemberProgrammeProgress/GetMyProgress",
                "ClubMemberSavings/GetMyAccount","ClubMemberOnyxLoan/GetMyAgreements",
                "ClubMemberEntryMonthlyObligation/GetMyObligations","OrderIntent/GetMine","Enquiry/GetMine",
                "Membership/GetActiveTiers","Membership/GetSavingsWindowStatuses","Product/GetAll","MyAccount/GetProfile"};
            foreach(var route in routes)
                (await client.GetAsync(host.Address+"/api/services/app/"+route)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized,route);
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(3001,1));
            (await client.GetAsync(host.Address+"/api/services/app/Membership/GetAll")).StatusCode.ShouldBe(HttpStatusCode.Forbidden,
                "The standard Member keeps ViewSelf without gaining the administrative inventory permission.");
            foreach(var route in routes)
            {
                var response=await client.GetAsync(host.Address+"/api/services/app/"+route);
                response.StatusCode.ShouldBe(HttpStatusCode.OK,route+" "+host.Log+" "+await response.Content.ReadAsStringAsync());
            }
            var customer=await (await client.GetAsync(host.Address+"/api/services/app/Customer/GetMyCustomer")).Content.ReadAsStringAsync();
            using(var document=System.Text.Json.JsonDocument.Parse(customer))
            {
                document.RootElement.GetProperty("result").GetProperty("id").GetInt32().ShouldBe(1);
                document.RootElement.GetProperty("result").GetProperty("email").GetString().ShouldBe("allocator-customer-1@example.test");
            }
            // Corrupt historical evidence remains an error, never a fabricated
            // zero/Incomplete progress value. This is disposable fixture data.
            await SqlAsync(c,"UPDATE \"EntryParticipations\" SET \"ActivatedAt\"=NULL WHERE \"TenantId\"=1 AND \"CustomerId\"=1");
            (await client.GetAsync(host.Address+"/api/services/app/ClubMemberProgrammeProgress/GetMyProgress")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            await SqlAsync(c,"UPDATE \"EntryParticipations\" SET \"ActivatedAt\"=\"StartedAt\" WHERE \"TenantId\"=1 AND \"CustomerId\"=1");
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(3001,2));
            (await client.GetAsync(host.Address+"/api/services/app/Customer/GetMyCustomer")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ReceiverToken(3001,1));
            foreach(var route in new[]{"ProgrammeInvitation/GetMyInvitations","Customer/GetAll","AdminCommission/GetAll","Unknown/GetAnything"})
                (await client.GetAsync(host.Address+"/api/services/app/"+route)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,route);
            foreach(var route in new[]{"Product/GetAll","ClubMemberProgrammeParticipation/GetMyParticipations","Account/Register",
                "ClubMemberProgrammeParticipation/StartEntry","ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout",
                "AdminProgrammeParticipation/ApproveProgrammeParticipation","AdminCommission/Release"})
                (await client.PostAsync(host.Address+"/api/services/app/"+route,new StringContent("{}",Encoding.UTF8,"application/json")))
                    .StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,route);
            client.DefaultRequestHeaders.Authorization=null;
            (await ProtectedDigestAsync(c)).ShouldBe(before);
        }
        private static string ReceiverToken(long user,int? tenant=null)
        {
            var claims=new System.Collections.Generic.List<Claim>{new Claim(ClaimTypes.NameIdentifier,user.ToString()),
                new Claim(JwtSessionSecurityStampValidator.SecurityStampClaimType,"synthetic-cutover-stamp-"+user)};
            if(tenant.HasValue)claims.Add(new Claim(Abp.Runtime.Security.AbpClaimTypes.TenantId,tenant.Value.ToString()));
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("AqualLifeStyle","AqualLifeStyle",claims,
                DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddHours(1),new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(new string('s',64))),SecurityAlgorithms.HmacSha256)));
        }
        private static async Task<HttpResponseMessage> ReceiverPost(HttpClient client,string address,byte[] body,bool valid,string id="evt_synthetic")
        {
            var(ts,sig)=CutoverSign(body,id);var request=new HttpRequestMessage(HttpMethod.Post,address+"/api/payments/yoco/webhook")
                {Content=new ByteArrayContent(body)};
            request.Headers.Add("webhook-id",id);request.Headers.Add("webhook-timestamp",ts);request.Headers.Add("webhook-signature",valid?sig:"v1,aW52YWxpZA==");
            return await client.SendAsync(request);
        }
        private sealed class ReceiverHost : IDisposable
        {
            private Process _process;private readonly ConcurrentQueue<string> _logs=new();
            public string Address;public string Log=>string.Join("\n",_logs);public int ExitCode=>_process.ExitCode;
            public static Task<ReceiverHost> Start(string dir,string cs,string redis,string cert,bool enabled,HttpClient _,bool operatorConfigured=true)
            {
                var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
                var host=new ReceiverHost {Address="http://127.0.0.1:"+port};
                var start=new ProcessStartInfo("dotnet") { WorkingDirectory=dir,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true };
                start.ArgumentList.Add(Path.Combine(dir,"AqualLifeStyle.Web.Host.dll"));
                start.Environment.Clear();start.Environment["PATH"]=Environment.GetEnvironmentVariable("PATH");
                foreach(var pair in new[]{("ASPNETCORE_ENVIRONMENT","Production"),("ASPNETCORE_URLS",host.Address),("ConnectionStrings__Default",cs),
                    ("App__ServerRootAddress","https://api.example.test"),("App__ClientRootAddress","https://app.example.test"),
                    ("App__CorsOrigins","https://app.example.test"),("Authentication__JwtBearer__SecurityKey",new string('s',64)),
                    ("Authentication__JwtBearer__Issuer","AqualLifeStyle"),("Authentication__JwtBearer__Audience","AqualLifeStyle"),("Authentication__JwtBearer__IsEnabled","true"),
                    ("DataProtection__CertificateBase64",cert),("DataProtection__CertificatePassword","synthetic"),("Redis__Configuration",redis),
                    ("App__YocoCutover__Enabled",enabled?"true":"false"),("App__YocoCutover__OperatorUserId","7"),
                    ("App__BackgroundJobs__Enabled","false"),("App__FinancialOperations__Enabled","false"),("App__WeeklyCommissions__Enabled","false"),
                    ("App__EntryMonthlyObligations__Enabled","false"),("App__AutomaticPayouts__Enabled","false"),("App__Recovery__Enabled","false"),
                    ("AQGreenV2Demo__Enabled","false"),("AQGreenV2Pilot__Enabled","false"),("SEED_DEMO_DATA","false"),("Bird__Enabled","true"),
                    ("Bird__ApiKey","bk_eu1_safe-test-placeholder"),("Bird__FromEmail","noreply@example.test"),("Bird__ReplyToEmail","help@example.test"),
                    ("Bird__FromName","Synthetic cutover test"),("Bird__DeliveryNotBeforeUtc","2026-01-01T00:00:00Z"),
                    ("Yoco__Mode","test"),("Yoco__SecretKey","sk_test_synthetic_not_a_real_credential"),("Yoco__WebhookSecret",CutoverSecret)}) start.Environment[pair.Item1]=pair.Item2;
                if(!operatorConfigured)start.Environment.Remove("App__YocoCutover__OperatorUserId");
                host._process=new Process {StartInfo=start};host._process.OutputDataReceived+=(_,e)=>{if(e.Data!=null)host._logs.Enqueue(e.Data);};
                host._process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)host._logs.Enqueue(e.Data);};host._process.Start();host._process.BeginOutputReadLine();host._process.BeginErrorReadLine();return Task.FromResult(host);
            }
            public async Task Ready(HttpClient client)
            {
                for(var i=0;i<300;i++)
                {
                    if(_process.HasExited)throw new InvalidOperationException("Synthetic receiver exited: "+Log);
                    try { if((await client.GetAsync(Address+"/api/health")).StatusCode==HttpStatusCode.OK)return; } catch(HttpRequestException) { }
                    await Task.Delay(200);
                }
                throw new TimeoutException("Synthetic receiver failed readiness: "+Log);
            }
            public async Task Exited(){await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));_process.WaitForExit();}
            public void Kill(){if(!_process.HasExited){_process.Kill(true);_process.WaitForExit();}}
            public void Dispose(){Kill();_process.Dispose();}
        }
    }
}
