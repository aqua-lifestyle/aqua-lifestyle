using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Castle.MicroKernel.Registration;
using Castle.Windsor.MsDependencyInjection;
using Abp.Dependency;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Identity;

namespace AqualLifeStyle.Tests.DependencyInjection
{
    public static class ServiceCollectionRegistrar
    {
        public static void Register(IIocManager iocManager)
        {
            var services = new ServiceCollection();

            IdentityRegistrar.Register(services);

            services.AddEntityFrameworkSqlite();

            var serviceProvider = WindsorRegistrationHelper.CreateServiceProvider(iocManager.IocContainer, services);

            // A SQLite in-memory database only lives while its connection is open, so keep a single
            // shared connection alive for the lifetime of this test's IoC scope. Using a real relational
            // provider (instead of the EF InMemory provider) lets integration tests exercise relational
            // behaviour such as ExecuteUpdate, matching production (PostgreSQL).
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var builder = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
            builder.UseSqlite(connection).UseInternalServiceProvider(serviceProvider);

            iocManager.IocContainer.Register(
                Component
                    .For<SqliteConnection>()
                    .Instance(connection)
                    .LifestyleSingleton()
            );

            iocManager.IocContainer.Register(
                Component
                    .For<DbContextOptions<AqualLifeStyleDbContext>>()
                    .Instance(builder.Options)
                    .LifestyleSingleton()
            );
        }
    }
}
