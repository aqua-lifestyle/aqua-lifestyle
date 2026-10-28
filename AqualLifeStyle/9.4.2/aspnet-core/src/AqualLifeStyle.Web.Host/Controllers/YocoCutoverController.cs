using System.Linq;
using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Controllers;
using Abp.Auditing;
using Abp.AspNetCore.Mvc.Authorization;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Controllers
{
    [ApiController]
    [Route("api/operations/yoco-cutover")]
    [AbpMvcAuthorize(PermissionNames.Pages_Tenants)]
    [DisableAuditing] // Raw financial envelopes never enter ordinary audit/log output.
    public sealed class YocoCutoverController : AbpController
    {
        private readonly YocoCutoverService _service;
        private readonly IConfiguration _configuration;
        public YocoCutoverController(YocoCutoverService service, IConfiguration configuration)
        { _service = service; _configuration = configuration; }
        private bool Operator() => AbpSession.UserId.HasValue && !AbpSession.TenantId.HasValue &&
            AbpSession.UserId == new YocoCutoverOptions(_configuration).OperatorUserId;
        [HttpGet("status")]
        public async Task<IActionResult> StatusAsync()
        {
            if (!Operator()) return Forbid();
            var s = await _service.StatusAsync();
            return Ok(new { s.Phase, s.HasMigration55, HasPending = s.Events.Any(YocoCutoverService.Unfinished),
                HasHeld = s.Events.Any(x => x.Status == "Held"), ReceiverOnlyArtifact = _service.CompatibilityReceiver });
        }
        // Restricted operator response: never log/spool it or publish it in chat.
        [HttpGet("events")]
        public async Task<IActionResult> EventsAsync()
        {
            if (!Operator()) return Forbid();
            var s = await _service.StatusAsync();
            return Ok(s.Events.Select(x => new { x.Id, x.EventId, x.PayloadHash, x.Status, x.HoldReason,
                x.ReconciliationDigest, x.VerifiedAtUtc }));
        }
        public sealed class PhaseInput { public string Phase { get; set; } }
        [HttpPost("phase")]
        public async Task<IActionResult> PhaseAsync(PhaseInput input)
        {
            if (!Operator()) return Forbid();
            try { await _service.SetPhaseAsync(input.Phase); return Ok(new { status = "Committed" }); }
            catch { return Conflict(new { status = "Held" }); }
        }
        public sealed class ReplayInput { public long Id { get; set; } }
        [HttpPost("replay")]
        public async Task<IActionResult> ReplayAsync(ReplayInput input)
        {
            if (!Operator()) return Forbid();
            try { return Ok(new { status = await _service.ReplayAsync(input.Id, AbpSession.GetUserId()) }); }
            catch { return Conflict(new { status = "Held" }); }
        }
    }
}
