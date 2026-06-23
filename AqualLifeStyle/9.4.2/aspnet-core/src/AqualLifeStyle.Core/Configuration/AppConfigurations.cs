using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Abp.Extensions;
using Abp.Reflection.Extensions;
using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Configuration
{
    public static class AppConfigurations
    {
        private static readonly ConcurrentDictionary<string, IConfigurationRoot> _configurationCache;

        static AppConfigurations()
        {
            _configurationCache = new ConcurrentDictionary<string, IConfigurationRoot>();
        }

        public static IConfigurationRoot Get(string path, string environmentName = null, bool addUserSecrets = false)
        {
            var cacheKey = path + "#" + environmentName + "#" + addUserSecrets;
            return _configurationCache.GetOrAdd(
                cacheKey,
                _ => BuildConfiguration(path, environmentName, addUserSecrets)
            );
        }

        private static IConfigurationRoot BuildConfiguration(string path, string environmentName = null, bool addUserSecrets = false)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(path)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            if (!environmentName.IsNullOrWhiteSpace())
            {
                builder = builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true);
            }

            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Default")) &&
                !string.IsNullOrWhiteSpace(databaseUrl))
            {
                builder.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:Default"] = ConvertPostgresUrlToConnectionString(databaseUrl)
                });
            }

            builder = builder.AddEnvironmentVariables();

            if (addUserSecrets)
            {
                builder.AddUserSecrets(typeof(AppConfigurations).GetAssembly(), optional: true);
            }

            return builder.Build();
        }

        private static string ConvertPostgresUrlToConnectionString(string databaseUrl)
        {
            Uri uri;
            if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out uri) ||
                (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
            {
                throw new InvalidOperationException("DATABASE_URL must be a valid PostgreSQL URL.");
            }

            var userInfo = uri.UserInfo.Split(new[] { ':' }, 2);
            if (userInfo.Length != 2)
            {
                throw new InvalidOperationException("DATABASE_URL must include a username and password.");
            }

            return string.Join(";", new[]
            {
                "Host=" + QuoteConnectionStringValue(uri.Host),
                "Port=" + (uri.IsDefaultPort ? 5432 : uri.Port),
                "Database=" + QuoteConnectionStringValue(Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))),
                "Username=" + QuoteConnectionStringValue(Uri.UnescapeDataString(userInfo[0])),
                "Password=" + QuoteConnectionStringValue(Uri.UnescapeDataString(userInfo[1])),
                "SSL Mode=Prefer",
                "Trust Server Certificate=true"
            });
        }

        private static string QuoteConnectionStringValue(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
