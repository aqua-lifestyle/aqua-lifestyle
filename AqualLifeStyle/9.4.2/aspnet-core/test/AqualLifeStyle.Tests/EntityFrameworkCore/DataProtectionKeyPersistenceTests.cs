using System;
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

            using (var setup = CreateServices(connection))
            {
                using var scope = setup.CreateScope();
                scope.ServiceProvider.GetRequiredService<AqualLifeStyleDbContext>()
                    .Database.EnsureCreated();
            }

            string protectedToken;
            using (var firstHost = CreateServices(connection))
            {
                protectedToken = firstHost.GetDataProtector("account-email-test")
                    .Protect("verification-token");
            }

            using (var restartedHost = CreateServices(connection))
            {
                restartedHost.GetDataProtector("account-email-test")
                    .Unprotect(protectedToken)
                    .Equals("verification-token", StringComparison.Ordinal)
                    .ShouldBeTrue();
            }
        }

        private static ServiceProvider CreateServices(SqliteConnection connection)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AqualLifeStyleDbContext>(options =>
                options.UseSqlite(connection));
            services.AddDataProtection()
                .SetApplicationName("AqualLifeStyle")
                .PersistKeysToDbContext<AqualLifeStyleDbContext>();
            return services.BuildServiceProvider();
        }
    }
}
