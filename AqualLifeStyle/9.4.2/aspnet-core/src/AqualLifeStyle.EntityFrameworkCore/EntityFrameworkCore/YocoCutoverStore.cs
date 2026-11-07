using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Abp.BackgroundJobs;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class YocoCutoverStore : IYocoCutoverStore, ITransientDependency
    {
        public const string EventType = "Aqua.YocoCutover.ManualHeldEventV1";
        public const string ControlType = "Aqua.YocoCutover.ControlV1";
        public const long LockKey = 0x594F434F435554;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _provider;
        private readonly IUnitOfWorkManager _uows;
        public YocoCutoverStore(IDbContextProvider<AqualLifeStyleDbContext> provider, IUnitOfWorkManager uows)
        { _provider = provider; _uows = uows; }

        public async Task<T> TransactAsync<T>(Func<YocoCutoverSnapshot, Task<T>> action)
        {
            using var uow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted });
            var context = await _provider.GetDbContextAsync();
            AQGreenD10ActivationReceipt newActivation = null;
            var result = await ExecuteAsync(context, async state =>
            {
                var wasActivated = state.PhaseEvidence.Any(e => e?.D10Activation != null);
                var value = await action(state);
                if (!wasActivated) newActivation = state.PhaseEvidence.FirstOrDefault(e => e?.D10Activation != null)?.D10Activation;
                return value;
            });
            newActivation?.EnsureNewCommitAllowed(DateTime.UtcNow);
            await uow.CompleteAsync(); // No acknowledgement may precede this commit.
            return result;
        }

        public async Task<YocoCutoverSnapshot> ReadCommittedControlAsync()
        {
            // No exclusive lock, save or ambient enlistment: first Normal is not
            // usable until its owning transaction has actually committed.
            using var uow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted });
            var context = await _provider.GetDbContextAsync();
            if (!context.Database.IsNpgsql()) throw new InvalidOperationException("D10_ACTIVATION_REQUIRES_POSTGRESQL");
            await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='30s'");
            var controls = await context.Set<BackgroundJobInfo>().AsNoTracking().Where(r => r.JobType == ControlType).Take(2).ToListAsync();
            if (controls.Count > 1) throw new InvalidOperationException("D10_COMMITTED_ACTIVATION_STORAGE_INVALID");
            var state = new YocoCutoverSnapshot { HasMigration55 = (await context.Database.GetAppliedMigrationsAsync())
                .Contains("20261021051549_AddAQGreenSalesEvidenceInvalidation") };
            if (controls.Count == 1)
            {
                var row = controls[0];
                if (row.IsAbandoned || row.TryCount != 0 || row.NextTryTime != DateTime.MaxValue)
                    throw new InvalidOperationException("D10_COMMITTED_ACTIVATION_STORAGE_INVALID");
                using var document = JsonDocument.Parse(row.JobArgs);
                RejectDuplicateControlFields(document.RootElement);
                var saved = JsonSerializer.Deserialize<Control>(row.JobArgs);
                if (saved?.Phase == null || saved.Evidence == null) throw new InvalidOperationException("D10_COMMITTED_ACTIVATION_STORAGE_INVALID");
                state.Phase = saved.Phase; state.PhaseEvidence = saved.Evidence;
            }
            await uow.CompleteAsync(); return state;
        }

        private static void RejectDuplicateControlFields(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new InvalidOperationException("D10_COMMITTED_ACTIVATION_STORAGE_INVALID");
                    RejectDuplicateControlFields(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray()) RejectDuplicateControlFields(item);
        }

        public async Task ReadOnlyAsync(Func<Task> read)
        {
            using var uow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead });
            var context = await _provider.GetDbContextAsync();
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("YOCO_CUTOVER_READ_REQUIRES_POSTGRES_TRANSACTION");
            await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL lock_timeout='3s'; SET LOCAL statement_timeout='30s'");
            using (YocoCutoverDispatch.EnterReadOnly()) { await read(); }
            // Deliberately no Complete: disposal rolls back the read transaction.
        }

        public async Task<YocoCutoverSnapshot> ReadVerifiedSnapshotAsync()
        {
            using var uow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted });
            var context = await _provider.GetDbContextAsync();
            return await ReadVerifiedSnapshotAsync(context);
        }

        public static async Task<YocoCutoverSnapshot> ReadVerifiedSnapshotAsync(AqualLifeStyleDbContext context)
        {
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("YOCO_CUTOVER_REQUIRES_POSTGRES_TRANSACTION");
            await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
            var (state, _, control) = await LoadVerifiedStateAsync(context, shared: true);
            if (control != null)
            {
                using var document = JsonDocument.Parse(control.JobArgs);
                RejectDuplicateControlFields(document.RootElement);
                if (control.IsAbandoned || control.TryCount != 0 || control.NextTryTime != DateTime.MaxValue ||
                    !document.RootElement.TryGetProperty("Evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("YOCO_CUTOVER_READINESS_UNVERIFIED");
            }
            if (state.PhaseEvidence.Count > 256 ||
                (state.PhaseEvidence.Count == 0 ? state.Phase != "Uninitialized" : state.PhaseEvidence.Last()?.Phase != state.Phase))
                throw new InvalidOperationException("YOCO_CUTOVER_READINESS_UNVERIFIED");
            DateTimeOffset previous = default;
            foreach (var e in state.PhaseEvidence)
            {
                if (e == null || !new[] { "ReceiveOnly", "Replay", "Normal" }.Contains(e.Phase) || e.OperatorUserId <= 0 ||
                    e.CommittedAtUtc == default || e.CommittedAtUtc.Offset != TimeSpan.Zero || e.CommittedAtUtc < previous ||
                    e.CommittedAtUtc > DateTimeOffset.UtcNow || e.Phase != "Normal" && e.D10Activation != null)
                    throw new InvalidOperationException("YOCO_CUTOVER_READINESS_UNVERIFIED");
                previous = e.CommittedAtUtc;
            }
            // No save/Complete: release the shared lock by rolling back this read.
            return state;
        }

        private static async Task<(YocoCutoverSnapshot State, List<BackgroundJobInfo> Rows, BackgroundJobInfo Control)>
            LoadVerifiedStateAsync(AqualLifeStyleDbContext context, bool shared)
        {
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("YOCO_CUTOVER_REQUIRES_POSTGRES_TRANSACTION");
            var lockingSql = shared
                ? "SET LOCAL lock_timeout='3s'; SET LOCAL statement_timeout='30s'; SELECT pg_advisory_xact_lock_shared({0})"
                : "SET LOCAL lock_timeout='3s'; SET LOCAL statement_timeout='30s'; SELECT pg_advisory_xact_lock({0})";
            await context.Database.ExecuteSqlRawAsync(lockingSql, LockKey);
            var history = await context.Database.GetAppliedMigrationsAsync();
            const string migration55 = "20261021051549_AddAQGreenSalesEvidenceInvalidation";
            var prefix = context.Database.GetMigrations().Where(x => x != migration55).ToArray();
            var ids = history.ToArray();
            if (prefix.Length != 54 || !(ids.SequenceEqual(prefix) || ids.SequenceEqual(prefix.Concat(new[] { migration55 }))))
                throw new InvalidOperationException("YOCO_CUTOVER_SCHEMA_NOT_SUPPORTED");
            var rows = await context.Set<BackgroundJobInfo>().Where(x => x.JobType == EventType || x.JobType == ControlType)
                .OrderBy(x => x.Id).Take(10001).ToListAsync();
            if (rows.Count > 10000 || rows.Count(x => x.JobType == ControlType) > 1)
                throw new InvalidOperationException("YOCO_CUTOVER_STORAGE_INTEGRITY_REQUIRED");
            var control = rows.SingleOrDefault(x => x.JobType == ControlType);
            var saved = control == null ? null : JsonSerializer.Deserialize<Control>(control.JobArgs);
            var s = new YocoCutoverSnapshot { Phase = control == null ? "Uninitialized" : saved?.Phase,
                PhaseEvidence = saved?.Evidence ?? new(),
                HasMigration55 = ids.Contains(migration55) };
            if (!new[] { "Uninitialized", "ReceiveOnly", "Replay", "Normal" }.Contains(s.Phase))
                throw new InvalidOperationException("YOCO_CUTOVER_PHASE_INVALID");
            foreach (var row in rows.Where(x => x.JobType == EventType))
            {
                using var eventDocument = JsonDocument.Parse(row.JobArgs);
                if (!eventDocument.RootElement.TryGetProperty("RequiresReconciliation", out var classification) ||
                    classification.ValueKind != JsonValueKind.True && classification.ValueKind != JsonValueKind.False)
                    throw new InvalidOperationException("YOCO_CUTOVER_EVENT_CLASSIFICATION_UNVERIFIED");
                var item = JsonSerializer.Deserialize<YocoCutoverEvent>(row.JobArgs);
                if (row.NextTryTime != DateTime.MaxValue || row.IsAbandoned || row.TryCount != 0 ||
                    item?.Contract != "Aqua.YocoCutover.EventV1" || item.BodyBase64 == null ||
                    string.IsNullOrWhiteSpace(item.EventId) || item.EventId.Length > 256 ||
                    string.IsNullOrWhiteSpace(item.Signature) || string.IsNullOrWhiteSpace(item.SignatureTimestamp) || item.VerifiedAtUtc == default ||
                    Convert.FromBase64String(item.BodyBase64).Length > 64 * 1024 ||
                    !new[] { "Pending", "Held", "Applied", "RetainedTest" }.Contains(item.Status) ||
                    !string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(item.BodyBase64))), item.PayloadHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("YOCO_CUTOVER_EVENT_INTEGRITY_REQUIRED");
                item.Id = row.Id; s.Events.Add(item);
            }
            if (s.Events.Select(x => x.EventId).Distinct(StringComparer.Ordinal).Count() != s.Events.Count)
                throw new InvalidOperationException("YOCO_CUTOVER_DUPLICATE_STORAGE_IDENTITY");
            return (s, rows, control);
        }

        public static async Task<T> ExecuteAsync<T>(AqualLifeStyleDbContext context, Func<YocoCutoverSnapshot, Task<T>> action)
        {
            var (s, rows, control) = await LoadVerifiedStateAsync(context, shared: false);
            var hadActivation = s.PhaseEvidence.Any(e => e?.D10Activation != null);
            var result = await action(s);
            var newActivation = hadActivation ? null : s.PhaseEvidence.FirstOrDefault(e => e?.D10Activation != null)?.D10Activation;
            newActivation?.EnsureNewCommitAllowed(DateTime.UtcNow);
            if (s.Phase != "Uninitialized")
            {
                if (control == null) { control = NewRow(ControlType); context.Add(control); }
                if (s.PhaseEvidence.Count > 256) throw new InvalidOperationException("YOCO_CUTOVER_PHASE_RETENTION_LIMIT");
                control.JobArgs = JsonSerializer.Serialize(new Control { Phase = s.Phase, Evidence = s.PhaseEvidence });
            }
            foreach (var item in s.Events)
            {
                var row = item.Id == 0 ? NewRow(EventType) : rows.Single(x => x.Id == item.Id && x.JobType == EventType);
                row.JobArgs = JsonSerializer.Serialize(item);
                if (item.Id == 0) context.Add(row);
            }
            await context.SaveChangesAsync();
            newActivation?.EnsureNewCommitAllowed(DateTime.UtcNow);
            return result;
        }

        private sealed class Control { public string Phase { get; set; } public List<YocoCutoverPhaseEvidence> Evidence { get; set; } }
        private static BackgroundJobInfo NewRow(string type) => new() { JobType = type, JobArgs = "{}", CreationTime = DateTime.UtcNow,
            NextTryTime = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), Priority = BackgroundJobPriority.Normal };

        public async Task<IAsyncDisposable> EnterBusinessAsync()
        {
            string connectionString;
            using (var uow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew, IsTransactional = false }))
            { connectionString = (await _provider.GetDbContextAsync()).Database.GetConnectionString(); await uow.CompleteAsync(); }
            return await EnterBusinessAsync(connectionString);
        }

        public static async Task<IAsyncDisposable> EnterBusinessAsync(string connectionString)
        {
            var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Enlist = false }.ConnectionString);
            var locked = false;
            try
            {
                await connection.OpenAsync();
                await using (var command = new NpgsqlCommand("SET lock_timeout='3s'; SET statement_timeout='30s'; SELECT pg_advisory_lock_shared(@key)", connection))
                { command.Parameters.AddWithValue("key", LockKey); await command.ExecuteNonQueryAsync(); locked = true; }
                await using var read = new NpgsqlCommand("SELECT \"JobType\",\"JobArgs\" FROM public.\"AbpBackgroundJobs\" WHERE \"JobType\" IN (@control,@event)", connection);
                read.Parameters.AddWithValue("control", ControlType); read.Parameters.AddWithValue("event", EventType);
                await using (var reader = await read.ExecuteReaderAsync())
                {
                    string phase = null; var controls = 0;
                    while (await reader.ReadAsync())
                    {
                        using var doc = JsonDocument.Parse(reader.GetString(1));
                        if (reader.GetString(0) == ControlType) { phase = doc.RootElement.GetProperty("Phase").GetString(); controls++; }
                        else
                        {
                            var status = doc.RootElement.GetProperty("Status").GetString();
                            if (doc.RootElement.GetProperty("Contract").GetString() != "Aqua.YocoCutover.EventV1" ||
                                !new[] { "Pending", "Held", "Applied", "RetainedTest" }.Contains(status) ||
                                !doc.RootElement.TryGetProperty("RequiresReconciliation", out var classification) ||
                                classification.ValueKind != JsonValueKind.True && classification.ValueKind != JsonValueKind.False)
                                throw new InvalidOperationException("YOCO_CUTOVER_EVENT_CLASSIFICATION_UNVERIFIED");
                            if (classification.GetBoolean() && status is not ("Applied" or "RetainedTest"))
                                throw new InvalidOperationException("YOCO_CUTOVER_RECONCILIATION_HELD");
                        }
                    }
                    if (controls != 1 || phase != "Normal") throw new InvalidOperationException("YOCO_CUTOVER_BUSINESS_WRITES_FENCED");
                }
                return new Lease(connection);
            }
            catch
            {
                try { if (locked) { await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_shared(@key)", connection);
                        release.Parameters.AddWithValue("key", LockKey); await release.ExecuteNonQueryAsync(); } }
                finally { await connection.DisposeAsync(); }
                throw;
            }
        }

        private sealed class Lease : IAsyncDisposable
        {
            private readonly NpgsqlConnection _connection;
            public Lease(NpgsqlConnection connection) => _connection = connection;
            public async ValueTask DisposeAsync()
            {
                try { await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock_shared(@key)", _connection);
                    command.Parameters.AddWithValue("key", LockKey); await command.ExecuteNonQueryAsync(); }
                finally { await _connection.DisposeAsync(); }
            }
        }
    }
}
