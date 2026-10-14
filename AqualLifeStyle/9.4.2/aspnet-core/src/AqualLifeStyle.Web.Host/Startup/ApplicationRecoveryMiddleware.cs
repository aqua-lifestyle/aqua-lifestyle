using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AqualLifeStyle.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Startup
{
    /// <summary>Suspends custom controllers and unknown routes during controlled recovery.</summary>
    public sealed class ApplicationRecoveryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GET /api/health",
            "POST /api/TokenAuth/Authenticate",
            "GET /AntiForgery/GetToken",
            "GET /AntiForgery/SetCookie",
            "GET /api/services/app/Session/GetCurrentLoginInformations",
            "GET /api/services/app/AdminProgrammeParticipation/GetLegacyAQGreenReconciliation",
            "GET /api/services/app/AdminProgrammeParticipation/GetAssignedAreas",
            "POST /api/services/app/AdminProgrammeParticipation/ExportLegacyAQGreenReconciliationEvidence"
        };

        public ApplicationRecoveryMiddleware(RequestDelegate next, IConfiguration configuration)
        { _next = next; _configuration = configuration; ApplicationRecoveryMode.IsEnabled(configuration); }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ApplicationRecoveryMode.IsEnabled(_configuration))
            {
                context.Response.Headers["X-Aqua-Recovery-Suspended"] = "true";
                if (!Allowed.Contains(context.Request.Method + " " + context.Request.Path.Value?.TrimEnd('/')))
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"APPLICATION_RECOVERY_SUSPENDED\"}");
                    return;
                }
            }
            await _next(context);
        }
    }
}
