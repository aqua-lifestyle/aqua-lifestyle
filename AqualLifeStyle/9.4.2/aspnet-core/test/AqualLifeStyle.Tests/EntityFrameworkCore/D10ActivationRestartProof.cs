using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;
using AqualLifeStyle.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    // Called by the isolated clean-start lifecycle against its disposable DB.
    // Real Production child processes, synthetic keys, no provider network calls.
    public static class D10ActivationRestartProof
    {
        public static async Task ActivateThroughHostAsync(string connectionString,IConfiguration configuration,bool expiredUnused=false)
        {
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root!=null&&!File.Exists(Path.Combine(root.FullName,"AqualLifeStyle.sln")))root=root.Parent;
            root.ShouldNotBeNull();
            var directory=Path.Combine(root.FullName,"src","AqualLifeStyle.Web.Host","bin","Release","net8.0");
            var overrides=configuration.AsEnumerable().Where(p=>p.Value!=null).ToDictionary(p=>p.Key.Replace(":","__"),p=>p.Value);
            overrides["Yoco__SecretKey"]="sk_live_synthetic_restart_not_a_real_credential";
            try { await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.DockerAsync("image","inspect","redis:7-alpine"); }
            catch(InvalidOperationException e) when(e.Message.Contains("No such image: redis:7-alpine",StringComparison.Ordinal))
            { await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.DockerAsync(TimeSpan.FromMinutes(2),"pull","redis:7-alpine"); }
            using var redis=await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.StartAsync();
            using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=Synthetic first activation",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddHours(1));
            using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};
            client.DefaultRequestHeaders.TryAddWithoutValidation("__tenant","");
            using var host=await AQGreenCleanStartPostgreSqlTests.ReceiverHost.Start(directory,connectionString,redis.Endpoint,
                Convert.ToBase64String(cert.Export(X509ContentType.Pfx,"synthetic")),true,client,environmentOverrides:overrides);
            await host.Ready(client); // Before Normal: the real provider health endpoint must be HTTP200.
            static JsonNode Payload(string json){var node=JsonNode.Parse(json);return node["result"]??node;}
            var held=Payload(await client.GetStringAsync(host.Address+"/api/health"));
            held["status"].GetValue<string>().ShouldBe("Healthy");held["paymentAvailability"].GetValue<string>().ShouldBe("Held");
            held["contractCapabilities"].AsArray().Select(n=>n.GetValue<string>()).ShouldNotContain("aqgreen-flexible-joining-v1");
            (await client.PostAsync(host.Address+"/api/services/app/ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout",
                new StringContent("{\"schedule\":0}",Encoding.UTF8,"application/json"))).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/status")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync();
            var operatorId=long.Parse(configuration["App:YocoCutover:OperatorUserId"]);
            await using var user=new NpgsqlCommand("SELECT \"SecurityStamp\" FROM \"AbpUsers\" WHERE \"Id\"=@id AND \"TenantId\" IS NULL AND \"IsActive\" AND NOT \"IsDeleted\"",connection);
            user.Parameters.AddWithValue("id",operatorId);var stamp=(string)await user.ExecuteScalarAsync();stamp.ShouldNotBeNullOrWhiteSpace();
            var claims=new[]{new Claim(ClaimTypes.NameIdentifier,operatorId.ToString()),new Claim(JwtSessionSecurityStampValidator.SecurityStampClaimType,stamp)};
            var token=new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("AqualLifeStyle","AqualLifeStyle",claims,
                DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddHours(1),new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(new string('s',64))),SecurityAlgorithms.HmacSha256)));
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
            var session=Payload(await client.GetStringAsync(host.Address+"/api/services/app/Session/GetCurrentLoginInformations"));
            session["tenant"].ShouldBeNull();session["user"].ShouldNotBeNull();
            (await client.GetAsync(host.Address+"/api/operations/yoco-cutover/status")).StatusCode.ShouldBe(HttpStatusCode.OK);
            async Task<HttpResponseMessage> Phase(string phase)=>await client.PostAsync(host.Address+"/api/operations/yoco-cutover/phase",
                new StringContent("{\"phase\":\""+phase+"\"}",Encoding.UTF8,"application/json"));
            if(expiredUnused)
            {
                (await Phase("Replay")).StatusCode.ShouldBe(HttpStatusCode.OK);
                var denied=await Phase("Normal");denied.StatusCode.ShouldBe(HttpStatusCode.Conflict);
                Payload(await denied.Content.ReadAsStringAsync())["status"].GetValue<string>().ShouldBe("Held");
                var stillHeld=Payload(await client.GetStringAsync(host.Address+"/api/health"));
                stillHeld["status"].GetValue<string>().ShouldBe("Healthy");stillHeld["paymentAvailability"].GetValue<string>().ShouldBe("Held");
                Payload(await client.GetStringAsync(host.Address+"/api/operations/yoco-cutover/status"))["phase"].GetValue<string>().ShouldBe("Replay");
                return; // No Normal/activation evidence may be created by an expired unused grant.
            }
            (await Phase("Normal")).StatusCode.ShouldBe(HttpStatusCode.Conflict); // Current source is ReceiveOnly, not Replay.
            foreach(var phase in new[]{"Replay","Normal"})
            {
                var response=await Phase(phase);response.StatusCode.ShouldBe(HttpStatusCode.OK,host.Log);
                Payload(await response.Content.ReadAsStringAsync())["status"].GetValue<string>().ShouldBe("Committed");
            }
            var available=Payload(await client.GetStringAsync(host.Address+"/api/health"));
            available["status"].GetValue<string>().ShouldBe("Healthy");available["paymentAvailability"].GetValue<string>().ShouldBe("Available");
            // A corrupt Normal receipt cannot fall back to Healthy/Held.
            await using var read=new NpgsqlCommand("SELECT \"JobArgs\" FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ControlV1'",connection);
            var original=(string)await read.ExecuteScalarAsync();var corrupt=JsonNode.Parse(original);
            foreach(var e in corrupt["Evidence"].AsArray())if(e["Phase"]?.GetValue<string>()=="Normal")e.AsObject().Remove("D10Activation");
            async Task Save(string value){await using var cmd=new NpgsqlCommand("UPDATE \"AbpBackgroundJobs\" SET \"JobArgs\"=@value WHERE \"JobType\"='Aqua.YocoCutover.ControlV1'",connection);
                cmd.Parameters.AddWithValue("value",value);(await cmd.ExecuteNonQueryAsync()).ShouldBe(1);}
            try {await Save(corrupt.ToJsonString());(await client.GetAsync(host.Address+"/api/health")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);}
            finally {await Save(original);}
            (await client.GetAsync(host.Address+"/api/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        public static async Task VerifyAsync(string connectionString,IConfiguration configuration)
        {
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root!=null&&!File.Exists(Path.Combine(root.FullName,"AqualLifeStyle.sln")))root=root.Parent;
            root.ShouldNotBeNull();
            var directory=Path.Combine(root.FullName,"src","AqualLifeStyle.Web.Host","bin","Release","net8.0");
            var overrides=configuration.AsEnumerable().Where(p=>p.Value!=null)
                .ToDictionary(p=>p.Key.Replace(":","__"),p=>p.Value);
            overrides["Yoco__SecretKey"]="sk_live_synthetic_restart_not_a_real_credential";
            // Prepare this proof's existing fixture explicitly. Do not depend on
            // a developer cache or change the immutable CI workflow/trust root.
            try { await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.DockerAsync("image","inspect","redis:7-alpine"); }
            catch(InvalidOperationException e) when(e.Message.Contains("No such image: redis:7-alpine",StringComparison.Ordinal))
            { await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.DockerAsync(TimeSpan.FromMinutes(2),"pull","redis:7-alpine"); }
            using var redis=await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.StartAsync();
            using var rsa=RSA.Create(2048);
            var request=new CertificateRequest("CN=Synthetic D10 restart",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddHours(1));
            var certificate=Convert.ToBase64String(cert.Export(X509ContentType.Pfx,"synthetic"));
            using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};
            for(var restart=0;restart<2;restart++)
            {
                using var host=await AQGreenCleanStartPostgreSqlTests.ReceiverHost.Start(directory,connectionString,redis.Endpoint,
                    certificate,true,client,environmentOverrides:overrides);
                await host.Ready(client);
                var healthNode=JsonNode.Parse(await client.GetStringAsync(host.Address+"/api/health"));
                (healthNode["result"]??healthNode)["paymentAvailability"].GetValue<string>().ShouldBe("Available");
                (await client.PostAsync(host.Address+"/api/services/app/Account/Register",
                    new StringContent("{}",Encoding.UTF8,"application/json"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest,host.Log);
                host.Kill(); // Next process has no shared runtime/source/IoC cache.
            }
            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync();
            await using var read=new NpgsqlCommand("SELECT \"JobArgs\" FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ControlV1'",connection);
            var original=(string)await read.ExecuteScalarAsync();var corrupt=JsonNode.Parse(original).AsObject();
            foreach(var entry in corrupt["Evidence"].AsArray())entry.AsObject().Remove("D10Activation");
            async Task Save(string value)
            {
                await using var update=new NpgsqlCommand("UPDATE \"AbpBackgroundJobs\" SET \"JobArgs\"=@value WHERE \"JobType\"='Aqua.YocoCutover.ControlV1'",connection);
                update.Parameters.AddWithValue("value",value);(await update.ExecuteNonQueryAsync()).ShouldBe(1);
            }
            try
            {
                await Save(corrupt.ToJsonString());
                using var rejected=await AQGreenCleanStartPostgreSqlTests.ReceiverHost.Start(directory,connectionString,redis.Endpoint,
                    certificate,true,client,environmentOverrides:overrides);
                await rejected.Exited();rejected.ExitCode.ShouldNotBe(0);
                rejected.Log.ShouldContain("D10_COMMITTED_ACTIVATION_INVALID");
            }
            finally { await Save(original); }
        }
    }
}
