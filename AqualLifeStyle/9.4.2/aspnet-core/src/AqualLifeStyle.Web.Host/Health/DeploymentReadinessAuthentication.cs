using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AqualLifeStyle.Web.Host.Health
{
    // This scheme authorizes only a read-only operational endpoint. It never
    // authenticates an application user or participates in the default JWT scheme.
    public static class DeploymentReadinessAuthentication
    {
        public const string Scheme = "GitHubDeploymentReadiness";
        public const string Policy = "GitHubDeploymentReadinessOnly";
        public const string Issuer = "https://token.actions.githubusercontent.com";
        public const string Audience = "https://aqualifestyle-api.onrender.com/api/deployment-readiness";
        public const string Workflow = "aqua-lifestyle/aqua-deployment/.github/workflows/deploy.yml@refs/heads/main";
        public const string Subject = "repo:aqua-lifestyle@307087979/aqua-deployment@1423239668:environment:production-release";

        public static TokenValidationParameters ValidationParameters()
            => new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                AudienceValidator = (audiences, token, parameters) =>
                    audiences != null && audiences.SequenceEqual(new[] { Audience }),
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            };

        public static void Configure(IServiceCollection services)
        {
            services.AddAuthentication().AddJwtBearer(Scheme, options =>
            {
                options.Authority = Issuer;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.BackchannelTimeout = TimeSpan.FromSeconds(10);
                options.TokenValidationParameters = ValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path != "/api/deployment-readiness") context.NoResult();
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        context.NoResult(); // Unavailable discovery/keys deny access; never select another issuer.
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        if (!Allows(context.Principal, DateTimeOffset.UtcNow))
                            context.Fail("Deployment readiness identity denied.");
                        return Task.CompletedTask;
                    }
                };
            });
            services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            {
                policy.AddAuthenticationSchemes(Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context => Allows(context.User, DateTimeOffset.UtcNow));
            }));
        }

        public static bool Allows(ClaimsPrincipal principal, DateTimeOffset now)
        {
            if (principal == null || !principal.Identities.Any(identity => identity.IsAuthenticated)) return false;
            Func<string, string> single = name =>
            {
                var claims = principal.FindAll(name).ToArray();
                return claims.Length == 1 ? claims[0].Value : null;
            };
            Func<string, bool> sha = value => value != null && value.Length == 40 &&
                value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
            long issued, expires, run, attempt;
            return single("repository") == "aqua-lifestyle/aqua-deployment" &&
                single("repository_id") == "1423239668" && single("repository_owner") == "aqua-lifestyle" &&
                single("repository_owner_id") == "307087979" && single("sub") == Subject &&
                single("environment") == "production-release" && single("ref") == "refs/heads/main" &&
                single("ref_type") == "branch" && single("workflow_ref") == Workflow &&
                single("event_name") == "workflow_dispatch" && single("runner_environment") == "github-hosted" &&
                sha(single("workflow_sha")) && single("workflow_sha") == single("sha") &&
                long.TryParse(single("run_id"), NumberStyles.None, CultureInfo.InvariantCulture, out run) && run > 0 &&
                long.TryParse(single("run_attempt"), NumberStyles.None, CultureInfo.InvariantCulture, out attempt) && attempt > 0 &&
                long.TryParse(single("iat"), out issued) && long.TryParse(single("exp"), out expires) &&
                issued <= now.ToUnixTimeSeconds() + 30 && issued >= now.ToUnixTimeSeconds() - 600 &&
                expires > now.ToUnixTimeSeconds() && expires > issued && expires - issued <= 600;
        }
    }
}
