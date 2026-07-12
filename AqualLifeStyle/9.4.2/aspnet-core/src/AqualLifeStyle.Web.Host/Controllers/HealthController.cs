using System;
using System.Threading.Tasks;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Models;
using Abp.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Web.Host.Controllers
{
    /// <summary>
    /// Provides a lightweight application health endpoint for frontend and operational readiness checks.
    /// </summary>
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private const string HealthyStatus = "Healthy";
        private const string DegradedStatus = "Degraded";

        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        /// <param name="dbContextProvider">The ABP database context provider.</param>
        /// <param name="environment">The current web host environment.</param>
        public HealthController(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IWebHostEnvironment environment)
        {
            _dbContextProvider = dbContextProvider;
            _environment = environment;
        }

        /// <summary>
        /// Gets the current application health status.
        /// </summary>
        /// <returns>A health response containing status and non-sensitive runtime metadata.</returns>
        [HttpGet]
        public async Task<ActionResult<HealthCheckResponse>> Get()
        {
            var isDatabaseReachable = await IsDatabaseReachable();

            return Ok(new HealthCheckResponse
            {
                Status = isDatabaseReachable ? HealthyStatus : DegradedStatus,
                IsDatabaseReachable = isDatabaseReachable,
                DatabaseStatus = isDatabaseReachable ? HealthyStatus : "Unavailable",
                Version = AppVersionHelper.Version,
                ReleaseDate = AppVersionHelper.ReleaseDate,
                CheckedAtUtc = DateTime.UtcNow,
                Environment = _environment.EnvironmentName,
                TraceId = HttpContext.TraceIdentifier
            });
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
