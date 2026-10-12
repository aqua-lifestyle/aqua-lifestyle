using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Email;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Controllers
{
    /// <summary>
    /// Tenant-admin test mailbox for synthetic signup only. Never exposed outside
    /// the validated disposable demo; the actual confirmation endpoint is reused.
    /// </summary>
    [ApiController]
    [Route("api/demo/aqgreen-verification-link")]
    [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.View)]
    public class AQGreenV2DemoVerificationController : ControllerBase, ITransientDependency
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IAbpSession _session;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _contextProvider;
        private readonly ITransactionalEmailBodyProtector _bodyProtector;

        public AQGreenV2DemoVerificationController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IAbpSession session,
            IUnitOfWorkManager unitOfWorkManager,
            IDbContextProvider<AqualLifeStyleDbContext> contextProvider,
            ITransactionalEmailBodyProtector bodyProtector)
        {
            _environment = environment;
            _configuration = configuration;
            _session = session;
            _unitOfWorkManager = unitOfWorkManager;
            _contextProvider = contextProvider;
            _bodyProtector = bodyProtector;
        }

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetAsync([FromBody] AQGreenV2DemoVerificationInput input)
        {
            if (!AQGreenV2DemoConfiguration.Validate(_environment, _configuration))
                return NotFound();
            if (_session.TenantId != 1 || !_session.UserId.HasValue)
                return Forbid();
            var email = input?.Email?.Trim().ToLowerInvariant();
            if (email == null ||
                !email.StartsWith("aqgreen.operational.", StringComparison.Ordinal) ||
                !email.EndsWith("@example.test", StringComparison.Ordinal) ||
                email.Length > 256)
                return BadRequest("A synthetic operational test address is required.");

            using (var uow = _unitOfWorkManager.Begin())
            using (_unitOfWorkManager.Current.SetTenantId(1))
            {
                var context = await _contextProvider.GetDbContextAsync();
                var record = await context.TransactionalEmailOutboxMessages
                    .AsNoTracking()
                    .Where(item => item.TenantId == 1 &&
                        item.Recipient == email &&
                        item.NotificationType == "EmailVerification")
                    .OrderByDescending(item => item.CreationTime)
                    .FirstOrDefaultAsync();
                if (record == null)
                {
                    await uow.CompleteAsync();
                    return NotFound();
                }

                var line = _bodyProtector.Unprotect(record.TextBody)
                    .Split('\n').FirstOrDefault(item =>
                        item.StartsWith("Verify email: ", StringComparison.Ordinal));
                var link = line?.Substring("Verify email: ".Length).Trim();
                var client = AQGreenV2DemoConfiguration.ValidateLoopbackClient(
                    _configuration["App:ClientRootAddress"]);
                if (!Uri.TryCreate(link, UriKind.Absolute, out var parsed) ||
                    parsed.Scheme != client.Scheme || parsed.Authority != client.Authority ||
                    parsed.AbsolutePath != "/verify-email" ||
                    !parsed.Query.Contains("tenantId=1", StringComparison.Ordinal) ||
                    !parsed.Fragment.StartsWith("#token=", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "The synthetic verification link has an unexpected target.");

                Response.Headers["Cache-Control"] = "no-store";
                await uow.CompleteAsync();
                return Ok(new { verificationUrl = link });
            }
        }
    }

    public sealed class AQGreenV2DemoVerificationInput
    {
        public string Email { get; set; }
    }
}
