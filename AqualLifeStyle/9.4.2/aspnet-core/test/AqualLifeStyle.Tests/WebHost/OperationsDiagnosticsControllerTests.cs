using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;

namespace AqualLifeStyle.Tests.WebHost
{
    public class OperationsDiagnosticsControllerTests
    {
        [Fact]
        public async Task Get_ReturnsSanitisedBuildAndDatabaseEvidence()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseSqlite(connection)
                .Options;
            using var dbContext = new AqualLifeStyleDbContext(options);
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContextAsync().Returns(Task.FromResult(dbContext));
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns("Staging");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Deployment:BuildId"] = "commit-abc123",
                    ["Deployment:EnvironmentId"] = "staging-a"
                })
                .Build();
            var controller = new OperationsDiagnosticsController(
                provider,
                configuration,
                environment);

            var result = await controller.GetAsync();

            var response = result.Result.ShouldBeOfType<OkObjectResult>()
                .Value.ShouldBeOfType<OperationsDiagnosticsResponse>();
            response.BuildId.ShouldBe("commit-abc123");
            response.EnvironmentId.ShouldBe("staging-a");
            response.DatabaseFingerprint.Length.ShouldBe(16);
            response.DatabaseFingerprint.ShouldNotContain("memory");
            response.PaymentContractVersion.ShouldBe(
                DeploymentMetadata.PaymentContractVersion);
            response.IsRequiredPaymentMigrationApplied.ShouldBeFalse();
        }

        [Fact]
        public void Controller_RequiresDedicatedDiagnosticsPermission()
        {
            var attribute = typeof(OperationsDiagnosticsController)
                .GetCustomAttributes<AbpAuthorizeAttribute>()
                .Single();

            attribute.Permissions.ShouldContain(AquaPermissions.Admin.Diagnostics.View);
        }
    }
}
