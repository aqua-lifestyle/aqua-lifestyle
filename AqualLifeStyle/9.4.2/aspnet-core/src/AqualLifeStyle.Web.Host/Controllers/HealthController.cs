using System;
using AqualLifeStyle.Web.Host.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace AqualLifeStyle.Web.Host.Controllers
{
    /// <summary>
    /// Provides a lightweight application health endpoint for frontend and operational readiness checks.
    /// </summary>
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        /// <param name="environment">The current web host environment.</param>
        public HealthController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        /// <summary>
        /// Gets the current application health status.
        /// </summary>
        /// <returns>A health response containing status and non-sensitive runtime metadata.</returns>
        [HttpGet]
        public ActionResult<HealthCheckResponse> Get()
        {
            return Ok(new HealthCheckResponse
            {
                Status = "Healthy",
                Version = AppVersionHelper.Version,
                ReleaseDate = AppVersionHelper.ReleaseDate,
                CheckedAtUtc = DateTime.UtcNow,
                Environment = _environment.EnvironmentName,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }
}
