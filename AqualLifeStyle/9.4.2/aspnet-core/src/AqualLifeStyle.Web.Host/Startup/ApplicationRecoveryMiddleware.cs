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

        private readonly IYocoCutoverStore _cutoverStore;
        public ApplicationRecoveryMiddleware(RequestDelegate next, IConfiguration configuration, IYocoCutoverStore cutoverStore = null)
        { _cutoverStore=cutoverStore; _next = next; _configuration = configuration; ApplicationRecoveryMode.IsEnabled(configuration); }

        public async Task InvokeAsync(HttpContext context)
        {
            var cutover = new YocoCutoverOptions(_configuration);
            if (cutover.Enabled)
            {
                context.Response.Headers["X-Aqua-Yoco-Receiver"] = cutover.CompatibilityReceiver ? "Schema54" : "FullV2";
                var path = context.Request.Path.Value?.TrimEnd('/');
                if (context.Request.Method == "POST" && path == "/api/payments/yoco/webhook" ||
                    context.Request.Method == "GET" && path == "/api/health" ||
                    context.Request.Method == "POST" && path == "/api/TokenAuth/Authenticate" ||
                    context.Request.Method == "GET" && path == "/api/services/app/Session/GetCurrentLoginInformations" ||
                    context.Request.Method == "GET" && (path == "/api/operations/yoco-cutover/status" || path == "/api/operations/yoco-cutover/events") ||
                    context.Request.Method == "POST" && (path == "/api/operations/yoco-cutover/phase" || path == "/api/operations/yoco-cutover/replay"))
                { await _next(context); return; }
                IAsyncDisposable lease;
                try
                {
                    if (cutover.CompatibilityReceiver || _cutoverStore == null)
                        throw new InvalidOperationException("YOCO_CUTOVER_BUSINESS_WRITES_FENCED");
                    lease = await _cutoverStore.EnterBusinessAsync();
                }
                catch
                { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsync("{\"error\":\"YOCO_CUTOVER_BUSINESS_WRITES_FENCED\"}"); return; }
                try { using (YocoCutoverDispatch.EnterBusiness()) { await _next(context); } }
                finally { await lease.DisposeAsync(); }
                return;
            }
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
