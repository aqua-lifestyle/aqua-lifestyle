using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenReconciliationEvidenceSnapshot
        : IAQGreenReconciliationEvidenceSnapshot, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _provider;
        public AQGreenReconciliationEvidenceSnapshot(IDbContextProvider<AqualLifeStyleDbContext> provider) => _provider = provider;

        public async Task<AQGreenReconciliationSourceBoundary> CaptureAsync()
        {
            var context = await _provider.GetDbContextAsync();
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Frozen evidence requires a transactional PostgreSQL snapshot.");
            // Same read-only/timeout safeguards as the existing B6 extractor.
            await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
            await context.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '10min'");
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'");
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            command.CommandText = """
                SELECT current_setting('transaction_read_only'), current_setting('transaction_isolation'),
                    transaction_timestamp(), current_setting('server_version'), txid_current_snapshot()::text,
                    current_database() || ':' || (SELECT oid::text FROM pg_database WHERE datname = current_database()),
                    (SELECT string_agg(table_name || ':' || column_name || ':' || data_type || ':' || is_nullable,
                        E'\n' ORDER BY table_name, ordinal_position)
                     FROM information_schema.columns WHERE table_schema = 'public')
                """;
            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != "on" || reader.GetString(1) != "repeatable read")
                throw new InvalidOperationException("Frozen evidence snapshot is not REPEATABLE READ READ ONLY.");
            string Hash(string value) => AQGreenB6CanonicalEvidence.Hash(Encoding.UTF8.GetBytes(value));
            return new AQGreenReconciliationSourceBoundary
            {
                SourceObservedAtUtc = reader.GetDateTime(2), SourcePostgreSqlVersion = reader.GetString(3),
                SourceMvccSnapshotEvidence = Hash(reader.GetString(4)),
                SourceDatabaseIdentityEvidence = Hash(reader.GetString(5)), SourceSchemaVersion = Hash(reader.GetString(6)),
                SourceAssemblyDigest = AQGreenB6CanonicalEvidence.Hash(File.ReadAllBytes(GetType().Assembly.Location)),
                TransactionMode = "REPEATABLE READ READ ONLY"
            };
        }
    }
}
