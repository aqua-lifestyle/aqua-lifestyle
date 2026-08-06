using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Abp.AspNetCore.Dependency;
using Abp.Dependency;
using DotNetEnv;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace AqualLifeStyle.Web.Host.Startup
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            LoadEnvFile();
            if (args.Contains("--health-check"))
            {
                return await CheckHealthAsync();
            }

            var isProduction = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                Environments.Production,
                StringComparison.OrdinalIgnoreCase);
            Log.Logger = CreateLogger(isProduction, LogEventLevel.Information);
            try
            {
                Log.Information("Starting AqualLifeStyle API");
                var host = CreateHostBuilder(args).Build();
                DeploymentConfigurationValidator.Validate(host.Services);
                await host.RunAsync();
                return 0;
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "AqualLifeStyle API terminated unexpectedly");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static void LoadEnvFile()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    Environments.Development,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var directory = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(directory))
            {
                var envPath = Path.Combine(directory, ".env");
                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    return;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }
        }

        internal static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseSerilog((context, loggerConfiguration) => ConfigureLogger(
                    loggerConfiguration,
                    context.Configuration,
                    context.HostingEnvironment.IsProduction()))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .UseCastleWindsor(IocManager.Instance.IocContainer);

        private static Serilog.ILogger CreateLogger(bool useJson, LogEventLevel minimumLevel)
        {
            var configuration = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "AqualLifeStyle.Api");
            return useJson
                ? configuration.WriteTo.Console(new RenderedCompactJsonFormatter()).CreateLogger()
                : configuration.WriteTo.Console().CreateLogger();
        }

        private static void ConfigureLogger(
            LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
            bool useJson)
        {
            var configuredLevel = configuration["Logging:MinimumLevel"];
            LogEventLevel minimumLevel;
            if (!Enum.TryParse(configuredLevel, true, out minimumLevel))
            {
                minimumLevel = LogEventLevel.Information;
            }

            loggerConfiguration
                .MinimumLevel.Is(minimumLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "AqualLifeStyle.Api");
            if (useJson)
            {
                loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
            else
            {
                loggerConfiguration.WriteTo.Console();
            }
        }

        private static async Task<int> CheckHealthAsync()
        {
            var healthCheckUrl = Environment.GetEnvironmentVariable("HEALTHCHECK_URL");
            if (string.IsNullOrWhiteSpace(healthCheckUrl))
            {
                var ports = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";
                var port = ports.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "8080";
                healthCheckUrl = "http://127.0.0.1:" + port + "/api/health";
            }

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                using (var response = await client.GetAsync(healthCheckUrl))
                {
                    return response.IsSuccessStatusCode ? 0 : 1;
                }
            }
            catch
            {
                return 1;
            }
        }
    }
}
