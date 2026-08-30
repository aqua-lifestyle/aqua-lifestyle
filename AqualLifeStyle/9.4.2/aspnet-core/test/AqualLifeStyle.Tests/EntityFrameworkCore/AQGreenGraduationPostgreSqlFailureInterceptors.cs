using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Events.Bus;
using Abp.Events.Bus.Entities;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public enum AQGreenGraduationPostgreSqlFailureMode
    {
        None,
        SerializationFailure,
        KnownGraduationCollision,
        UnknownUniqueViolation,
        CommitOutcomeUnknown,
        AfterOnyxTracked,
        AfterDecisionTracked,
        DuringEvidenceInsert,
        DeferredManifestValidation
    }

    internal sealed class AQGreenGraduationPostgreSqlFailureState
    {
        public static readonly AQGreenGraduationPostgreSqlFailureState Shared = new();
        private int _injectionClaimed;

        public string ConnectionString { get; private set; }
        public AQGreenGraduationPostgreSqlFailureMode Mode { get; set; }
        public ConcurrentDictionary<Guid, byte> LockContextIds { get; } = new();
        public ConcurrentDictionary<Guid, byte> SaveContextIds { get; } = new();
        public ConcurrentDictionary<Guid, byte> CommandContextIds { get; } = new();
        public string ActualSqlState { get; set; }
        public string ActualConstraintName { get; set; }
        public bool CommitWasDurableBeforeInjectedFailure { get; set; }
        public bool SawOnyxTracked { get; set; }
        public bool SawDecisionTracked { get; set; }
        public bool SawEvidenceTracked { get; set; }

        public void Reset(string connectionString = null)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
                ConnectionString = connectionString;
            Mode = AQGreenGraduationPostgreSqlFailureMode.None;
            Interlocked.Exchange(ref _injectionClaimed, 0);
            LockContextIds.Clear();
            SaveContextIds.Clear();
            CommandContextIds.Clear();
            ActualSqlState = null;
            ActualConstraintName = null;
            CommitWasDurableBeforeInjectedFailure = false;
            SawOnyxTracked = false;
            SawDecisionTracked = false;
            SawEvidenceTracked = false;
        }

        public bool TryClaim(AQGreenGraduationPostgreSqlFailureMode mode) =>
            Mode == mode && Interlocked.CompareExchange(ref _injectionClaimed, 1, 0) == 0;
    }

    internal sealed class AQGreenGraduationPostgreSqlSaveChangesInterceptor
        : SaveChangesInterceptor
    {
        public static readonly AQGreenGraduationPostgreSqlSaveChangesInterceptor Shared = new();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return result;
            var state = AQGreenGraduationPostgreSqlFailureState.Shared;
            state.SaveContextIds.TryAdd(context.ContextId.InstanceId, 0);
            state.SawOnyxTracked |= context.ChangeTracker.Entries<OnyxParticipation>()
                .Any(entry => entry.State == EntityState.Added);
            state.SawDecisionTracked |= context.ChangeTracker
                .Entries<OnyxGraduationDecision>()
                .Any(entry => entry.State == EntityState.Added);
            state.SawEvidenceTracked |= context.ChangeTracker
                .Entries<AQGreenV2GraduationEvidence>()
                .Any(entry => entry.State == EntityState.Added);

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.SerializationFailure))
            {
                await RaisePostgreSqlAsync(
                    context,
                    "40001",
                    "B5.2 controlled serialization failure",
                    cancellationToken);
            }

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.UnknownUniqueViolation))
            {
                try
                {
                    await ExecuteInCurrentTransactionAsync(
                        context,
                        """
                        INSERT INTO public."AQGreenPlacementTreeScopes" ("Id", "TenantId")
                        SELECT "Id", "TenantId"
                        FROM public."AQGreenPlacementTreeScopes"
                        ORDER BY "Id"
                        LIMIT 1;
                        """,
                        cancellationToken);
                }
                catch (PostgresException exception)
                {
                    state.ActualSqlState = exception.SqlState;
                    state.ActualConstraintName = exception.ConstraintName;
                    throw;
                }
            }

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.KnownGraduationCollision))
            {
                await PersistCompetingCoherentGraduationAsync(cancellationToken);
            }

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.AfterOnyxTracked))
            {
                if (!state.SawOnyxTracked)
                    throw new InvalidOperationException(
                        "The controlled Onyx-stage failure did not observe a tracked Onyx entity.");
                await RaisePostgreSqlAsync(
                    context,
                    "P0001",
                    "B5.2 controlled failure after Onyx tracking",
                    cancellationToken);
            }

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.AfterDecisionTracked))
            {
                if (!state.SawDecisionTracked)
                    throw new InvalidOperationException(
                        "The controlled decision-stage failure did not observe a tracked decision.");
                await RaisePostgreSqlAsync(
                    context,
                    "P0001",
                    "B5.2 controlled failure after decision tracking",
                    cancellationToken);
            }

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.DuringEvidenceInsert))
            {
                if (!state.SawEvidenceTracked)
                    throw new InvalidOperationException(
                        "The controlled evidence failure did not observe tracked evidence.");
                await ExecuteInCurrentTransactionAsync(
                    context,
                    """
                    CREATE FUNCTION public.b52_reject_evidence_insert()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'B5.2 controlled evidence insert failure'
                            USING ERRCODE = 'P0001';
                    END;
                    $function$;

                    CREATE TRIGGER b52_reject_evidence_insert
                    BEFORE INSERT ON public."AQGreenV2GraduationEvidenceNodes"
                    FOR EACH ROW
                    EXECUTE FUNCTION public.b52_reject_evidence_insert();
                    """,
                    cancellationToken);
            }

            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.DeferredManifestValidation))
            {
                var evidence = context.ChangeTracker
                    .Entries<AQGreenV2GraduationEvidence>()
                    .Single(entry => entry.State == EntityState.Added);
                evidence.Property(nameof(AQGreenV2GraduationEvidence.EvidenceNodeCount))
                    .CurrentValue = 30;
            }

            return result;
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var postgres = Find<PostgresException>(eventData.Exception);
            if (postgres != null)
            {
                var state = AQGreenGraduationPostgreSqlFailureState.Shared;
                state.ActualSqlState = postgres.SqlState;
                state.ActualConstraintName = postgres.ConstraintName;
            }
            return Task.CompletedTask;
        }

        private static async Task PersistCompetingCoherentGraduationAsync(
            CancellationToken cancellationToken)
        {
            var structuralEvidence = AQGreenGraduationPostgreSqlEvaluator.Shared.LastResult ??
                throw new InvalidOperationException(
                    "A captured structural result is required for collision injection.");
            await using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql(AQGreenGraduationPostgreSqlFailureState.Shared.ConnectionString)
                    .Options);
            context.EntityChangeEventHelper = NullEntityChangeEventHelper.Instance;
            context.EventBus = NullEventBus.Instance;
            context.SuppressAutoSetTenantId = true;

            var participation = await context.EntryParticipations.SingleAsync(
                item => item.Id == structuralEvidence.ParticipantId,
                cancellationToken);
            var loan = await context.OnyxLoanAgreements.SingleAsync(
                item => item.EntryParticipationId == participation.Id,
                cancellationToken);
            var membershipId = await context.Memberships
                .Where(item => item.MembershipType ==
                               AqualLifeStyle.Domain.Enums.MembershipType.Onyx &&
                               item.IsActive)
                .Select(item => item.Id)
                .FirstAsync(cancellationToken);
            var onyx = OnyxParticipation.GraduateFromAQGreenIndependently(
                participation,
                loan,
                membershipId,
                OnyxPlanTerms.FromCanonicalAcceptedAgreement(loan),
                structuralEvidence.Cutoff);
            var decision = OnyxGraduationDecision.RecordPlacementV2Approval(
                participation,
                loan,
                onyx,
                structuralEvidence,
                administratorUserId: 3001,
                "Competing coherent B5.2 graduation",
                structuralEvidence.Cutoff);
            var evidence = AQGreenV2GraduationEvidence.Capture(
                decision,
                structuralEvidence);
            context.AddRange(onyx, decision, evidence);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task RaisePostgreSqlAsync(
            DbContext context,
            string sqlState,
            string message,
            CancellationToken cancellationToken)
        {
            try
            {
                await ExecuteInCurrentTransactionAsync(
                    context,
                    $"DO $block$ BEGIN RAISE EXCEPTION '{message}' USING ERRCODE = '{sqlState}'; END $block$;",
                    cancellationToken);
            }
            catch (PostgresException exception)
            {
                var state = AQGreenGraduationPostgreSqlFailureState.Shared;
                state.ActualSqlState = exception.SqlState;
                state.ActualConstraintName = exception.ConstraintName;
                throw;
            }
        }

        private static async Task ExecuteInCurrentTransactionAsync(
            DbContext context,
            string sql,
            CancellationToken cancellationToken)
        {
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            var transaction = (NpgsqlTransaction)(context.Database.CurrentTransaction?
                .GetDbTransaction() ?? throw new InvalidOperationException(
                    "The controlled provider failure requires an active transaction."));
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static T Find<T>(Exception exception) where T : Exception
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is T match) return match;
            return null;
        }
    }

    internal sealed class AQGreenGraduationPostgreSqlCommandInterceptor
        : DbCommandInterceptor
    {
        public static readonly AQGreenGraduationPostgreSqlCommandInterceptor Shared = new();

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Record(command, eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Record(command, eventData);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            Record(command, eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Record(command, eventData);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Record(command, eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command, eventData);
            return ValueTask.FromResult(result);
        }

        private static void Record(DbCommand command, CommandEventData eventData)
        {
            if (eventData.Context != null)
            {
                var state = AQGreenGraduationPostgreSqlFailureState.Shared;
                state.CommandContextIds.TryAdd(
                    eventData.Context.ContextId.InstanceId,
                    0);
                if (command.CommandText.Contains(
                        "pg_advisory_xact_lock",
                        StringComparison.Ordinal))
                {
                    state.LockContextIds.TryAdd(
                        eventData.Context.ContextId.InstanceId,
                        0);
                }
            }
        }
    }

    internal sealed class AQGreenGraduationPostgreSqlTransactionInterceptor
        : DbTransactionInterceptor
    {
        public static readonly AQGreenGraduationPostgreSqlTransactionInterceptor Shared = new();

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var state = AQGreenGraduationPostgreSqlFailureState.Shared;
            if (state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.CommitOutcomeUnknown))
            {
                state.CommitWasDurableBeforeInjectedFailure = true;
                throw new NpgsqlException(
                    "B5.2 controlled lost commit acknowledgement after provider commit.");
            }
            return Task.CompletedTask;
        }
    }

    internal sealed class AQGreenGraduationPostgreSqlConnectionInterceptor
        : DbConnectionInterceptor
    {
        public static readonly AQGreenGraduationPostgreSqlConnectionInterceptor Shared = new();

        public override InterceptionResult ConnectionClosing(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            ThrowAfterDurableCommitIfRequested();
            return result;
        }

        public override ValueTask<InterceptionResult> ConnectionClosingAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            ThrowAfterDurableCommitIfRequested();
            return ValueTask.FromResult(result);
        }

        private static void ThrowAfterDurableCommitIfRequested()
        {
            var state = AQGreenGraduationPostgreSqlFailureState.Shared;
            if (!state.TryClaim(AQGreenGraduationPostgreSqlFailureMode.CommitOutcomeUnknown))
                return;
            state.CommitWasDurableBeforeInjectedFailure = true;
            throw new NpgsqlException(
                "B5.2 controlled lost acknowledgement while closing the committed provider connection.");
        }
    }
}
