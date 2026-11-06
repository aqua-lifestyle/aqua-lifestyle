using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Health;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

namespace AqualLifeStyle.Tests.WebHost
{
    public class DeploymentReadinessTests
    {
        private static readonly string Revision = new string('a', 40);
        private static IConfigurationRoot Configuration()
        {
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings:Default"] = "Host=synthetic;Password=database-secret-canary",
                ["App:ServerRootAddress"] = "https://api.example.test",
                ["App:ClientRootAddress"] = "https://web.example.test",
                ["App:CorsOrigins"] = "https://web.example.test",
                ["Authentication:JwtBearer:IsEnabled"] = "true",
                ["Authentication:JwtBearer:SecurityKey"] = "application-jwt-secret-canary-32-characters",
                ["Authentication:JwtBearer:Issuer"] = "application-issuer",
                ["Authentication:JwtBearer:Audience"] = "application-audience",
                ["DataProtection:CertificateBase64"] = "certificate-secret-canary",
                ["DataProtection:CertificatePassword"] = "certificate-password-canary",
                ["Redis:Configuration"] = "redis:6379",
                ["Bird:Enabled"] = "true", ["Bird:ApiKey"] = "bk_eu1_bird-secret-canary",
                ["Bird:FromEmail"] = "noreply@example.test", ["Bird:FromName"] = "Fixture",
                ["Bird:ReplyToEmail"] = "reply@example.test", ["Bird:DeliveryNotBeforeUtc"] = "1970-01-01T00:00:00Z",
                ["Yoco:Mode"] = "test", ["Yoco:SecretKey"] = "sk_test_yoco-secret-canary",
                ["Yoco:WebhookSecret"] = "whsec_" + Convert.ToBase64String(new byte[32]),
                ["App:Recovery:Enabled"] = "false", ["Deployment:BuildId"] = Revision
            };
            foreach (var key in DeploymentReadinessEvidence.HeldSettings) settings[key] = "false";
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }
        private static IWebHostEnvironment Environment()
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns("Production");
            return environment;
        }
        private static Dictionary<string, string> Claims()
        {
            return new Dictionary<string, string>
            {
                ["repository"] = "aqua-lifestyle/aqua-deployment", ["repository_id"] = "1423239668",
                ["repository_owner"] = "aqua-lifestyle", ["repository_owner_id"] = "307087979",
                ["sub"] = DeploymentReadinessAuthentication.Subject, ["environment"] = "production-release",
                ["ref"] = "refs/heads/main", ["ref_type"] = "branch",
                ["workflow_ref"] = DeploymentReadinessAuthentication.Workflow,
                ["workflow_sha"] = new string('b', 40), ["sha"] = new string('b', 40),
                ["event_name"] = "workflow_dispatch", ["runner_environment"] = "github-hosted",
                ["run_id"] = "123", ["run_attempt"] = "1",
                ["iat"] = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeSeconds().ToString()
            };
        }
        private static string Token(SecurityKey key, Dictionary<string, string> claims = null,
            string issuer = DeploymentReadinessAuthentication.Issuer,
            string audience = DeploymentReadinessAuthentication.Audience, int expirySeconds = 120)
        {
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience,
                (claims ?? Claims()).Select(c => new Claim(c.Key, c.Value)),
                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddSeconds(expirySeconds),
                new SigningCredentials(key, key is SymmetricSecurityKey ? SecurityAlgorithms.HmacSha256 : SecurityAlgorithms.RsaSha256)));
        }

        private static IWebHost Server(IConfigurationRoot configuration, SecurityKey key, bool cutover = false)
        {
            if (cutover)
            {
                configuration[YocoCutoverOptions.Prefix + "Enabled"] = "true";
                configuration[AQGreenD10OperatorEvidenceSource.Prefix + "AdmissionMode"] = "CLEAN_START";
            }
            var evidence = new DeploymentReadinessEvidence(Environment(), configuration);
            var oidc = new OpenIdConnectConfiguration { Issuer = DeploymentReadinessAuthentication.Issuer };
            oidc.SigningKeys.Add(key);
            return new WebHostBuilder().UseKestrel().UseUrls("http://127.0.0.1:0")
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddSingleton(evidence);
                    services.AddControllers().AddApplicationPart(typeof(DeploymentReadinessController).Assembly);
                    AuthConfigurer.Configure(services, configuration);
                    services.Configure<JwtBearerOptions>(DeploymentReadinessAuthentication.Scheme, options =>
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
                }).Configure(app =>
                {
                    app.UseRouting(); app.UseAuthentication();
                    app.UseMiddleware<ApplicationRecoveryMiddleware>();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }).Build();
        }
        private static HttpClient Client(IWebHost server)
        {
            return new HttpClient { BaseAddress = new Uri(server.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>().Addresses.Single()) };
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Protected_endpoint_accepts_only_GitHub_identity_and_never_returns_secrets(bool cutover)
        {
            using var rsa = RSA.Create(2048);
            using var server = Server(Configuration(), new RsaSecurityKey(rsa) { KeyId = "fixture" }, cutover);
            await server.StartAsync();
            using var client = Client(server);
            var path = "/api/deployment-readiness?expectedRevision=" + Revision;
            (await client.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Token(new RsaSecurityKey(rsa) { KeyId = "fixture" }));
            var response = await client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            json.ShouldContain("StartupValidated"); json.ShouldContain("ConfigurationHeld"); json.ShouldContain(Revision);
            foreach (var value in Configuration().AsEnumerable().Where(v => v.Key.Contains("Secret") || v.Key.Contains("ApiKey") || v.Key.Contains("Password") || v.Key.Contains("ConnectionStrings")))
                if (!string.IsNullOrEmpty(value.Value)) json.ShouldNotContain(value.Value);
            json.ShouldNotContain("secret-canary"); json.ShouldNotContain("ConnectionStrings");
            response.Headers.CacheControl.NoStore.ShouldBeTrue();
            (await client.GetAsync("/api/deployment-readiness?expectedRevision=" + new string('c', 40)))
                .StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            var defaults = server.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
            defaults.DefaultAuthenticateScheme.ShouldBe("JwtBearer"); defaults.DefaultChallengeScheme.ShouldBe("JwtBearer");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Wrong_signature_issuer_audience_expiry_claims_and_application_JWT_are_unauthorized(bool cutover)
        {
            using var rsa = RSA.Create(2048); using var other = RSA.Create(2048);
            var key = new RsaSecurityKey(rsa) { KeyId = "fixture" };
            using var server = Server(Configuration(), key, cutover); await server.StartAsync(); using var client = Client(server);
            var tokens = new List<string> { Token(new RsaSecurityKey(other) { KeyId = "fixture" }),
                Token(key, issuer: "https://attacker.invalid"), Token(key, audience: "other"), Token(key, expirySeconds: -1),
                Token(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("application-jwt-secret-canary-32-characters")),
                    issuer: "application-issuer", audience: "application-audience") };
            foreach (var name in new[] { "repository", "repository_id", "repository_owner", "repository_owner_id", "sub", "environment", "ref", "ref_type",
                "workflow_ref", "workflow_sha", "sha", "event_name", "runner_environment", "run_id", "run_attempt", "iat" })
            {
                var claims = Claims(); claims[name] = "unapproved"; tokens.Add(Token(key, claims));
                claims = Claims(); claims.Remove(name); tokens.Add(Token(key, claims));
            }
            var future = Claims(); future["iat"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString(); tokens.Add(Token(key, future));
            tokens.Add(Token(key, expirySeconds: 1000)); tokens.Add("malformed.jwt");
            foreach (var token in tokens)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                (await client.GetAsync("/api/deployment-readiness?expectedRevision=" + Revision)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
        }

        [Fact]
        public void Missing_configuration_invalid_revision_unknown_flags_and_reload_fail_closed()
        {
            foreach (var key in new[] { "Bird:ApiKey", "Yoco:SecretKey", "Yoco:WebhookSecret" })
            {
                var configuration = Configuration(); configuration[key] = null;
                Should.Throw<InvalidOperationException>(() => new DeploymentReadinessEvidence(Environment(), configuration));
            }
            var current = Configuration(); var evidence = new DeploymentReadinessEvidence(Environment(), current);
            evidence.Read(null).Status.ShouldBe("Unavailable"); evidence.Read("main").Status.ShouldBe("Unavailable");
            current["Authentication:JwtBearer:IsEnabled"] = "false"; evidence.Read(Revision).Status.ShouldBe("Unavailable");
            current["Authentication:JwtBearer:IsEnabled"] = "true";
            current["Bird:ApiKey"] = null; evidence.Read(Revision).Status.ShouldBe("Unavailable");
            current["Bird:ApiKey"] = "bk_eu1_bird-secret-canary";
            current["App:BackgroundJobs:Enabled"] = null; evidence.Read(Revision).Status.ShouldBe("Unavailable");
            current["App:BackgroundJobs:Enabled"] = "true"; evidence.Read(Revision).Status.ShouldBe("Unavailable");
            current["App:BackgroundJobs:Enabled"] = "false"; current.Reload(); evidence.Read(Revision).Status.ShouldBe("Unavailable");
        }

        [Fact]
        public async Task Live_clean_start_exports_actual_hash_only_bindings_under_the_existing_protected_identity()
        {
            var configuration=Configuration();
            configuration["Yoco:Mode"]="live";configuration["Yoco:SecretKey"]="sk_live_yoco-secret-canary";
            configuration["App:AutomaticPayouts:Enabled"]="false";
            configuration["Deployment:BuildId"]=null;configuration["RENDER_GIT_COMMIT"]=Revision;
            using var rsa=RSA.Create(2048);var key=new RsaSecurityKey(rsa){KeyId="fixture"};
            using var server=Server(configuration,key,true);await server.StartAsync();using var client=Client(server);
            var path="/api/deployment-readiness?expectedRevision="+Revision;
            (await client.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(key));
            var response=await client.GetAsync(path);response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var doc=System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("version").GetInt32().ShouldBe(2);
            var binding=doc.RootElement.GetProperty("d10Bindings");
            var expectedEf=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                typeof(AqualLifeStyle.EntityFrameworkCore.AQGreenReconciliationEvidenceSnapshot).Assembly.Location))).ToLowerInvariant();
            binding.GetProperty("requiredAssemblyDigest").GetString().ShouldBe(expectedEf);
            binding.GetProperty("requiredRuntimeAssemblyDigest").GetString().ShouldBe(
                AqualLifeStyle.Application.ProgrammeParticipations.AQGreenD10Runtime.RuntimeAssemblyDigest(expectedEf));
            binding.GetProperty("intendedConfigurationDigest").GetString().ShouldNotBe(binding.GetProperty("currentConfigurationDigest").GetString());
            binding.GetProperty("intendedBuildId").GetString().ShouldBe(Revision);
            binding.GetProperty("intendedEnvironmentId").GetString().ShouldBe("production-release");
            configuration["Deployment:BuildId"].ShouldBeNull();configuration["Deployment:EnvironmentId"].ShouldBeNull();
            configuration["App:FinancialOperations:Enabled"].ShouldBe("false");
            (await response.Content.ReadAsStringAsync()).ShouldNotContain("secret-canary");
        }

        [Fact]
        public void Live_binding_export_does_not_hash_secrets_or_turn_missing_or_enabled_inputs_into_valid_metadata()
        {
            var current=Configuration();current["Yoco:Mode"]="live";current["Yoco:SecretKey"]="sk_live_yoco-secret-canary";
            current["App:AQGreenV2:Commissioning:AdmissionMode"]="CLEAN_START";current["App:AutomaticPayouts:Enabled"]="false";
            var evidence=new DeploymentReadinessEvidence(Environment(),current);var before=evidence.Read(Revision);
            before.Status.ShouldBe("Ready");before.Version.ShouldBe(2);before.D10Bindings.ShouldNotBeNull();
            current["Yoco:SecretKey"]="sk_live_different-secret-canary";
            evidence.Read(Revision).D10Bindings.IntendedConfigurationDigest.ShouldBe(before.D10Bindings.IntendedConfigurationDigest);
            current["App:AutomaticPayouts:Enabled"]=null;
            var unknown=evidence.Read(Revision);unknown.Status.ShouldBe("Unavailable");unknown.Version.ShouldBe(2);unknown.D10Bindings.ShouldBeNull();
            current["App:AutomaticPayouts:Enabled"]="true";
            evidence.Read(Revision).Status.ShouldBe("Unavailable");
        }

        [Fact]
        public void Duplicate_identity_claims_and_unauthenticated_principals_are_denied()
        {
            var claims = Claims().Select(c => new Claim(c.Key, c.Value)).ToList();
            claims.Add(new Claim("exp", DateTimeOffset.UtcNow.AddSeconds(120).ToUnixTimeSeconds().ToString()));
            DeploymentReadinessAuthentication.Allows(new ClaimsPrincipal(new ClaimsIdentity(claims)), DateTimeOffset.UtcNow).ShouldBeFalse();
            claims.Add(new Claim("repository_id", "1423239668"));
            DeploymentReadinessAuthentication.Allows(new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")), DateTimeOffset.UtcNow).ShouldBeFalse();
        }

        private sealed class UnavailableKeys : IConfigurationManager<OpenIdConnectConfiguration>
        {
            public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
                => Task.FromException<OpenIdConnectConfiguration>(new InvalidOperationException("Fixture unavailable"));
            public void RequestRefresh() { }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Unavailable_signing_keys_never_authorize_readiness(bool cutover)
        {
            using var rsa = RSA.Create(2048); var key = new RsaSecurityKey(rsa) { KeyId = "fixture" };
            using var server = Server(Configuration(), key, cutover); await server.StartAsync(); using var client = Client(server);
            server.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(DeploymentReadinessAuthentication.Scheme).ConfigurationManager = new UnavailableKeys();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(key));
            (await client.GetAsync("/api/deployment-readiness?expectedRevision=" + Revision))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
