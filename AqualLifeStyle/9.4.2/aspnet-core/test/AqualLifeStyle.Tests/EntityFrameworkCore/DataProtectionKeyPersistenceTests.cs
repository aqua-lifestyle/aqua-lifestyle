using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Abp.AspNetCore.Dependency;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Health;
using AqualLifeStyle.Web.Host.Startup;
using Castle.Windsor;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class DataProtectionKeyPersistenceTests
    {
        [Fact]
        public void CastleBackedHost_PersistsKeysAcrossRestartWithoutApplicationDbContextRegistration()
        {
            var databasePath = CreateDatabase();
            try
            {
                using var certificate = CreateCertificate();
                string protectedPayload;

                using (var firstHost = BuildHost(databasePath, certificate, "AqualLifeStyle"))
                {
                    AssertProductionStyleRegistration(firstHost.Services);
                    protectedPayload = firstHost.Services
                        .GetDataProtector("account-email-test")
                        .Protect("verification-token");

                    using var scope = firstHost.Services.CreateScope();
                    var persistedKey = scope.ServiceProvider
                        .GetRequiredService<DataProtectionKeyDbContext>()
                        .DataProtectionKeys.Single();
                    persistedKey.Xml.ShouldContain("EncryptedKey");
                }

                using (var restartedHost = BuildHost(databasePath, certificate, "AqualLifeStyle"))
                {
                    AssertProductionStyleRegistration(restartedHost.Services);
                    restartedHost.Services
                        .GetDataProtector("account-email-test")
                        .Unprotect(protectedPayload)
                        .ShouldBe("verification-token");
                }
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void CastleBackedHosts_ShareKeysOnlyWhenApplicationNameMatches()
        {
            var databasePath = CreateDatabase();
            try
            {
                using var certificate = CreateCertificate();
                using var firstHost = BuildHost(databasePath, certificate, "AqualLifeStyle");
                var protectedPayload = firstHost.Services
                    .GetDataProtector("shared-instance-test")
                    .Protect("shared-payload");

                using var matchingHost = BuildHost(databasePath, certificate, "AqualLifeStyle");
                matchingHost.Services
                    .GetDataProtector("shared-instance-test")
                    .Unprotect(protectedPayload)
                    .ShouldBe("shared-payload");

                using var isolatedHost = BuildHost(databasePath, certificate, "AnotherApplication");
                Should.Throw<CryptographicException>(() => isolatedHost.Services
                    .GetDataProtector("shared-instance-test")
                    .Unprotect(protectedPayload));
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void CastleBackedHost_UnprotectsKeysDuringCertificateRotation()
        {
            var databasePath = CreateDatabase();
            try
            {
                using var previousCertificate = CreateCertificate();
                using var currentCertificate = CreateCertificate();
                string protectedPayload;

                using (var previousHost = BuildHost(
                           databasePath,
                           previousCertificate,
                           "AqualLifeStyle"))
                {
                    protectedPayload = previousHost.Services
                        .GetDataProtector("certificate-rotation-test")
                        .Protect("rotation-payload");
                }

                using var rotatedHost = BuildHost(
                    databasePath,
                    currentCertificate,
                    "AqualLifeStyle",
                    previousCertificate);
                rotatedHost.Services
                    .GetDataProtector("certificate-rotation-test")
                    .Unprotect(protectedPayload)
                    .ShouldBe("rotation-payload");
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void DedicatedContext_ReadsKeysWrittenByThePreviousPersistenceMapping()
        {
            var databasePath = CreateDatabase();
            try
            {
                using var certificate = CreateCertificate();
                string protectedPayload;

                using (var legacyProvider = BuildLegacyProvider(databasePath, certificate))
                {
                    protectedPayload = legacyProvider
                        .GetDataProtector("account-email-test")
                        .Protect("pre-fix-token");
                }

                using var fixedHost = BuildHost(databasePath, certificate, "AqualLifeStyle");
                fixedHost.Services
                    .GetDataProtector("account-email-test")
                    .Unprotect(protectedPayload)
                    .ShouldBe("pre-fix-token");
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void ProductionRegistration_UsesTheConfiguredHostDatabaseConnection()
        {
            const string connectionString =
                "Host=database.invalid;Port=5432;Database=aqua_host";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "ConnectionStrings:Default",
                        connectionString)
                })
                .Build();
            var services = new ServiceCollection();

            services.AddAqualLifeStyleDataProtection(configuration);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataProtectionKeyDbContext>();
            context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
            context.Database.GetConnectionString().ShouldBe(connectionString);
        }

        [Fact]
        public void MissingKeyTable_FailsWithoutFallingBackToEphemeralKeys()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"aqualifestyle-data-protection-missing-{Guid.NewGuid():N}.db");
            try
            {
                using var certificate = CreateCertificate();
                using var host = BuildHost(databasePath, certificate, "AqualLifeStyle");

                var exception = Should.Throw<CryptographicException>(() => host.Services
                    .GetDataProtector("missing-database-test")
                    .Protect("must-not-use-ephemeral-key"));
                exception.ToString().ShouldContain("DataProtectionKeys");
                exception.ToString().ShouldNotContain(databasePath);
                exception.ToString().ShouldNotContain("must-not-use-ephemeral-key");
            }
            finally
            {
                File.Delete(databasePath);
            }
        }

        [Fact]
        public void ApplicationMigrationStream_RemainsTheOnlySchemaOwner()
        {
            var dedicatedOptions = new DbContextOptionsBuilder<DataProtectionKeyDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            using var dedicatedContext = new DataProtectionKeyDbContext(dedicatedOptions);
            var dedicatedEntity = dedicatedContext.GetService<IDesignTimeModel>().Model
                .FindEntityType(typeof(DataProtectionKey));
            dedicatedEntity.IsTableExcludedFromMigrations().ShouldBeTrue();

            var applicationOptions = new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            using var applicationContext = new AqualLifeStyleDbContext(applicationOptions);
            var applicationEntity = applicationContext.GetService<IDesignTimeModel>().Model
                .FindEntityType(typeof(DataProtectionKey));
            applicationEntity.IsTableExcludedFromMigrations().ShouldBeFalse();

            AssertEquivalentKeyTableMappings(applicationEntity, dedicatedEntity);
        }

        private static IHost BuildHost(
            string databasePath,
            X509Certificate2 certificate,
            string applicationName,
            X509Certificate2 previousCertificate = null)
        {
            var container = new WindsorContainer();
            return Host.CreateDefaultBuilder()
                .UseCastleWindsor(container)
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureServices(services =>
                {
                    var dataProtection = services.AddAqualLifeStyleDataProtection(options =>
                        options.UseSqlite($"Data Source={databasePath}"));
                    if (!string.Equals(
                            applicationName,
                            DataProtectionPersistenceServiceCollectionExtensions.ApplicationName,
                            StringComparison.Ordinal))
                    {
                        dataProtection.SetApplicationName(applicationName);
                    }
                    dataProtection.ProtectKeysWithCertificate(certificate);
                    if (previousCertificate != null)
                    {
                        dataProtection.UnprotectKeysWithAnyCertificate(previousCertificate);
                    }
                })
                .Build();
        }

        private static void AssertProductionStyleRegistration(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<DataProtectionKeyDbContext>()
                .ShouldNotBeNull();
            scope.ServiceProvider.GetRequiredService<IDataProtectionKeyStoreReadinessProbe>()
                .IsReadyAsync()
                .GetAwaiter()
                .GetResult()
                .ShouldBeTrue();
            scope.ServiceProvider.GetService<AqualLifeStyleDbContext>()
                .ShouldBeNull();
        }

        private static void AssertEquivalentKeyTableMappings(
            IEntityType applicationEntity,
            IEntityType dedicatedEntity)
        {
            applicationEntity.GetTableName().ShouldBe("DataProtectionKeys");
            dedicatedEntity.GetTableName().ShouldBe(applicationEntity.GetTableName());
            dedicatedEntity.GetSchema().ShouldBe(applicationEntity.GetSchema());

            var applicationTable = StoreObjectIdentifier.Table(
                applicationEntity.GetTableName(),
                applicationEntity.GetSchema());
            var dedicatedTable = StoreObjectIdentifier.Table(
                dedicatedEntity.GetTableName(),
                dedicatedEntity.GetSchema());
            var expectedColumns = new[] { "FriendlyName", "Id", "Xml" };
            applicationEntity.GetProperties()
                .Select(property => property.GetColumnName(applicationTable))
                .OrderBy(name => name)
                .ShouldBe(expectedColumns);
            dedicatedEntity.GetProperties()
                .Select(property => property.GetColumnName(dedicatedTable))
                .OrderBy(name => name)
                .ShouldBe(expectedColumns);

            foreach (var applicationProperty in applicationEntity.GetProperties())
            {
                var dedicatedProperty = dedicatedEntity.FindProperty(applicationProperty.Name);
                dedicatedProperty.ShouldNotBeNull();
                dedicatedProperty.GetColumnName(dedicatedTable)
                    .ShouldBe(applicationProperty.GetColumnName(applicationTable));
                dedicatedProperty.ClrType.ShouldBe(applicationProperty.ClrType);
                dedicatedProperty.IsNullable.ShouldBe(applicationProperty.IsNullable);
                dedicatedProperty.ValueGenerated.ShouldBe(applicationProperty.ValueGenerated);
            }

            dedicatedEntity.FindPrimaryKey().Properties.Select(property => property.Name)
                .ShouldBe(applicationEntity.FindPrimaryKey().Properties.Select(property => property.Name));
        }

        private static ServiceProvider BuildLegacyProvider(
            string databasePath,
            X509Certificate2 certificate)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<LegacyDataProtectionKeyDbContext>(options =>
                options.UseSqlite($"Data Source={databasePath}"));
            services.AddDataProtection()
                .SetApplicationName("AqualLifeStyle")
                .PersistKeysToDbContext<LegacyDataProtectionKeyDbContext>()
                .ProtectKeysWithCertificate(certificate);
            return services.BuildServiceProvider();
        }

        private static string CreateDatabase()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"aqualifestyle-data-protection-{Guid.NewGuid():N}.db");
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "DataProtectionKeys" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY AUTOINCREMENT,
                    "FriendlyName" TEXT NULL,
                    "Xml" TEXT NULL
                );
                """;
            command.ExecuteNonQuery();
            return path;
        }

        private static X509Certificate2 CreateCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=AqualLifeStyle Data Protection Test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1));
        }

        private sealed class LegacyDataProtectionKeyDbContext
            : DbContext, IDataProtectionKeyContext
        {
            public LegacyDataProtectionKeyDbContext(
                DbContextOptions<LegacyDataProtectionKeyDbContext> options)
                : base(options)
            {
            }

            public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
        }
    }
}
