using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Npgsql;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    // TEMPORARY OBSERVATION ONLY: no readiness query, pool mutation or retry.
    internal static class PostgreSqlHostedDiagnostics
    {
        private static readonly object Gate = new();
        private sealed class ObjectIdentity { public long Value; }
        private static long NextIdentity;
        private static readonly ConditionalWeakTable<object, ObjectIdentity> Identities = new();
        private static long ObjectId(object value) => Identities.GetValue(value,
            _ => new ObjectIdentity { Value = Interlocked.Increment(ref NextIdentity) }).Value;
        private static readonly FieldInfo DataSourceField = typeof(NpgsqlConnection)
            .GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);

        private static string Hash(string value) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static object Identity(NpgsqlConnection connection)
        {
            var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            builder.Password = "<REDACTED>";
            var source = DataSourceField?.GetValue(connection);
            int? backend = connection.FullState == System.Data.ConnectionState.Open
                ? connection.ProcessID : null;
            return new
            {
                connectionInstance = ObjectId(connection),
                redactedConnectionString = builder.ConnectionString,
                endpointIdentity = Hash(builder.ConnectionString),
                pooling = builder.Pooling,
                host = builder.Host, port = builder.Port, database = builder.Database,
                backendPid = backend,
                dataSourceObservable = source != null,
                dataSourceInstance = source == null ? (long?)null : ObjectId(source),
                dataSourceType = source?.GetType().FullName
            };
        }

        private static string Redact(string text, NpgsqlConnection connection)
        {
            var password = new NpgsqlConnectionStringBuilder(connection.ConnectionString).Password;
            return string.IsNullOrEmpty(password) ? text : text.Replace(password, "<REDACTED>");
        }

        private static void Record(string phase, NpgsqlConnection connection,
            string file, string member, int line, Exception exception = null)
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("AQGREEN_PG_DIAGNOSTICS");
                if (string.IsNullOrEmpty(root)) return;
                Directory.CreateDirectory(root);
                var row = JsonSerializer.Serialize(new
                {
                    utc = DateTimeOffset.UtcNow,
                    monotonicTicks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency,
                    process = Environment.ProcessId,
                    phase, fixtureSource = Path.GetFileName(file), caller = member, line,
                    connection = Identity(connection),
                    exception = exception == null ? null : Redact(exception.ToString(), connection)
                });
                lock (Gate) File.AppendAllText(Path.Combine(root,
                    $"client-{Environment.ProcessId}.jsonl"), row + Environment.NewLine);
            }
            catch (Exception diagnosticError)
            {
                Console.WriteLine($"[PG-DIAG-ERROR] {phase} {diagnosticError.GetType().Name}");
            }
        }

        private static void FixtureEvent(object fixture, string phase, string containerName = null, int? port = null,
            [CallerMemberName] string member = "")
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("AQGREEN_PG_DIAGNOSTICS");
                if (string.IsNullOrEmpty(root)) return;
                Directory.CreateDirectory(root);
                var row = JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow,
                    monotonicTicks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency,
                    process = Environment.ProcessId, phase, fixtureType = fixture.GetType().FullName,
                    fixtureInstance = ObjectId(fixture), containerName, port, caller = member });
                lock (Gate) File.AppendAllText(Path.Combine(root, $"client-{Environment.ProcessId}.jsonl"), row + Environment.NewLine);
            }
            catch (Exception diagnosticError) { Console.WriteLine($"[PG-DIAG-ERROR] fixture-event {diagnosticError.GetType().Name}"); }
        }

        public static void FixtureCreated(object fixture, string containerName, int port) =>
            FixtureEvent(fixture, "fixture.created", containerName, port);

        public static void TestBody(object fixture, [CallerMemberName] string member = "") =>
            FixtureEvent(fixture, "test.body.begin", member: member);

        public static async Task OpenAsync(NpgsqlConnection connection,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            Record("connection.open.begin", connection, file, member, line);
            try
            {
                await connection.OpenAsync();
                Record("connection.open.success", connection, file, member, line);
            }
            catch (Exception exception)
            {
                Record("connection.open.failure", connection, file, member, line, exception);
                throw;
            }
        }

        public static async Task<int> ExecuteNonQueryAsync(NpgsqlCommand command,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            if (!command.CommandText.TrimStart().StartsWith("CREATE DATABASE ",
                    StringComparison.OrdinalIgnoreCase))
                return await command.ExecuteNonQueryAsync();
            Record("create-database.begin", command.Connection, file, member, line);
            try
            {
                var result = await command.ExecuteNonQueryAsync();
                Record("create-database.success", command.Connection, file, member, line);
                return result;
            }
            catch (Exception exception)
            {
                Record("create-database.failure", command.Connection, file, member, line, exception);
                try
                {
                    using var observer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "docker",
                        ArgumentList = { "aqgreen-diagnostic-capture", command.Connection.Port.ToString() },
                        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                    });
                    if (observer != null)
                    {
                        var stdout = observer.StandardOutput.ReadToEndAsync();
                        var stderr = observer.StandardError.ReadToEndAsync();
                        await observer.WaitForExitAsync();
                        await stdout; await stderr;
                    }
                }
                catch (Exception diagnosticError)
                {
                    Console.WriteLine($"[PG-DIAG-ERROR] failure-capture {diagnosticError.GetType().Name}");
                }
                throw;
            }
        }
    }
}
