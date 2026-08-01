using System;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Health;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AqualLifeStyle.Web.Host.Startup
{
    /// <summary>
    /// Configures the shared persistent Data Protection key ring.
    /// </summary>
    public static class DataProtectionPersistenceServiceCollectionExtensions
    {
        /// <summary>
        /// Stable discriminator shared by all Aqua API instances.
        /// </summary>
        public const string ApplicationName = "AqualLifeStyle";

        /// <summary>
        /// Registers the persistent key ring against the configured host database.
        /// </summary>
        public static IDataProtectionBuilder AddAqualLifeStyleDataProtection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var connectionString = configuration.GetConnectionString(
                AqualLifeStyleConsts.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The host database connection string is required for Data Protection persistence.");
            }

            return services.AddAqualLifeStyleDataProtection(options =>
                options.UseNpgsql(connectionString));
        }

        /// <summary>
        /// Registers the isolated key-store context with explicit provider options.
        /// </summary>
        public static IDataProtectionBuilder AddAqualLifeStyleDataProtection(
            this IServiceCollection services,
            Action<DbContextOptionsBuilder> configureOptions)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

            services.AddDbContext<DataProtectionKeyDbContext>(configureOptions);
            services.AddTransient<
                IDataProtectionKeyStoreReadinessProbe,
                DataProtectionKeyStoreReadinessProbe>();
            return services.AddDataProtection()
                .SetApplicationName(ApplicationName)
                .PersistKeysToDbContext<DataProtectionKeyDbContext>();
        }
    }
}
