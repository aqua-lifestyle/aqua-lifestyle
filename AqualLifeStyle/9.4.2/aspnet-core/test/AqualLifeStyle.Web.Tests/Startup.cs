using System;
using Abp.AspNetCore;
using Abp.AspNetCore.TestBase;
using Abp.Dependency;
using AqualLifeStyle.Authentication.JwtBearer;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Identity;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Resources;
using AqualLifeStyle.Web.Startup;
using Castle.MicroKernel.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;

namespace AqualLifeStyle.Web.Tests
{
    public class Startup
    {
        private readonly IConfigurationRoot _appConfiguration;

        public Startup(IWebHostEnvironment env)
        {
            _appConfiguration = env.GetAppConfiguration();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddEntityFrameworkSqlite();

            services.AddMvc();
            
            IdentityRegistrar.Register(services);
            services.AddDataProtection().SetApplicationName("AqualLifeStyle.Web.Tests");
            AuthConfigurer.Configure(services, _appConfiguration);
            
            services.AddScoped<IWebResourceManager, WebResourceManager>();
            services.AddHttpClient<IYocoCheckoutGateway, YocoCheckoutGateway>();

            //Configure Abp and Dependency Injection
            return services.AddAbp<AqualLifeStyleWebTestModule>(options =>
            {
                options.SetupTest();
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            UseInMemoryDb(app.ApplicationServices);

            app.UseAbp(); //Initializes ABP framework.

            app.UseExceptionHandler("/Error");

            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();

            app.UseJwtTokenMiddleware();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private void UseInMemoryDb(IServiceProvider serviceProvider)
        {
            // Production-equivalent regression mode: run against a real PostgreSQL database when
            // REPRO_PG=true. PostgreSQL is required to reproduce the transactional visibility
            // semantics (suppressed reads cannot see uncommitted rows) that the default SQLite
            // shared-connection test database cannot emulate.
            var usePostgres = string.Equals(Environment.GetEnvironmentVariable("REPRO_PG"), "true", StringComparison.OrdinalIgnoreCase);
            if (usePostgres)
            {
                var conn = Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION")
                    ?? throw new InvalidOperationException(
                        "REPRO_PG=true requires REPRO_PG_CONNECTION to point at the configured PostgreSQL test database.");
                var builder = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
                builder.UseNpgsql(conn);
                var options = builder.Options;

                using (var context = new AqualLifeStyleDbContext(options))
                {
                    context.Database.Migrate();
                }

                var iocManager = serviceProvider.GetRequiredService<IIocManager>();
                iocManager.IocContainer
                    .Register(
                        Component.For<DbContextOptions<AqualLifeStyleDbContext>>()
                            .Instance(options)
                            .LifestyleSingleton()
                    );

                return;
            }

            // A SQLite in-memory database only lives while its connection is open, so keep a single
            // shared connection alive for the lifetime of the test server. Using a real relational
            // provider (instead of the EF InMemory provider) keeps the web tests consistent with
            // production (PostgreSQL) relational behaviour.
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var builderSqlite = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
            builderSqlite.UseSqlite(connection).UseInternalServiceProvider(serviceProvider);
            var optionsSqlite = builderSqlite.Options;

            // The database starts empty; create the schema before ABP runs its data seeders.
            using (var context = new AqualLifeStyleDbContext(optionsSqlite))
            {
                context.Database.EnsureCreated();
            }

            var iocManager2 = serviceProvider.GetRequiredService<IIocManager>();
            iocManager2.IocContainer
                .Register(
                    Component.For<SqliteConnection>()
                        .Instance(connection)
                        .LifestyleSingleton()
                );
            iocManager2.IocContainer
                .Register(
                    Component.For<DbContextOptions<AqualLifeStyleDbContext>>()
                        .Instance(optionsSqlite)
                        .LifestyleSingleton()
                );
        }
    }
}
