using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace AqualLifeStyle.Web.Host.AQGreenV2Demo
{
    /// <summary>
    /// Fail-closed boundary for the disposable AQGreen V2 browser demo.
    /// This is test infrastructure, not D10 business authority or a production cutover.
    /// </summary>
    public static class AQGreenV2DemoConfiguration
    {
        public const string EnvironmentName = "AQGreenV2Demo";
        public const string EnabledSetting = "AQGreenV2Demo:Enabled";
        public const string DatabaseName = "aqua_aqgreen_v2_demo";

        public static bool Validate(
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var requested = configuration.GetValue<bool>(EnabledSetting);
            var isDemoEnvironment = string.Equals(
                environment.EnvironmentName,
                EnvironmentName,
                StringComparison.Ordinal);

            if (environment.IsProduction() && requested)
            {
                throw new InvalidOperationException(
                    "NON-PRODUCTION AQGreen V2 demo mode was requested in Production. " +
                    "The application refuses to start.");
            }

            if (requested && !isDemoEnvironment)
            {
                throw new InvalidOperationException(
                    "AQGreenV2Demo__Enabled=true is valid only when " +
                    "ASPNETCORE_ENVIRONMENT=AQGreenV2Demo.");
            }

            if (isDemoEnvironment && !requested)
            {
                throw new InvalidOperationException(
                    "The AQGreenV2Demo environment requires the explicit " +
                    "AQGreenV2Demo__Enabled=true opt-in.");
            }

            if (!isDemoEnvironment)
            {
                return false;
            }

            ValidateDisposableDatabase(configuration["ConnectionStrings:Default"]);
            return true;
        }

        private static void ValidateDisposableDatabase(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString) ||
                connectionString.StartsWith("<set-via-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "AQGreen V2 demo mode requires an explicit disposable PostgreSQL " +
                    "ConnectionStrings__Default value.");
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);

                if (!HasOnlyLoopbackHosts(builder.Host))
                {
                    throw new InvalidOperationException(
                        "AQGreen V2 demo mode requires every PostgreSQL host to be " +
                        "loopback/local and the dedicated demo database to be named " +
                        DatabaseName + ".");
                }

                if (!string.Equals(
                        builder.Database,
                        DatabaseName,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "AQGreen V2 demo mode refuses databases not named " +
                        DatabaseName + ".");
                }
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "AQGreen V2 demo mode requires a valid PostgreSQL connection string.",
                    exception);
            }
        }

        private static bool HasOnlyLoopbackHosts(string hostList)
        {
            if (string.IsNullOrWhiteSpace(hostList))
            {
                return false;
            }

            // Npgsql represents a multi-host connection as one comma-separated Host value.
            // Validate every endpoint and preserve empty tokens so malformed lists fail closed.
            foreach (var hostToken in hostList.Split(','))
            {
                var host = hostToken.Trim();
                if (host.Length == 0 || !IsLoopbackHost(host))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLoopbackHost(string host)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
        }
    }
}
