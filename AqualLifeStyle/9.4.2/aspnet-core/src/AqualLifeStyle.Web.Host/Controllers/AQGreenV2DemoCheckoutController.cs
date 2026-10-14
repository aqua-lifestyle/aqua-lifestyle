using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Controllers
{
    [ApiController]
    [Route("api/demo/aqgreen-checkout")]
    [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
    public class AQGreenV2DemoCheckoutController : ControllerBase, ITransientDependency
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IAbpSession _session;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;
        private readonly ProgrammePaymentConfirmationProcessor _processor;

        public AQGreenV2DemoCheckoutController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IAbpSession session,
            IUnitOfWorkManager unitOfWorkManager,
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            ProgrammePaymentConfirmationProcessor processor)
        {
            _environment = environment;
            _configuration = configuration;
            _session = session;
            _unitOfWorkManager = unitOfWorkManager;
            _dbContextProvider = dbContextProvider;
            _processor = processor;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            if (!AQGreenV2DemoConfiguration.Validate(_environment, _configuration))
                return NotFound();
            if (!_session.TenantId.HasValue || !_session.UserId.HasValue)
                return Forbid();

            using (var uow = _unitOfWorkManager.Begin())
            using (_unitOfWorkManager.Current.SetTenantId(_session.TenantId.Value))
            {
                var context = await _dbContextProvider.GetDbContextAsync();
                var customer = await context.Customers.AsNoTracking().SingleOrDefaultAsync(item =>
                    item.TenantId == _session.TenantId.Value &&
                    item.UserId == _session.UserId.Value && item.IsActive);
                if (customer == null)
                {
                    await uow.CompleteAsync();
                    return Forbid();
                }
                var checkout = await context.AQGreenJoiningCheckouts.AsNoTracking()
                    .Where(item => item.TenantId == _session.TenantId.Value &&
                        item.CustomerId == customer.Id &&
                        item.ProviderCheckoutId.StartsWith("aqgreen_v2_demo_") &&
                        (item.Status == HostedPaymentCheckoutStatus.AwaitingPayment ||
                         item.Status == HostedPaymentCheckoutStatus.Completed))
                    .OrderByDescending(item => item.CreatedAt)
                    .FirstOrDefaultAsync();
                if (checkout == null)
                {
                    await uow.CompleteAsync();
                    return NotFound();
                }
                var participation = await context.EntryParticipations.AsNoTracking()
                    .SingleAsync(item => item.Id == checkout.ParticipationId &&
                        item.TenantId == _session.TenantId.Value);
                await uow.CompleteAsync();
                return Ok(new
                {
                    checkoutId = checkout.Id,
                    amount = checkout.Amount,
                    currency = checkout.Currency,
                    confirmed = checkout.Status == HostedPaymentCheckoutStatus.Completed,
                    participationStatus = participation.Status.ToString()
                });
            }
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmAsync([FromBody] DemoCheckoutConfirmationInput input)
        {
            if (!AQGreenV2DemoConfiguration.Validate(_environment, _configuration))
                return NotFound();
            if (!_session.TenantId.HasValue || !_session.UserId.HasValue)
                return Forbid();
            if (input == null || input.CheckoutId == Guid.Empty)
                return BadRequest("A test checkout is required.");

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                   {
                       IsTransactional = true
                   }))
            using (_unitOfWorkManager.Current.SetTenantId(_session.TenantId.Value))
            {
                var context = await _dbContextProvider.GetDbContextAsync();
                var customer = await context.Customers.AsNoTracking().SingleOrDefaultAsync(item =>
                    item.TenantId == _session.TenantId.Value &&
                    item.UserId == _session.UserId.Value && item.IsActive);
                if (customer == null)
                {
                    await uow.CompleteAsync();
                    return Forbid();
                }
                var checkout = await context.AQGreenJoiningCheckouts.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == input.CheckoutId &&
                        item.TenantId == _session.TenantId.Value &&
                        item.CustomerId == customer.Id);
                if (checkout == null || checkout.ProviderCheckoutId !=
                    $"aqgreen_v2_demo_{checkout.Id:N}")
                {
                    await uow.CompleteAsync();
                    return NotFound();
                }
                if (input.Amount != checkout.Amount)
                {
                    await uow.CompleteAsync();
                    return BadRequest("The simulated amount does not match the checkout amount.");
                }

                var result = await _processor.ProcessAQGreenJoiningCheckoutAsync(
                    checkout.Id,
                    "AQGREEN_V2_DEMO",
                    $"aqgreen-v2-demo-{checkout.Id:N}",
                    checkout.ProviderCheckoutId,
                    input.Amount,
                    checkout.Currency,
                    DateTime.UtcNow);
                var participation = await context.EntryParticipations.AsNoTracking()
                    .SingleAsync(item => item.Id == checkout.ParticipationId &&
                        item.TenantId == _session.TenantId.Value);
                await uow.CompleteAsync();
                return Ok(new
                {
                    confirmed = true,
                    alreadyConfirmed = result.WasAlreadyProcessed,
                    awaitingAdministrativeApproval = participation.IsAwaitingAdministrativeApproval,
                    participationStatus = participation.Status.ToString()
                });
            }
        }
    }

    public sealed class DemoCheckoutConfirmationInput
    {
        public Guid CheckoutId { get; set; }
        public decimal Amount { get; set; }
    }
}
