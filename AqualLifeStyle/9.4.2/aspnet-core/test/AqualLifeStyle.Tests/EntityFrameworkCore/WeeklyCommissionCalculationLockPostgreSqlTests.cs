using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class WeeklyCommissionCalculationLockPostgreSqlTests : IAsyncLifetime
    {
        private readonly string _containerName =
            $"weekly-commission-lock-pg-{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public WeeklyCommissionCalculationLockPostgreSqlTests()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            await RunDockerAsync(
                $"run -d --name {_containerName} -e POSTGRES_DB=aqualifestyle -e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle -p {_hostPort}:5432 postgres:16-alpine");

            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(ConnectionString);
                    await connection.OpenAsync();
                    return;
                }
                catch when (attempt < 29)
                {
                    await Task.Delay(1000);
                }
            }
        }

        public Task DisposeAsync()
        {
            return RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);
        }

        [Fact]
        public async Task TransactionLock_SerializesTheSameWeeklyEngineKey()
        {
            await using var secondConnection = new NpgsqlConnection(ConnectionString);
            await secondConnection.OpenAsync();
            await using var secondTransaction = await secondConnection.BeginTransactionAsync();
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            await context.Database.OpenConnectionAsync();
            await using var firstTransaction =
                await context.Database.BeginTransactionAsync();
            var contextProvider =
                Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            contextProvider.GetDbContext().Returns(context);
            var calculationLock = new WeeklyCommissionCalculationLock(contextProvider);

            await calculationLock.AcquireAsync();
            (await TryAcquireAsync(secondConnection, secondTransaction)).ShouldBeFalse();

            await firstTransaction.CommitAsync();

            (await TryAcquireAsync(secondConnection, secondTransaction)).ShouldBeTrue();
        }

        [Fact]
        public async Task AreaActivationLock_SerializesTheSameAreaOnly()
        {
            const int tenantId = 42;
            await using var secondConnection = new NpgsqlConnection(ConnectionString);
            await secondConnection.OpenAsync();
            await using var secondTransaction = await secondConnection.BeginTransactionAsync();
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            await context.Database.OpenConnectionAsync();
            await using var firstTransaction =
                await context.Database.BeginTransactionAsync();
            var contextProvider =
                Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            contextProvider.GetDbContext().Returns(context);
            var areaLock = new AreaActivationStateLock(contextProvider);
            var areaClock = new AreaActivationStateClock(contextProvider);

            var databaseTime = await areaClock.GetUtcNowAsync();
            databaseTime.Kind.ShouldBe(DateTimeKind.Utc);
            await areaLock.AcquireAsync(tenantId);
            (await TryAcquireAsync(
                secondConnection,
                secondTransaction,
                AreaActivationStateLock.LockKey(tenantId))).ShouldBeFalse();
            (await TryAcquireAsync(
                secondConnection,
                secondTransaction,
                AreaActivationStateLock.LockKey(tenantId + 1))).ShouldBeTrue();

            await firstTransaction.CommitAsync();

            (await TryAcquireAsync(
                secondConnection,
                secondTransaction,
                AreaActivationStateLock.LockKey(tenantId))).ShouldBeTrue();
        }

        private static async Task<bool> TryAcquireAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction)
        {
            return await TryAcquireAsync(
                connection,
                transaction,
                WeeklyCommissionCalculationLock.LockKey);
        }

        private static async Task<bool> TryAcquireAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long lockKey)
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_xact_lock(@key)",
                connection,
                transaction);
            command.Parameters.AddWithValue("key", lockKey);
            return (bool)await command.ExecuteScalarAsync();
        }

        private async Task RunDockerAsync(string arguments, bool throwOnFailure = true)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process.ShouldNotBeNull();
            await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (throwOnFailure && process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Docker command failed: {error}");
            }
        }

        private string ConnectionString =>
            $"Host=localhost;Port={_hostPort};Database=aqualifestyle;Username=aqualifestyle;Password=aqualifestyle";
    }
}
