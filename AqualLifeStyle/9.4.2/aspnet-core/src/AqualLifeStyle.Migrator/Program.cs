using System;
using System.IO;
using Castle.Facilities.Logging;
using DotNetEnv;
using Abp;
using Abp.Castle.Logging.Log4Net;
using Abp.Collections.Extensions;
using Abp.Dependency;

namespace AqualLifeStyle.Migrator
{
    public class Program
    {
        private static bool _quietMode;

        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            LoadEnvFile();
            ParseArgs(args);

            using (var bootstrapper = AbpBootstrapper.Create<AqualLifeStyleMigratorModule>())
            {
                bootstrapper.IocManager.IocContainer
                    .AddFacility<LoggingFacility>(
                        f => f.UseAbpLog4Net().WithConfig("log4net.config")
                    );

                bootstrapper.Initialize();

                using (var migrateExecuter = bootstrapper.IocManager.ResolveAsDisposable<MultiTenantMigrateExecuter>())
                {
                    var migrationSucceeded = migrateExecuter.Object.Run(_quietMode);
                    
                    if (_quietMode)
                    {
                        // exit clean (with exit code 0) if migration is a success, otherwise exit with code 1
                        var exitCode = Convert.ToInt32(!migrationSucceeded);
                        Environment.Exit(exitCode);
                    }
                    else
                    {
                        Console.WriteLine("Press ENTER to exit...");
                        Console.ReadLine();
                    }
                }
            }
        }

        private static void LoadEnvFile()
        {
            var directory = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(directory))
            {
                var envPath = Path.Combine(directory, ".env");
                if (File.Exists(envPath))
                {
                    Console.WriteLine($"[DEBUG] Loading .env from {envPath}");
                    Env.Load(envPath);
                    var connString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
                    Console.WriteLine($"[DEBUG] ConnectionStrings__Default={connString}");
                    return;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            Console.WriteLine("[DEBUG] .env file not found in current or parent directories");
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
