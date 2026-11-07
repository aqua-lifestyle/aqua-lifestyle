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
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    // Called by the isolated clean-start lifecycle against its disposable DB.
    // Real Production child processes, synthetic keys, no provider network calls.
    public static class D10ActivationRestartProof
    {
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
            await AQGreenMigrationRollbackPostgreSqlTests.RecoveryRedis.DockerAsync("pull","redis:7-alpine");
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
