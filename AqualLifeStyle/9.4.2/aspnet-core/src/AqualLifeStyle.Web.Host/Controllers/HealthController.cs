using System;
using System.Threading.Tasks;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Health;
using AqualLifeStyle.Web.Host.Models;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Abp.Runtime.Caching.Redis;
using AqualLifeStyle.Payments.Yoco;

namespace AqualLifeStyle.Web.Host.Controllers
{
    /// <summary>
    /// Provides a lightweight application health endpoint for frontend and operational readiness checks.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ITransientDependency"/> so Castle Windsor can resolve this controller.
    /// Plain <see cref="ControllerBase"/> types are not registered by ABP conventional registration
    /// unless they also implement an ABP dependency interface (or inherit <c>AbpController</c>).
    /// </remarks>
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase, ITransientDependency
    {
        private const string HealthyStatus = "Healthy";
        private const string DegradedStatus = "Degraded";

        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IAbpRedisCacheDatabaseProvider _redisDatabaseProvider;
        private readonly IDataProtectionKeyStoreReadinessProbe _dataProtectionKeyStoreProbe;
        private readonly IYocoCutoverStore _cutoverStore;
        private readonly IYocoCutoverReleaseGate _cutoverReleaseGate;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        /// <param name="dbContextProvider">The ABP database context provider.</param>
        /// <param name="environment">The current web host environment.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="redisDatabaseProvider">The ABP Redis database provider.</param>
        /// <param name="dataProtectionKeyStoreProbe">The Data Protection key-store readiness probe.</param>
        public HealthController(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IAbpRedisCacheDatabaseProvider redisDatabaseProvider,
            IDataProtectionKeyStoreReadinessProbe dataProtectionKeyStoreProbe,
            IYocoCutoverStore cutoverStore = null,
            IYocoCutoverReleaseGate cutoverReleaseGate = null)
        {
            _dbContextProvider = dbContextProvider;
            _environment = environment;
            _configuration = configuration;
            _redisDatabaseProvider = redisDatabaseProvider;
            _dataProtectionKeyStoreProbe = dataProtectionKeyStoreProbe;
            _cutoverStore = cutoverStore;
            _cutoverReleaseGate = cutoverReleaseGate;
        }

        /// <summary>
        /// Gets the current application health status.
        /// </summary>
        /// <returns>A health response containing status and non-sensitive runtime metadata.</returns>
        [HttpGet]
        public async Task<ActionResult<HealthCheckResponse>> Get()
        {
            var isDatabaseReachable = await IsDatabaseReachable();
            var isDataProtectionKeyStoreReachable =
                await _dataProtectionKeyStoreProbe.IsReadyAsync();
            var isRedisConfigured = !string.IsNullOrWhiteSpace(_configuration["Redis:Configuration"]);
            var isRedisReachable = !isRedisConfigured || await IsRedisReachable();
            var paymentAvailability = await GetPaymentAvailabilityAsync();
            var isHealthy = isDatabaseReachable &&
                isDataProtectionKeyStoreReachable &&
                isRedisReachable && paymentAvailability != "Unavailable";

            var response = new HealthCheckResponse
            {
                Status = isHealthy ? HealthyStatus : DegradedStatus,
                IsDatabaseReachable = isDatabaseReachable,
                DatabaseStatus = isDatabaseReachable ? HealthyStatus : "Unavailable",
                IsDataProtectionKeyStoreReachable = isDataProtectionKeyStoreReachable,
                DataProtectionKeyStoreStatus = isDataProtectionKeyStoreReachable
                    ? HealthyStatus
                    : "Unavailable",
                IsRedisReachable = isRedisReachable,
                RedisStatus = !isRedisConfigured ? "NotConfigured" : isRedisReachable ? HealthyStatus : "Unavailable",
                Version = AppVersionHelper.Version,
                BuildId = DeploymentMetadata.ResolveBuildId(_configuration),
                ImageId = DeploymentMetadata.ResolveImageId(_configuration),
                PaymentContractVersion = DeploymentMetadata.PaymentContractVersion,
                ContractCapabilities = GetContractCapabilities(paymentAvailability),
                PaymentAvailability = paymentAvailability,
                ReleaseDate = AppVersionHelper.ReleaseDate,
                CheckedAtUtc = DateTime.UtcNow,
                Environment = _environment.EnvironmentName,
                TraceId = HttpContext.TraceIdentifier
            };
            return isHealthy
                ? Ok(response)
                : StatusCode(503, response);
        }

        private async Task<string> GetPaymentAvailabilityAsync()
        {
            var cutover = new YocoCutoverOptions(_configuration);
            if (!cutover.Enabled) return "NotCutoverGated";
            if (cutover.CompatibilityReceiver || !cutover.HasOperator ||
                !_configuration.GetValue<bool>(RuntimeOperationGuards.FinancialSetting)) return "Held";
            if (_cutoverStore == null || _cutoverReleaseGate == null) return "Unavailable";
            try
            {
                // The existing lease verifies durable Normal and zero unfinished
                // events without initializing/updating control or callback records.
                var lease = await _cutoverStore.EnterBusinessAsync();
                try
                {
                    await _cutoverStore.ReadOnlyAsync(() => _cutoverReleaseGate.VerifyAsync());
                    return "Available";
                }
                finally { await lease.DisposeAsync(); }
            }
            catch
            {
                // A rejected lease does not distinguish an ordinary held phase
                // from corrupt control/event state. Do not infer a healthy hold
                // from that failure: Normal authority could not be established.
                return "Unavailable";
            }
        }

        private string[] GetContractCapabilities(string paymentAvailability)
        {
            var capabilities = System.Linq.Enumerable.Where(
                DeploymentMetadata.ContractCapabilities,
                capability => _environment.EnvironmentName != AQGreenV2Pilot.AQGreenV2PilotConfiguration.EnvironmentName ||
                    capability == "aqgreen-flexible-joining-v1" ||
                    capability == "programme-approval-queue-v1" ||
                    capability == "member-programme-journey-v1");
            if (AQGreenV2DemoConfiguration.Validate(_environment, _configuration))
                capabilities = System.Linq.Enumerable.Append(capabilities, "aqgreen-v2-demo-checkout-v1");
            if (paymentAvailability == "Held" || paymentAvailability == "Unavailable")
                capabilities = System.Linq.Enumerable.Where(capabilities, capability =>
                    capability != "aqgreen-flexible-joining-v1" &&
                    capability != "programme-approval-queue-v1" &&
                    capability != "direct-onyx-checkout-v1");
            return System.Linq.Enumerable.ToArray(capabilities);
        }

        private async Task<bool> IsRedisReachable()
        {
            try
            {
                var database = _redisDatabaseProvider.GetDatabase();
                await database.PingAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsDatabaseReachable()
        {
            try
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                return await dbContext.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }
    }
}
