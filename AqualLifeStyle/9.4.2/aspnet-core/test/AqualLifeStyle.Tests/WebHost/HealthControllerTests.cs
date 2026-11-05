using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using Abp.Runtime.Caching.Redis;
using AqualLifeStyle;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Health;
using AqualLifeStyle.Web.Host.Models;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Payments.Yoco;
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

        private static IConfiguration BuildConfiguration(
            string redisConfiguration,
            IDictionary<string, string> additionalSettings = null)
        {
            var settings = new Dictionary<string, string>();
            if (redisConfiguration != null)
            {
                settings["Redis:Configuration"] = redisConfiguration;
            }
            if (additionalSettings != null)
            {
                foreach (var setting in additionalSettings)
                    settings[setting.Key] = setting.Value;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        private static HealthController CreateController(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IConfiguration configuration,
            IAbpRedisCacheDatabaseProvider redisDatabaseProvider,
            IDataProtectionKeyStoreReadinessProbe dataProtectionKeyStoreProbe = null,
            string environmentName = "Production",
            IYocoCutoverStore cutoverStore = null,
            IYocoCutoverReleaseGate releaseGate = null)
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);

            if (dataProtectionKeyStoreProbe == null)
            {
                dataProtectionKeyStoreProbe = Substitute.For<IDataProtectionKeyStoreReadinessProbe>();
                dataProtectionKeyStoreProbe.IsReadyAsync().Returns(Task.FromResult(true));
            }
            var controller = new HealthController(
                dbContextProvider,
                environment,
                configuration,
                redisDatabaseProvider,
                dataProtectionKeyStoreProbe, cutoverStore, releaseGate);
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
            response.IsDataProtectionKeyStoreReachable.ShouldBeTrue();
            response.DataProtectionKeyStoreStatus.ShouldBe("Healthy");
            response.IsRedisReachable.ShouldBeTrue();
            response.RedisStatus.ShouldBe("NotConfigured");
        }

        [Fact]
        public async Task Get_WhenDataProtectionKeyStoreUnavailable_ReturnsNoSensitiveDetails()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var dbContext = CreateOpenSqliteContext(connection);
            var dbContextProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            dbContextProvider.GetDbContextAsync().Returns(Task.FromResult(dbContext));
            var redisDatabaseProvider = Substitute.For<IAbpRedisCacheDatabaseProvider>();
            var keyStoreProbe = Substitute.For<IDataProtectionKeyStoreReadinessProbe>();
            keyStoreProbe.IsReadyAsync().Returns(Task.FromResult(false));
            var controller = CreateController(
                dbContextProvider,
                BuildConfiguration(null),
                redisDatabaseProvider,
                keyStoreProbe);

            var actionResult = await controller.Get();

            var objectResult = actionResult.Result.ShouldBeOfType<ObjectResult>();
            objectResult.StatusCode.ShouldBe(503);
            var response = objectResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Status.ShouldBe("Degraded");
            response.IsDatabaseReachable.ShouldBeTrue();
            response.IsDataProtectionKeyStoreReachable.ShouldBeFalse();
            response.DataProtectionKeyStoreStatus.ShouldBe("Unavailable");
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
                dbContextProvider,
                BuildConfiguration(null, new Dictionary<string, string>
                {
                    ["Deployment:BuildId"] = "commit-abc123",
                    ["Deployment:ImageId"] = "image-456"
                }),
                redisDatabaseProvider,
                environmentName: "Staging");

            var actionResult = await controller.Get();

            var okResult = actionResult.Result.ShouldBeOfType<OkObjectResult>();
            var response = okResult.Value.ShouldBeOfType<HealthCheckResponse>();
            response.Environment.ShouldBe("Staging");
            response.Version.ShouldBe(AppVersionHelper.Version);
            response.BuildId.ShouldBe("commit-abc123");
            response.ImageId.ShouldBe("image-456");
            response.PaymentContractVersion.ShouldBe(
                DeploymentMetadata.PaymentContractVersion);
            response.ContractCapabilities.ShouldContain(
                "aqgreen-flexible-joining-v1");
            response.ContractCapabilities.ShouldContain(
                "programme-approval-queue-v1");
            response.ContractCapabilities.ShouldContain(
                "member-programme-journey-v1");
            response.TraceId.ShouldNotBeNull();
        }

        [Fact]
        public void DeploymentMetadata_DoesNotExposeUnsafeConfiguredValues()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Deployment:BuildId"] = "contains/a/path",
                    ["Deployment:ImageId"] = "contains a secret-like value"
                })
                .Build();

            DeploymentMetadata.ResolveBuildId(configuration).ShouldBe("unavailable");
            DeploymentMetadata.ResolveImageId(configuration).ShouldBe("unavailable");
        }

        [Theory]
        [InlineData("YOCO_CUTOVER_BUSINESS_WRITES_FENCED", "Unavailable", 503)]
        [InlineData("YOCO_CUTOVER_RECONCILIATION_HELD", "Unavailable", 503)]
        [InlineData("synthetic corrupt/unreadable state", "Unavailable", 503)]
        [InlineData(null, "Available", 200)]
        public async Task CutoverCapabilitiesRequireNormalLeaseAndCurrentReleaseAuthority(
            string failure, string expectedAvailability, int status)
        {
            using var connection = new SqliteConnection("DataSource=:memory:"); connection.Open();
            using var context = CreateOpenSqliteContext(connection);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContextAsync().Returns(context);
            var store = Substitute.For<IYocoCutoverStore>(); var lease = Substitute.For<IAsyncDisposable>();
            store.EnterBusinessAsync().Returns(_ => failure == null ? Task.FromResult(lease) :
                Task.FromException<IAsyncDisposable>(new InvalidOperationException(failure)));
            store.ReadOnlyAsync(Arg.Any<Func<Task>>()).Returns(call => call.Arg<Func<Task>>()());
            var release = Substitute.For<IYocoCutoverReleaseGate>();
            var configuration = BuildConfiguration(null, new Dictionary<string, string>
            {
                [YocoCutoverOptions.Prefix + "Enabled"] = "true",
                [YocoCutoverOptions.Prefix + "OperatorUserId"] = "7",
                [RuntimeOperationGuards.FinancialSetting] = "true"
            });
            var controller = CreateController(provider, configuration, Substitute.For<IAbpRedisCacheDatabaseProvider>(),
                cutoverStore: store, releaseGate: release);
            var actionResult = await controller.Get();
            ObjectResult result = status == 200
                ? actionResult.Result.ShouldBeOfType<OkObjectResult>()
                : actionResult.Result.ShouldBeOfType<ObjectResult>();
            result.StatusCode.ShouldBe(status);
            var response = result.Value.ShouldBeOfType<HealthCheckResponse>();
            response.PaymentAvailability.ShouldBe(expectedAvailability);
            response.ContractCapabilities.Contains("aqgreen-flexible-joining-v1").ShouldBe(failure == null);
            if (failure == null)
            {
                await release.Received(1).VerifyAsync();
                await lease.Received(1).DisposeAsync();
            }
            else await release.DidNotReceive().VerifyAsync();
            await store.DidNotReceive().TransactAsync(Arg.Any<Func<YocoCutoverSnapshot, Task<bool>>>());
        }

        [Fact]
        public async Task InvalidReleaseAuthorityIsUnavailableAndDisposesTheLease()
        {
            var store = Substitute.For<IYocoCutoverStore>(); var lease = Substitute.For<IAsyncDisposable>();
            store.EnterBusinessAsync().Returns(lease);
            store.ReadOnlyAsync(Arg.Any<Func<Task>>()).Returns(call => call.Arg<Func<Task>>()());
            var release = Substitute.For<IYocoCutoverReleaseGate>();
            release.VerifyAsync().Returns(Task.FromException(new InvalidOperationException("synthetic missing D10 authority")));
            using var connection = new SqliteConnection("DataSource=:memory:"); connection.Open();
            using var context = CreateOpenSqliteContext(connection);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>(); provider.GetDbContextAsync().Returns(context);
            var configuration = BuildConfiguration(null, new Dictionary<string, string>
            {
                [YocoCutoverOptions.Prefix + "Enabled"] = "true", [YocoCutoverOptions.Prefix + "OperatorUserId"] = "7",
                [RuntimeOperationGuards.FinancialSetting] = "true"
            });
            var result = (await CreateController(provider, configuration, Substitute.For<IAbpRedisCacheDatabaseProvider>(),
                cutoverStore: store, releaseGate: release).Get()).Result.ShouldBeOfType<ObjectResult>();
            result.StatusCode.ShouldBe(503);
            result.Value.ShouldBeOfType<HealthCheckResponse>().PaymentAvailability.ShouldBe("Unavailable");
            await lease.Received(1).DisposeAsync();
        }

        [Theory]
        [InlineData(null, "true")]
        [InlineData("7", "false")]
        public async Task ExplicitRuntimeHoldsOmitCapabilitiesWithoutAttemptingNormalQualification(string pinnedOperator, string financial)
        {
            using var connection = new SqliteConnection("DataSource=:memory:"); connection.Open();
            using var context = CreateOpenSqliteContext(connection);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>(); provider.GetDbContextAsync().Returns(context);
            var store = Substitute.For<IYocoCutoverStore>(); var release = Substitute.For<IYocoCutoverReleaseGate>();
            var configuration = BuildConfiguration(null, new Dictionary<string, string>
            {
                [YocoCutoverOptions.Prefix + "Enabled"] = "true", [YocoCutoverOptions.Prefix + "OperatorUserId"] = pinnedOperator,
                [RuntimeOperationGuards.FinancialSetting] = financial
            });
            var result = (await CreateController(provider, configuration, Substitute.For<IAbpRedisCacheDatabaseProvider>(),
                cutoverStore: store, releaseGate: release).Get()).Result.ShouldBeOfType<OkObjectResult>();
            var response = result.Value.ShouldBeOfType<HealthCheckResponse>();
            response.PaymentAvailability.ShouldBe("Held");
            response.ContractCapabilities.ShouldNotContain("aqgreen-flexible-joining-v1");
            response.ContractCapabilities.ShouldNotContain("programme-approval-queue-v1");
            response.ContractCapabilities.ShouldNotContain("direct-onyx-checkout-v1");
            await store.DidNotReceive().EnterBusinessAsync(); await release.DidNotReceive().VerifyAsync();
        }
    }
}
