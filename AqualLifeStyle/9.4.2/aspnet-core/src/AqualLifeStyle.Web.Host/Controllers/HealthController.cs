using System;
using System.Threading.Tasks;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Health;
using AqualLifeStyle.Web.Host.Models;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Abp.Runtime.Caching.Redis;

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
            IDataProtectionKeyStoreReadinessProbe dataProtectionKeyStoreProbe)
        {
            _dbContextProvider = dbContextProvider;
            _environment = environment;
            _configuration = configuration;
            _redisDatabaseProvider = redisDatabaseProvider;
            _dataProtectionKeyStoreProbe = dataProtectionKeyStoreProbe;
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
            var isHealthy = isDatabaseReachable &&
                isDataProtectionKeyStoreReachable &&
                isRedisReachable;

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
                ContractCapabilities = System.Linq.Enumerable.ToArray(
                    DeploymentMetadata.ContractCapabilities),
                ReleaseDate = AppVersionHelper.ReleaseDate,
                CheckedAtUtc = DateTime.UtcNow,
                Environment = _environment.EnvironmentName,
                TraceId = HttpContext.TraceIdentifier
            };
            return isHealthy
                ? Ok(response)
                : StatusCode(503, response);
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
