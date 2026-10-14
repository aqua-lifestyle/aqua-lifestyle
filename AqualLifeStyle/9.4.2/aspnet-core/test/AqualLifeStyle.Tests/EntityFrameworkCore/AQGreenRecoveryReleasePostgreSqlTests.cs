using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.MultiTenancy;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    // Reuses the existing disposable PostgreSQL migration fixture and synthetic member helpers.
    public partial class AQGreenMigrationRollbackPostgreSqlTests
    {
        private string _recoveryRedisEndpoint;
        [Fact]
        public async Task RecoveryRelease_ActualProductionHostPreservesBusinessData_DeniesMutationsAndFailsClosedOnSchemaMismatch()
        {
            using var redis = await RecoveryRedis.StartAsync();
            _recoveryRedisEndpoint = redis.Endpoint;
            await SeedModernAQGreenParticipationAsync(completeJoiningPayment: false);
            await SeedRecoveryReviewerAsync();
            using (var context = CreateDbContext()) DatabaseReleasePreflight.Verify(context);
            var before = await RecoveryBusinessFingerprintAsync();
            using (var api = LaunchRecoveryProcess("AqualLifeStyle.Web.Host"))
            {
                await api.WaitUntilListeningAsync();
                using var client = new HttpClient { BaseAddress = api.Address, Timeout = TimeSpan.FromSeconds(15) };
                foreach (var path in new[] { "/api/payments/yoco/webhook",
                    "/api/services/app/AdminProgrammeParticipation/ApproveProgrammeParticipation",
                    "/api/services/app/ClubMemberProgrammeParticipation/StartEntry",
                    "/api/services/app/AdminCommission/CalculateLatestClosedWeek",
                    "/api/services/app/AdminCommission/ReleaseAsync", "/unknown-controller" })
                {
                    using var response = await client.PostAsync(path, new StringContent("{}"));
                    response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                    response.Headers.GetValues("X-Aqua-Recovery-Suspended").Single().ShouldBe("true");
                    (await response.Content.ReadAsStringAsync()).ShouldContain("APPLICATION_RECOVERY_SUSPENDED");
                }
                using var unauthorized = await client.GetAsync(
                    "/api/services/app/AdminProgrammeParticipation/GetLegacyAQGreenReconciliation");
                new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }.ShouldContain(unauthorized.StatusCode);
                using var export = await client.PostAsync(
                    "/api/services/app/AdminProgrammeParticipation/ExportLegacyAQGreenReconciliationEvidence",
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
                new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }.ShouldContain(export.StatusCode);
                client.DefaultRequestHeaders.TryAddWithoutValidation("__tenant", string.Empty); // Existing explicit Host login contract.
                using var login = await client.PostAsync("/api/TokenAuth/Authenticate", new StringContent(
                    "{\"userNameOrEmailAddress\":\"admin\",\"password\":\"SyntheticRecovery1!\"}",
                    System.Text.Encoding.UTF8, "application/json"));
                login.StatusCode.ShouldBe(HttpStatusCode.OK);
                using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                    loginJson.RootElement.GetProperty("result").GetProperty("accessToken").GetString());
                using var approvedExport = await client.PostAsync(
                    "/api/services/app/AdminProgrammeParticipation/ExportLegacyAQGreenReconciliationEvidence",
                    new StringContent(JsonSerializer.Serialize(new { tenantId = 1, wholeTenant = true,
                        analysisSnapshotCutoffUtc = DateTime.UtcNow }, AQGreenB6CanonicalEvidence.Options),
                        System.Text.Encoding.UTF8, "application/json"));
                approvedExport.StatusCode.ShouldBe(HttpStatusCode.OK);
                using var exportJson = JsonDocument.Parse(await approvedExport.Content.ReadAsStringAsync());
                var package = exportJson.RootElement.GetProperty("result")
                    .Deserialize<AQGreenReconciliationEvidencePackage>(AQGreenB6CanonicalEvidence.Options);
                package.Verify(); package.Manifest.RecordCount.ShouldBe(1);
                package.Records.Single().HistoricalEvidenceIssues.ShouldNotBeEmpty();
                package.Records.Single().RecordedActivatedAt.ShouldBeNull();
                // Even this explicitly authorized reviewer cannot perform a business transition during recovery.
                using var deniedApproval = await client.PostAsync(
                    "/api/services/app/AdminProgrammeParticipation/ApproveProgrammeParticipation", new StringContent("{}"));
                deniedApproval.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                await api.StopAsync();
                api.ExitCode.ShouldBe(0);
            }
            (await RecoveryBusinessFingerprintAsync()).ShouldBe(before);
            using (var context = CreateDbContext())
            {
                (await context.EntryParticipations.SingleAsync()).ActivatedAt.ShouldBeNull();
                (await context.AQGreenNetworkPlacements.CountAsync()).ShouldBe(0);
                (await context.MemberPayments.CountAsync()).ShouldBe(0);
            }

            // Rehearse restoration into a separate empty database; never roll back protected history.
            var restoreDatabase = _databaseName + "_restore";
            await RecoveryRedis.DockerAsync("exec", _containerName, "pg_dump", "-U", "aqualifestyle",
                "-d", _databaseName, "-Fc", "-f", "/tmp/aqgreen-synthetic-recovery.dump");
            var backupChecksum = await RecoveryRedis.DockerAsync("exec", _containerName, "sha256sum", "/tmp/aqgreen-synthetic-recovery.dump");
            await RecoveryRedis.DockerAsync("exec", _containerName, "createdb", "-U", "aqualifestyle", restoreDatabase);
            await RecoveryRedis.DockerAsync("exec", _containerName, "pg_restore", "-U", "aqualifestyle",
                "-d", restoreDatabase, "--exit-on-error", "/tmp/aqgreen-synthetic-recovery.dump");
            var restoredConnection = new NpgsqlConnectionStringBuilder(BuildTestConnectionString()) { Database = restoreDatabase }.ConnectionString;
            (await RecoveryBusinessFingerprintAsync(restoredConnection)).ShouldBe(before);
            using (var restored = new AqualLifeStyleDbContext(new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                .UseNpgsql(restoredConnection).Options)) DatabaseReleasePreflight.Verify(restored);
            (await RecoveryRedis.DockerAsync("exec", _containerName, "sha256sum", "/tmp/aqgreen-synthetic-recovery.dump")).ShouldBe(backupChecksum);

            // A future schema must reject this release without seeding, down-migration or repair.
            await ExecuteAsync("INSERT INTO \"__EFMigrationsHistory\" VALUES ('20990101000000_SyntheticFutureRelease', '8.0.8')");
            before = await RecoveryBusinessFingerprintAsync();
            using (var api = LaunchRecoveryProcess("AqualLifeStyle.Web.Host"))
            {
                await api.WaitForFailureAsync(); api.ExitCode.ShouldBe(1);
                api.Log.ShouldContain("Database release precondition failed");
            }
            (await RecoveryBusinessFingerprintAsync()).ShouldBe(before);
            await ExecuteAsync("DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='20990101000000_SyntheticFutureRelease'");

            // Recovery cannot silently invoke the separately authorized schema/seed executable.
            before = await RecoveryBusinessFingerprintAsync();
            using (var migrator = LaunchRecoveryProcess("AqualLifeStyle.Migrator"))
            {
                await migrator.WaitForFailureAsync(); migrator.ExitCode.ShouldBe(1);
                migrator.Log.ShouldContain("Business processing is suspended during API recovery");
            }
            (await RecoveryBusinessFingerprintAsync()).ShouldBe(before);
        }

        [Fact]
        public async Task RecoveryRelease_SchemaPreflightRejectsMissingAndGappedHistoryWithoutWrites()
        {
            await using var context = CreateDbContext();
            var migrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
            migrations.Length.ShouldBeGreaterThan(1);
            await using var transaction = await context.Database.BeginTransactionAsync();
            foreach (var migration in new[] { migrations.Last(), migrations[migrations.Length / 2] })
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"={migration}");
                Should.Throw<InvalidOperationException>(() => DatabaseReleasePreflight.Verify(context));
            }
            await transaction.RollbackAsync();
            DatabaseReleasePreflight.Verify(context);
            (await context.Database.GetAppliedMigrationsAsync()).ShouldBe(migrations);
        }

        private async Task<string> RecoveryBusinessFingerprintAsync(string connectionString = null)
        {
            await using var connection = new NpgsqlConnection(connectionString ?? BuildTestConnectionString()); await connection.OpenAsync();
            var names = new List<string>();
            await using (var command = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE' AND table_name NOT IN ('DataProtectionKeys','AbpAuditLogs','AbpUserLoginAttempts') ORDER BY table_name", connection))
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            var hashes = new List<string>();
            foreach (var name in names)
            {
                var quoted = '"' + name.Replace("\"", "\"\"") + '"';
                await using var command = new NpgsqlCommand("SELECT md5(COALESCE(string_agg(h, ',' ORDER BY h),'')) FROM (SELECT md5(row_to_json(t)::text) h FROM " + quoted + " t) rows", connection);
                hashes.Add(name + ":" + (string)await command.ExecuteScalarAsync());
            }
            return string.Join("\n", hashes);
        }

        private async Task SeedRecoveryReviewerAsync()
        {
            await using var context = CreateDbContext();
            // This fixture models an existing initialized database. The API must not create its baseline.
            new AqualLifeStyle.EntityFrameworkCore.Seed.Host.DefaultLanguagesCreator(context).Create();
            if (!await context.Tenants.AnyAsync(t => t.Id == 1))
                context.Tenants.Add(new Tenant("Default", "Synthetic recovery scope") { Id = 1 });
            var area = Area.Create(1, "RECOVERY", "Synthetic recovery Area"); context.Areas.Add(area);
            var user = await context.Users.SingleAsync(u => u.Id == 1);
            user.Password = new PasswordHasher<User>().HashPassword(user, "SyntheticRecovery1!");
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            var role = new Role(null, "SyntheticRecoveryReviewer", "Synthetic recovery reviewer"); context.Roles.Add(role);
            await context.SaveChangesAsync();
            var customer = await context.Customers.SingleAsync(c => c.Id == 1);
            customer.AssignInitialArea(area, DateTime.UtcNow, "Synthetic recovery fixture only");
            context.CustomerAreaAssignments.Add(customer.AreaAssignments.Single());
            context.UserRoles.Add(new UserRole(null, 1, role.Id));
            foreach (var permission in new[] { AquaPermissions.Admin.AllTenants,
                AquaPermissions.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation,
                AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence })
                context.Permissions.Add(new RolePermissionSetting { TenantId = null, RoleId = role.Id, Name = permission, IsGranted = true });
            await context.SaveChangesAsync();
        }

        private RecoveryProcess LaunchRecoveryProcess(string assemblyName)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "AqualLifeStyle.sln"))) root = root.Parent;
            root.ShouldNotBeNull();
            var directory = Path.Combine(root.FullName, "src", assemblyName, "bin", "Release", "net8.0");
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Aqua offline recovery synthetic", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(Path.Combine(directory, assemblyName + ".dll"));
            if (assemblyName == "AqualLifeStyle.Migrator") start.ArgumentList.Add("-q");
            start.Environment.Clear();
            start.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
            foreach (var pair in new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production", ["ASPNETCORE_URLS"] = "http://127.0.0.1:" + port,
                ["App__Recovery__Enabled"] = "true", ["ConnectionStrings__Default"] = BuildTestConnectionString(),
                ["App__ServerRootAddress"] = "https://api.example.test", ["App__ClientRootAddress"] = "https://app.example.test",
                ["App__CorsOrigins"] = "https://app.example.test", ["Authentication__JwtBearer__SecurityKey"] = new string('s', 64),
                ["DataProtection__CertificateBase64"] = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, "synthetic")),
                ["DataProtection__CertificatePassword"] = "synthetic", ["Redis__Configuration"] = _recoveryRedisEndpoint,
                ["Bird__Enabled"] = "false", ["Bird__ApiKey"] = "", ["Yoco__Mode"] = "", ["Yoco__SecretKey"] = "", ["Yoco__WebhookSecret"] = "",
                ["App__WeeklyCommissions__Enabled"] = "false", ["App__EntryMonthlyObligations__Enabled"] = "false", ["SEED_DEMO_DATA"] = "false",
                ["AQGreenV2Demo__Enabled"] = "false", ["AQGreenV2Pilot__Enabled"] = "false"
            }) start.Environment[pair.Key] = pair.Value;
            return new RecoveryProcess(start, new Uri("http://127.0.0.1:" + port),
                Path.Combine(root.FullName, "TestResults", "b7-offline", assemblyName + "-" + Guid.NewGuid().ToString("N") + ".log"));
        }

        private sealed class RecoveryProcess : IDisposable
        {
            private readonly Process _process;
            private readonly ConcurrentQueue<string> _logs = new();
            private readonly string _logPath;
            public Uri Address { get; }
            public int ExitCode => _process.ExitCode;
            public string Log => string.Join("\n", _logs);
            public RecoveryProcess(ProcessStartInfo start, Uri address, string logPath)
            {
                Address = address; _logPath = logPath; _process = new Process { StartInfo = start };
                _process.OutputDataReceived += (_, e) => { if (e.Data != null) _logs.Enqueue(e.Data); };
                _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _logs.Enqueue(e.Data); };
                _process.Start(); _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
            }
            public async Task WaitUntilListeningAsync()
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
                for (var i = 0; i < 100; i++)
                {
                    if (_process.HasExited) throw new InvalidOperationException("Recovery host exited before listening. See synthetic release log.");
                    try { using var response = await client.GetAsync(new Uri(Address, "/unknown-controller"));
                        if (response.Headers.Contains("X-Aqua-Recovery-Suspended")) return; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }
                    await Task.Delay(200);
                }
                throw new TimeoutException("Synthetic recovery host did not start.");
            }
            public async Task WaitForFailureAsync()
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _process.WaitForExitAsync(timeout.Token);
            }
            public async Task StopAsync()
            {
                using var signal = Process.Start(new ProcessStartInfo("kill", "-TERM " + _process.Id) { UseShellExecute = false });
                await signal.WaitForExitAsync(); await WaitForFailureAsync();
            }
            public void Dispose()
            {
                if (!_process.HasExited) { _process.Kill(entireProcessTree: true); _process.WaitForExit(); }
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)); File.WriteAllText(_logPath, Log);
                _process.Dispose();
            }
        }

        private sealed class RecoveryRedis : IDisposable
        {
            private readonly string _name = "aqgreen-recovery-redis-" + Guid.NewGuid().ToString("N");
            public string Endpoint { get; private set; }
            public static async Task<RecoveryRedis> StartAsync()
            {
                var redis = new RecoveryRedis();
                try
                {
                    await DockerAsync("run", "--pull=never", "-d", "--name", redis._name,
                        "-p", "127.0.0.1::6379", "redis:7-alpine", "--save", "", "--appendonly", "no");
                    redis.Endpoint = (await DockerAsync("port", redis._name, "6379/tcp")).Trim();
                    for (var i = 0; i < 30; i++)
                    {
                        try { if ((await DockerAsync("exec", redis._name, "redis-cli", "ping")).Trim() == "PONG") return redis; }
                        catch (InvalidOperationException) { }
                        await Task.Delay(100);
                    }
                    throw new TimeoutException("Isolated Redis was not ready.");
                }
                catch { redis.Dispose(); throw; }
            }
            public static async Task<string> DockerAsync(params string[] arguments)
            {
                var start = new ProcessStartInfo("docker") { UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
                using var process = Process.Start(start); var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(timeout.Token);
                if (process.ExitCode != 0) throw new InvalidOperationException("Synthetic Redis command failed: " + await error);
                return await output;
            }
            public void Dispose() => DockerAsync("rm", "-f", _name).GetAwaiter().GetResult();
        }
    }
}
