using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class DataProtectionKeyPersistenceTests
    {
        [Fact]
        public void Token_RemainsValid_AfterServiceProviderRestart()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var certificate = CreateCertificate();

            using (var setup = CreateServices(connection, certificate))
            {
                using var scope = setup.CreateScope();
                scope.ServiceProvider.GetRequiredService<AqualLifeStyleDbContext>()
                    .Database.EnsureCreated();
            }

            string protectedToken;
            using (var firstHost = CreateServices(connection, certificate))
            {
                protectedToken = firstHost.GetDataProtector("account-email-test")
                    .Protect("verification-token");
                using var scope = firstHost.CreateScope();
                scope.ServiceProvider.GetRequiredService<AqualLifeStyleDbContext>()
                    .DataProtectionKeys.Single().Xml.ShouldContain("EncryptedKey");
            }

            using (var restartedHost = CreateServices(connection, certificate))
            {
                restartedHost.GetDataProtector("account-email-test")
                    .Unprotect(protectedToken)
                    .Equals("verification-token", StringComparison.Ordinal)
                    .ShouldBeTrue();
            }
        }

        [Fact]
        public void Token_RemainsValid_DuringCertificateRotation()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var oldCertificate = CreateCertificate();
            using var newCertificate = CreateCertificate();
            using (var setup = CreateServices(connection, oldCertificate))
            {
                using var scope = setup.CreateScope();
                scope.ServiceProvider.GetRequiredService<AqualLifeStyleDbContext>().Database.EnsureCreated();
            }

            string protectedToken;
            using (var oldHost = CreateServices(connection, oldCertificate))
            {
                protectedToken = oldHost.GetDataProtector("account-email-test").Protect("verification-token");
            }

            using var rotatedHost = CreateServices(connection, newCertificate, oldCertificate);
            rotatedHost.GetDataProtector("account-email-test")
                .Unprotect(protectedToken)
                .ShouldBe("verification-token");
        }

        private static ServiceProvider CreateServices(
            SqliteConnection connection,
            X509Certificate2 certificate,
            X509Certificate2 previousCertificate = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AqualLifeStyleDbContext>(options =>
                options.UseSqlite(connection));
            var dataProtection = services.AddDataProtection()
                .SetApplicationName("AqualLifeStyle")
                .PersistKeysToDbContext<AqualLifeStyleDbContext>()
                .ProtectKeysWithCertificate(certificate);
            if (previousCertificate != null)
            {
                dataProtection.UnprotectKeysWithAnyCertificate(previousCertificate);
            }
            return services.BuildServiceProvider();
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
    }
}
