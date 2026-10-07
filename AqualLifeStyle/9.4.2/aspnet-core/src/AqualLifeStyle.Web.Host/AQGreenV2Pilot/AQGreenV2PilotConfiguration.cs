using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AqualLifeStyle.Web.Host.AQGreenV2Pilot
{
    public static class AQGreenV2PilotConfiguration
    {
        public const string EnvironmentName = "AQGreenV2Pilot";
        public const string DatabaseName = "aqua_aqgreen_v2_pilot";
        public static bool Validate(IWebHostEnvironment environment, IConfiguration configuration)
        {
            var requested = configuration.GetValue<bool>("AQGreenV2Pilot:Enabled");
            var exactEnvironment = environment.EnvironmentName == EnvironmentName;
            if (requested != exactEnvironment)
                throw new InvalidOperationException($"The isolated pilot requires its exact environment and explicit opt-in together. Environment={environment.EnvironmentName}; pilot opt-in={requested}.");
            if (!requested)
            {
                var configured = configuration["ConnectionStrings:Default"];
                if (!string.IsNullOrWhiteSpace(configured) && !configured.StartsWith("<set-via-", StringComparison.Ordinal))
                {
                    var nonPilotConnection = new NpgsqlConnectionStringBuilder(configured);
                    if (nonPilotConnection.Database == DatabaseName)
                        throw new InvalidOperationException("The dedicated test-money database cannot be opened outside the isolated pilot.");
                }
                return false;
            }
            if (configuration.GetValue<bool>("AQGreenV2Demo:Enabled"))
                throw new InvalidOperationException("Pilot and seeded demo modes cannot be combined.");
            if (!string.IsNullOrWhiteSpace(configuration["Redis:Configuration"]))
                throw new InvalidOperationException("Shared external caching is forbidden in the isolated pilot.");
            var connection = new NpgsqlConnectionStringBuilder(configuration["ConnectionStrings:Default"]);
            if (connection.Database != DatabaseName || !OnlyLoopback(connection.Host))
                throw new InvalidOperationException("The pilot requires its dedicated disposable loopback PostgreSQL database.");
            foreach (var setting in new[] { "Yoco:SecretKey", "Yoco:WebhookSecret", "Bird:AccessKey", "Bird:ApiKey" })
                if (!string.IsNullOrWhiteSpace(configuration[setting]))
                    throw new InvalidOperationException("External provider credentials are forbidden in the pilot.");
            foreach (var setting in new[] { "Bird:Enabled", "App:WeeklyCommissions:Enabled", "App:EntryMonthlyObligations:Enabled" })
                if (configuration.GetValue<bool>(setting))
                    throw new InvalidOperationException("Uncommissioned external/background capabilities are forbidden in the pilot.");
            foreach (var setting in new[] { "App:ClientRootAddress", "App:ServerRootAddress" })
                if (!Uri.TryCreate(configuration[setting], UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                    !OnlyLoopback(uri.Host))
                    throw new InvalidOperationException("Pilot application addresses must be explicit loopback URLs.");
            return true;
        }

        private static bool OnlyLoopback(string hosts)
        {
            if (string.IsNullOrWhiteSpace(hosts)) return false;
            foreach (var token in hosts.Split(','))
            {
                var host = token.Trim();
                if (host != "localhost" && !(IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)))
                    return false;
            }
            return true;
        }
    }
}
