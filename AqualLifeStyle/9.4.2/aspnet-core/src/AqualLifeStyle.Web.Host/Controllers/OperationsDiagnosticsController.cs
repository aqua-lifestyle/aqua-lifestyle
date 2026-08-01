using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Web.Host.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Controllers
{
    [ApiController]
    [Route("api/admin/operations-diagnostics")]
    [AbpAuthorize(AquaPermissions.Admin.Diagnostics.View)]
    public sealed class OperationsDiagnosticsController : ControllerBase, ITransientDependency
    {
        public const string RequiredPaymentMigration =
            "20260801092352_AddAQGreenSchedulesAndOnyxGraduation";

        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public OperationsDiagnosticsController(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _dbContextProvider = dbContextProvider;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet]
        public async Task<ActionResult<OperationsDiagnosticsResponse>> GetAsync()
        {
            var dbContext = await _dbContextProvider.GetDbContextAsync();
            var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
            var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
            var connection = dbContext.Database.GetDbConnection();
            var databaseIdentity = string.Join(
                "|",
                dbContext.Database.ProviderName ?? "unknown",
                connection.DataSource ?? "unknown",
                connection.Database ?? "unknown");

            return Ok(new OperationsDiagnosticsResponse
            {
                ApplicationVersion = AppVersionHelper.Version,
                BuildId = DeploymentMetadata.ResolveBuildId(_configuration),
                ImageId = DeploymentMetadata.ResolveImageId(_configuration),
                Environment = _environment.EnvironmentName,
                EnvironmentId = DeploymentMetadata.ResolveEnvironmentId(_configuration),
                PaymentContractVersion = DeploymentMetadata.PaymentContractVersion,
                DatabaseProvider = dbContext.Database.ProviderName ?? "unknown",
                DatabaseFingerprint = CreateFingerprint(databaseIdentity),
                LatestAppliedMigration = applied.LastOrDefault() ?? "none",
                RequiredPaymentMigration = RequiredPaymentMigration,
                IsRequiredPaymentMigrationApplied = applied.Contains(
                    RequiredPaymentMigration,
                    StringComparer.Ordinal),
                AreAllKnownMigrationsApplied = pending.Length == 0
            });
        }

        private static string CreateFingerprint(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }
    }
}
