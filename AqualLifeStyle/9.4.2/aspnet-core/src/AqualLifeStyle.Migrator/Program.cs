using System;
using System.IO;
using Castle.Facilities.Logging;
using DotNetEnv;
using Abp;
using Abp.Collections.Extensions;
using Abp.Dependency;
using Castle.Services.Logging.SerilogIntegration;
using Serilog;
using Serilog.Formatting.Compact;
using SerilogLog = Serilog.Log;

namespace AqualLifeStyle.Migrator
{
    public class Program
    {
        private static bool _quietMode;

        public static int Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            LoadEnvFile();
            ParseArgs(args);
            var isProduction = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Production",
                StringComparison.OrdinalIgnoreCase);
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "AqualLifeStyle.Migrator");
            SerilogLog.Logger = isProduction
                ? loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter()).CreateLogger()
                : loggerConfiguration.WriteTo.Console().CreateLogger();
            try
            {
                using (var bootstrapper = AbpBootstrapper.Create<AqualLifeStyleMigratorModule>())
                {
                    bootstrapper.IocManager.IocContainer
                        .AddFacility<LoggingFacility>(f => f.LogUsing<SerilogFactory>());

                    bootstrapper.Initialize();

                    using (var migrateExecuter = bootstrapper.IocManager.ResolveAsDisposable<MultiTenantMigrateExecuter>())
                    {
                        var migrationSucceeded = migrateExecuter.Object.Run(_quietMode);
                        if (!_quietMode)
                        {
                            Console.WriteLine("Press ENTER to exit...");
                            Console.ReadLine();
                        }
                        return migrationSucceeded ? 0 : 1;
                    }
                }
            }
            catch (Exception exception)
            {
                SerilogLog.Fatal(exception, "Database migration failed unexpectedly");
                return 1;
            }
            finally
            {
                SerilogLog.CloseAndFlush();
            }
        }

        private static void LoadEnvFile()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    "Development",
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

        private static void ParseArgs(string[] args)
        {
            if (args.IsNullOrEmpty())
            {
                return;
            }

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "-q":
                        _quietMode = true;
                        break;
                }
            }
        }
    }
}
