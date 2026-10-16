using System;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.AQGreenV2Demo
{
    public sealed class AQGreenV2DemoCalculationMode : IAQGreenDemoCalculationMode
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public AQGreenV2DemoCalculationMode(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
            if (!AQGreenV2DemoConfiguration.Validate(environment, configuration))
                throw new InvalidOperationException("Demo calculation requires the validated disposable demo host.");
        }

        // Recheck environment, disposable database and loopback-client invariants.
        public bool IsEnabled => AQGreenV2DemoConfiguration.Validate(_environment, _configuration);
    }
}
