using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using Abp.Runtime.Caching.Redis;
using AqualLifeStyle;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;

namespace AqualLifeStyle.Tests.WebHost
{
    /// <summary>
    /// Covers the Redis-awareness added to <see cref="HealthController"/>: the endpoint now
    /// reports database *and* Redis reachability, treats an unconfigured Redis cache as healthy,
    /// and returns HTTP 503 with a "Degraded" status whenever a configured dependency is down.
    /// </summary>
    public class HealthControllerTests
    {
        private static AqualLifeStyleDbContext CreateOpenSqliteContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseSqlite(connection)
                .Options;
            return new AqualLifeStyleDbContext(options);
        }

        private static IConfiguration BuildConfiguration(string redisConfiguration)
        {
            var settings = new Dictionary<string, string>();
            if (redisConfiguration != null)
            {
                settings["Redis:Configuration"] = redisConfiguration;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        private static HealthController CreateController(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IConfiguration configuration,
            IAbpRedisCacheDatabaseProvider redisDatabaseProvider,
            string environmentName = "Production")
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);

            var controller = new HealthController(dbContextProvider, environment, configuration, redisDatabaseProvider);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            return controller;
        }

        [Fact]
        public async Task Get_WhenDatabaseReachableAndRedisNotConfigured_ReturnsOkWithHealthyStatus()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var dbContext = CreateOpenSqliteContext(connection);

            var dbContextProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            dbContextProvider.GetDbContextAsync().Returns(Task.FromResult(dbContext));

            var redisDatabaseProvider = Substitute.For<IAbpRedisCacheDatabaseProvider>();

            var controller = CreateController(dbContextProvider, BuildConfiguration(null), redisDatabaseProvider);

            var actionResult = await controller.Get();

            var okResult = actionResult.Result.ShouldBeOfType<OkObjectResult>();
            var response = okResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Status.ShouldBe("Healthy");
            response.IsDatabaseReachable.ShouldBeTrue();
            response.DatabaseStatus.ShouldBe("Healthy");
            response.IsRedisReachable.ShouldBeTrue();
            response.RedisStatus.ShouldBe("NotConfigured");
        }

        [Fact]
        public async Task Get_WhenDatabaseReachableAndRedisReachable_ReturnsOkWithHealthyStatus()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var dbContext = CreateOpenSqliteContext(connection);

            var dbContextProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            dbContextProvider.GetDbContextAsync().Returns(Task.FromResult(dbContext));

            var redisDatabase = Substitute.For<IDatabase>();
            redisDatabase.PingAsync().Returns(Task.FromResult(TimeSpan.Zero));
            var redisDatabaseProvider = Substitute.For<IAbpRedisCacheDatabaseProvider>();
            redisDatabaseProvider.GetDatabase().Returns(redisDatabase);

            var controller = CreateController(dbContextProvider, BuildConfiguration("redis:6379"), redisDatabaseProvider);

            var actionResult = await controller.Get();

            var okResult = actionResult.Result.ShouldBeOfType<OkObjectResult>();
            var response = okResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Status.ShouldBe("Healthy");
            response.IsRedisReachable.ShouldBeTrue();
            response.RedisStatus.ShouldBe("Healthy");
        }

        [Fact]
        public async Task Get_WhenDatabaseUnreachable_ReturnsServiceUnavailableWithDegradedStatus()
        {
            var dbContextProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            dbContextProvider.GetDbContextAsync()
                .Returns(Task.FromException<AqualLifeStyleDbContext>(new InvalidOperationException("no database")));

            var redisDatabaseProvider = Substitute.For<IAbpRedisCacheDatabaseProvider>();

            var controller = CreateController(dbContextProvider, BuildConfiguration(null), redisDatabaseProvider);

            var actionResult = await controller.Get();

            var objectResult = actionResult.Result.ShouldBeOfType<ObjectResult>();
            objectResult.StatusCode.ShouldBe(503);
            var response = objectResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Status.ShouldBe("Degraded");
            response.IsDatabaseReachable.ShouldBeFalse();
            response.DatabaseStatus.ShouldBe("Unavailable");
        }

        [Fact]
        public async Task Get_WhenRedisConfiguredButUnreachable_ReturnsServiceUnavailableWithDegradedStatus()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var dbContext = CreateOpenSqliteContext(connection);

            var dbContextProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            dbContextProvider.GetDbContextAsync().Returns(Task.FromResult(dbContext));

            var redisDatabase = Substitute.For<IDatabase>();
            redisDatabase.PingAsync().Returns(Task.FromException<TimeSpan>(new Exception("redis unreachable")));
            var redisDatabaseProvider = Substitute.For<IAbpRedisCacheDatabaseProvider>();
            redisDatabaseProvider.GetDatabase().Returns(redisDatabase);

            var controller = CreateController(dbContextProvider, BuildConfiguration("redis:6379"), redisDatabaseProvider);

            var actionResult = await controller.Get();

            var objectResult = actionResult.Result.ShouldBeOfType<ObjectResult>();
            objectResult.StatusCode.ShouldBe(503);
            var response = objectResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Status.ShouldBe("Degraded");
            response.IsDatabaseReachable.ShouldBeTrue();
            response.IsRedisReachable.ShouldBeFalse();
            response.RedisStatus.ShouldBe("Unavailable");
        }

        [Fact]
        public async Task Get_PopulatesVersionAndEnvironmentMetadata()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var dbContext = CreateOpenSqliteContext(connection);

            var dbContextProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            dbContextProvider.GetDbContextAsync().Returns(Task.FromResult(dbContext));

            var redisDatabaseProvider = Substitute.For<IAbpRedisCacheDatabaseProvider>();

            var controller = CreateController(
                dbContextProvider, BuildConfiguration(null), redisDatabaseProvider, "Staging");

            var actionResult = await controller.Get();

            var okResult = actionResult.Result.ShouldBeOfType<OkObjectResult>();
            var response = okResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Environment.ShouldBe("Staging");
            response.Version.ShouldBe(AppVersionHelper.Version);
            response.TraceId.ShouldNotBeNull();
        }
    }
}