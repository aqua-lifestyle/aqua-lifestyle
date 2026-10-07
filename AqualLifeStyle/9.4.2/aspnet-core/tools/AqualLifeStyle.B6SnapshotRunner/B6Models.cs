using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed class AllowlistContract
{
    public string AllowlistVersion { get; set; } = "";
    public string PackageSchemaVersion { get; set; } = "";
    public string FieldContract { get; set; } = "";
    public Dictionary<string, JsonObject> TransformSemantics { get; set; } = new(StringComparer.Ordinal);
    public List<TableContract> Tables { get; set; } = [];
    public List<string> ProhibitedOutputColumnPatterns { get; set; } = [];
    public Dictionary<string, List<string>> ProhibitedSourceColumns { get; set; } = new(StringComparer.Ordinal);
    public List<string> ProhibitedTablePatterns { get; set; } = [];
}

public sealed class TableContract
{
    public string SourceTable { get; set; } = "";
    public string ScopeRelationship { get; set; } = "";
    public List<string> PrimaryKey { get; set; } = [];
    public List<FieldContract> Fields { get; set; } = [];
    [JsonIgnore] public List<FieldContract> SourceFields =>
        Fields.Where(field => field.Transform != "legacy-participation-order-ordinal").ToList();
}

public sealed class FieldContract
{
    public string Output { get; set; } = "";
    public List<string> SourceColumns { get; set; } = [];
    public string Purpose { get; set; } = "";
    public string Classification { get; set; } = "";
    public string Transform { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed record ExtractionRequest(
    string Dsn, string Output, string SourceSystemId, string RepositoryCommit,
    int TenantId, string PopulationScope, DateTimeOffset AnalysisCutoff,
    bool AuthorizedOperatorAcknowledged);

public sealed record FinalizationRequest(
    string Snapshot, string BaseManifest, string RootDispositions,
    string Inventory, string RuntimeState, string Schema, string InventorySql,
    string AnalysisOutput, string SimulationOutput, string ImpactOutput,
    string Output, string? ForbiddenValues);

public sealed class SourceMetadata
{
    public string PackageSchemaVersion { get; set; } = "";
    public string SourceSystemIdentifier { get; set; } = "";
    public string RepositoryCommit { get; set; } = "";
    public string ExportToolVersion { get; set; } = "";
    public string AllowlistDigest { get; set; } = "";
    public string AnalysisSnapshotCutoff { get; set; } = "";
    public string SourceObservedAt { get; set; } = "";
    public int SourcePostgreSqlMajorVersion { get; set; }
    public string SourcePostgreSqlVersion { get; set; } = "";
    public string SourceServerEncoding { get; set; } = "";
    public string SourceDatabaseCollation { get; set; } = "";
    public List<string> SourceExtensions { get; set; } = [];
    public string SourceMvccSnapshot { get; set; } = "";
    public string SourceDatabaseIdentityEvidence { get; set; } = "";
    public string TransactionMode { get; set; } = "";
    public int AuthorizedTenantId { get; set; }
    public string PopulationScope { get; set; } = "";
    public string SourceSchemaVersion { get; set; } = "";
    public string? EfMigrationHead { get; set; }
    public string ExtractedAt { get; set; } = "";
    public ToolchainIdentity ToolchainIdentity { get; set; } = EmptyToolchain();

    private static ToolchainIdentity EmptyToolchain() => new("", "", new("", "", null), []);
}

public sealed class SnapshotMetadata
{
    public string SnapshotId { get; set; } = "";
    public string AnalysisSnapshotCutoff { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string SourceObservedAt { get; set; } = "";
    public int SourcePostgreSqlMajorVersion { get; set; }
    public string SourcePostgreSqlVersion { get; set; } = "";
    public string SourceServerEncoding { get; set; } = "";
    public string SourceDatabaseCollation { get; set; } = "";
    public List<string> SourceExtensions { get; set; } = [];
    public string SourceSystemIdentifier { get; set; } = "";
    public string RepositoryCommit { get; set; } = "";
    public string ExportToolVersion { get; set; } = "";
    public string SourceSchemaVersion { get; set; } = "";
    public string? EfMigrationHead { get; set; }
    public string SourceMvccSnapshotEvidence { get; set; } = "";
    public string SourceDatabaseIdentityEvidence { get; set; } = "";
    public string TransactionMode { get; set; } = "";
    public int AuthorizedTenantSnapshotKey { get; set; }
    public string PopulationScope { get; set; } = "";
    public string AllowlistVersion { get; set; } = "";
    public string AllowlistDigest { get; set; } = "";
    public string SanitisationSchemeVersion { get; set; } = "";
    public string OrderingComparatorVersion { get; set; } = "";
    public string PseudonymisationSchemeVersion { get; set; } = "";
    public string InventoryRulesVersion { get; set; } = "";
    public string AnalysisAdapterVersion { get; set; } = "";
    public string SimulatorVersion { get; set; } = "";
    public string PackageSchemaVersion { get; set; } = "";
    public string CanonicalEncodingVersion { get; set; } = "";
    public string Classification { get; set; } = "";
    public ToolchainIdentity ToolchainIdentity { get; set; } = EmptyToolchain();

    private static ToolchainIdentity EmptyToolchain() => new("", "", new("", "", null), []);
}

public sealed class RuntimeState
{
    public string RuntimeVersion { get; set; } = "";
    public string RuntimeRole { get; set; } = "";
    public string ContainerId { get; set; } = "";
    public string ContainerName { get; set; } = "";
    public string OwnershipNonce { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DatabaseIdentityEvidence { get; set; } = "";
    public string PostgreSqlVersion { get; set; } = "";
    public int PostgreSqlMajorVersion { get; set; }
    public string WorkerBoundary { get; set; } = "";
    public ToolchainIdentity ToolchainIdentity { get; set; } = EmptyToolchain();

    private static ToolchainIdentity EmptyToolchain() => new("", "", new("", "", null), []);
}

public sealed class RootDispositionFile
{
    public string SchemaVersion { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string SourceSnapshotDigest { get; set; } = "";
    public string BaseManifestDigest { get; set; } = "";
    public List<RootDisposition> Dispositions { get; set; } = [];
}

public sealed class RootDisposition
{
    public string ParticipantSnapshotKey { get; set; } = "";
    public string Disposition { get; set; } = "";
    public string EvidenceType { get; set; } = "";
    public string EvidenceDigest { get; set; } = "";
    public string DecisionReference { get; set; } = "";
    public string ReviewedAt { get; set; } = "";
    public string ReviewedByRole { get; set; } = "";
}

public sealed class SimulationInput
{
    public string SnapshotId { get; set; } = "";
    public string AnalysisSnapshotCutoff { get; set; } = "";
    public List<SimulationComponent> Components { get; set; } = [];
}

public sealed class SimulationComponent
{
    public int TenantSnapshotKey { get; set; }
    public Guid PlacementTreeScopeSnapshotKey { get; set; }
    public string AnalysisSnapshotCutoffUtc { get; set; } = "";
    public List<SimulationCandidate> Candidates { get; set; } = [];
}

public sealed class SimulationCandidate
{
    public int TenantSnapshotKey { get; set; }
    public Guid ParticipantSnapshotKey { get; set; }
    public Guid? AcceptedLegacySponsorSnapshotKey { get; set; }
    public string QualifiedUnderRecruiterAtUtc { get; set; } = "";
    public long LegacyOrderingTieBreakOrdinal { get; set; }
    public string SponsorEvidenceStatus { get; set; } = "";
    public string RootEvidenceStatus { get; set; } = "";
    public string AcquisitionProvenance { get; set; } = "";
    public int EffectiveV1StructuralLevel { get; set; }
    public string SourceEvidenceDigest { get; set; } = "";
}
