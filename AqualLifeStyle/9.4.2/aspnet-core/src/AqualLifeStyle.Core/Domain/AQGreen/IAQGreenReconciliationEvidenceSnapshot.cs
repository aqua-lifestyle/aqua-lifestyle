using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenReconciliationEvidenceSnapshot
    {
        Task<AQGreenReconciliationSourceBoundary> CaptureAsync();
        Task<AQGreenCleanStartState> CaptureCleanStartAsync(int tenantId, DateTime boundaryUtc, string resetId) =>
            throw new InvalidOperationException("D10_CLEAN_START_PROVIDER_VERIFICATION_UNAVAILABLE");
    }

    public sealed class AQGreenReconciliationSourceBoundary
    {
        public DateTime SourceObservedAtUtc { get; set; }
        public string SourcePostgreSqlVersion { get; set; }
        public string SourceMvccSnapshotEvidence { get; set; }
        public string SourceDatabaseIdentityEvidence { get; set; }
        public string SourceSchemaVersion { get; set; }
        public string SourceAssemblyDigest { get; set; }
        public string TransactionMode { get; set; }
    }
}
