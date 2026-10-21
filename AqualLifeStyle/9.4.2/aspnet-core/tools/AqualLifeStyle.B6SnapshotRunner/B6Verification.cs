using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed partial class B6SnapshotPackage
{
    public void CreateBaseManifest(string snapshot, string output) =>
        CanonicalJson.WriteNew(output, CreateBaseManifestValue(snapshot));

    public JsonObject Verify(string snapshot, string manifestPath, string? forbiddenValues = null)
    {
        var (supplied, baseManifest, stage) = PrepareVerification(snapshot, manifestPath);
        if (stage != "BASE_SNAPSHOT")
            throw new B6PackageException("FINAL_ANALYSIS verification requires restored-snapshot inventory authority context.");
        var expected = baseManifest;
        if (!JsonNode.DeepEquals(supplied, expected))
            throw new B6PackageException("Manifest identity, source evidence, row count, tool binding, or authoritative digest mismatches.");
        if (forbiddenValues != null) CheckForbiddenValues(snapshot, forbiddenValues);
        return supplied;
    }

    public async Task<JsonObject> VerifyFinalAsync(string snapshot, string manifestPath, string rootDispositions,
        string inventoryPath, string runtimeState, string schema, string inventorySql, string? forbiddenValues = null)
    {
        var (supplied, baseManifest, stage) = PrepareVerification(snapshot, manifestPath);
        if (stage != "FINAL_ANALYSIS")
            throw new B6PackageException("Authoritative final verification requires a FINAL_ANALYSIS manifest.");
        var expected = BuildFinalManifest(baseManifest,
            await BuildAuthoritativeChainAsync(snapshot, baseManifest, rootDispositions, inventoryPath,
                runtimeState, schema, inventorySql));
        if (!JsonNode.DeepEquals(supplied, expected))
            throw new B6PackageException("Manifest identity, source evidence, row count, tool binding, or authoritative digest mismatches.");
        if (forbiddenValues != null) CheckForbiddenValues(snapshot, forbiddenValues);
        return supplied;
    }

    private (JsonObject Supplied, JsonObject BaseManifest, string Stage) PrepareVerification(
        string snapshot, string manifestPath)
    {
        RequireExpectedFiles(snapshot, "snapshot-metadata.json");
        var rows = LoadRows(snapshot);
        ValidateTypes(rows);
        ValidateRelationships(rows);
        var metadata = CanonicalJson.Read<SnapshotMetadata>(Path.Combine(snapshot, "snapshot-metadata.json"));
        ValidateSnapshotMetadata(metadata, rows);
        var supplied = CanonicalJson.Read(manifestPath).AsObject();
        VerifySelfHash(supplied);
        var stage = supplied["manifestStage"]?.GetValue<string>();
        if (stage is not ("BASE_SNAPSHOT" or "FINAL_ANALYSIS"))
            throw new B6PackageException("Manifest stage is not BASE_SNAPSHOT or FINAL_ANALYSIS.");
        return (supplied, CreateBaseManifestValue(snapshot), stage);
    }

    private JsonObject CreateBaseManifestValue(string snapshot)
    {
        RequireExpectedFiles(snapshot, "snapshot-metadata.json");
        var rows = LoadRows(snapshot);
        ValidateTypes(rows);
        ValidateRelationships(rows);
        var metadata = CanonicalJson.Read<SnapshotMetadata>(Path.Combine(snapshot, "snapshot-metadata.json"));
        ValidateSnapshotMetadata(metadata, rows);
        var analysis = BuildAnalysis(rows, metadata, null);
        var tables = new JsonArray();
        foreach (var table in _allowlist.Value.Tables)
            tables.Add(new JsonObject
            {
                ["table"] = table.SourceTable,
                ["rowCount"] = rows[table.SourceTable].Count,
                ["canonicalDigest"] = B6Values.Sha256Hex(B6Digests.CanonicalTableBytes(table, rows[table.SourceTable])),
                ["schemaContractDigest"] = B6Digests.Tuples(table.Fields.Select(field => new[]
                    { field.Output, B6Values.SqlType(field.Type), field.Type.EndsWith("?", StringComparison.Ordinal) ? "NULL" : "NOT_NULL" })
                    .Append(new[] { "PRIMARY_KEY", string.Join(",", table.PrimaryKey), "UNIQUE" }))
            });
        var result = new JsonObject
        {
            ["manifestStage"] = "BASE_SNAPSHOT",
            ["snapshotId"] = metadata.SnapshotId,
            ["analysisSnapshotCutoff"] = metadata.AnalysisSnapshotCutoff,
            ["sourceObservedAt"] = metadata.SourceObservedAt,
            ["sourcePostgreSqlMajorVersion"] = metadata.SourcePostgreSqlMajorVersion,
            ["sourcePostgreSqlVersion"] = metadata.SourcePostgreSqlVersion,
            ["sourceServerEncoding"] = metadata.SourceServerEncoding,
            ["sourceDatabaseCollation"] = metadata.SourceDatabaseCollation,
            ["sourceExtensions"] = new JsonArray(metadata.SourceExtensions
                .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["sourceSystemIdentifier"] = metadata.SourceSystemIdentifier,
            ["repositoryCommit"] = metadata.RepositoryCommit,
            ["exportToolVersion"] = metadata.ExportToolVersion,
            ["sourceSchemaVersion"] = metadata.SourceSchemaVersion,
            ["efMigrationHead"] = metadata.EfMigrationHead,
            ["sourceMvccSnapshotEvidence"] = metadata.SourceMvccSnapshotEvidence,
            ["sourceDatabaseIdentityEvidence"] = metadata.SourceDatabaseIdentityEvidence,
            ["transactionMode"] = metadata.TransactionMode,
            ["authorizedTenantSnapshotKey"] = metadata.AuthorizedTenantSnapshotKey,
            ["populationScope"] = metadata.PopulationScope,
            ["allowlistVersion"] = metadata.AllowlistVersion,
            ["allowlistDigest"] = metadata.AllowlistDigest,
            ["sanitisationSchemeVersion"] = metadata.SanitisationSchemeVersion,
            ["orderingComparatorVersion"] = metadata.OrderingComparatorVersion,
            ["pseudonymisationSchemeVersion"] = metadata.PseudonymisationSchemeVersion,
            ["inventoryRulesVersion"] = metadata.InventoryRulesVersion,
            ["analysisAdapterVersion"] = metadata.AnalysisAdapterVersion,
            ["simulatorVersion"] = metadata.SimulatorVersion,
            ["packageSchemaVersion"] = metadata.PackageSchemaVersion,
            ["canonicalEncodingVersion"] = metadata.CanonicalEncodingVersion,
            ["toolchainIdentity"] = CanonicalJson.ToNode(metadata.ToolchainIdentity),
            ["tables"] = tables,
            ["relationshipDigest"] = RelationshipDigest(rows),
            ["recruiterCorrectionDigest"] = RecruiterCorrectionDigest(rows),
            ["candidatePopulationDigest"] = analysis["candidatePopulationDigest"]!.DeepClone(),
            ["createdAt"] = metadata.CreatedAt,
            ["digestAlgorithm"] = "SHA-256"
        };
        var stable = result.DeepClone().AsObject();
        stable.Remove("createdAt"); stable.Remove("digestAlgorithm");
        result["overallSnapshotDigest"] = CanonicalJson.Digest(stable);
        AddSelfHash(result);
        return result;
    }

    private void ValidateSnapshotMetadata(SnapshotMetadata metadata,
        Dictionary<string, List<Dictionary<string, string>>> rows)
    {
        var expectedVersions = metadata.PackageSchemaVersion == B6Contract.PackageVersion &&
            metadata.ExportToolVersion == B6Contract.PackageVersion && metadata.AllowlistDigest == _allowlist.Digest &&
            metadata.AllowlistVersion == _allowlist.Value.AllowlistVersion &&
            metadata.SanitisationSchemeVersion == B6Contract.SanitisationVersion &&
            metadata.OrderingComparatorVersion == B6Contract.OrderingVersion &&
            metadata.PseudonymisationSchemeVersion == B6Contract.PseudonymVersion &&
            metadata.InventoryRulesVersion == B6Contract.InventoryVersion &&
            metadata.AnalysisAdapterVersion == B6Contract.AnalysisVersion &&
            metadata.SimulatorVersion == B6Contract.SimulatorVersion &&
            metadata.CanonicalEncodingVersion == B6Contract.EncodingVersion;
        if (!expectedVersions || metadata.Classification != B6Contract.Classification ||
            metadata.TransactionMode != "REPEATABLE READ READ ONLY" || metadata.PopulationScope != B6Contract.PopulationScope ||
            metadata.SourcePostgreSqlMajorVersion != B6Contract.PostgreSqlMajorVersion || !B6SourceSchema.VersionTextMatches(metadata.SourcePostgreSqlVersion) || metadata.SourceServerEncoding != "UTF8" ||
            string.IsNullOrWhiteSpace(metadata.SourceDatabaseCollation) || metadata.SourceExtensions.Count == 0 ||
            !B6Contract.SnapshotId.IsMatch(metadata.SnapshotId) || !B6Contract.ControlledCode.IsMatch(metadata.SourceSystemIdentifier) ||
            !B6Contract.GitSha.IsMatch(metadata.RepositoryCommit) || metadata.RepositoryCommit != _toolchainIdentity.RepositoryCommit ||
            metadata.SourceSchemaVersion != B6SourceSchema.ExpectedHistoryDigest || !B6Contract.Sha256.IsMatch(metadata.SourceMvccSnapshotEvidence) ||
            !B6Contract.Sha256.IsMatch(metadata.SourceDatabaseIdentityEvidence) || metadata.EfMigrationHead != B6Contract.ExpectedMigrationHead)
            throw new B6PackageException("Snapshot metadata fails its sealed PostgreSQL/schema/scope/version contract.");
        B6Values.RequireCutoffNotAfterObservation(metadata.AnalysisSnapshotCutoff, metadata.SourceObservedAt);
        _ = B6Values.ParseTimestamp(metadata.CreatedAt);
        if (CanonicalJson.ToNode(metadata).ToJsonString().Contains("V2EffectiveAt", StringComparison.Ordinal))
            throw new B6PackageException("AnalysisSnapshotCutoff is confused with V2EffectiveAt.");
        metadata.ToolchainIdentity.RequireEqual(_toolchainIdentity);
        var migrations = rows["__EFMigrationsHistory"].OrderBy(row => row["MigrationId"], StringComparer.Ordinal).ToList();
        B6SourceSchema.RequireHistory(migrations.Select(row => (row["MigrationId"], row["ProductVersion"])));
        var migrationDigest = B6Values.Sha256Hex(string.Join('\n', migrations.Select(row => $"{row["MigrationId"]}|{row["ProductVersion"]}")));
        if (migrations.LastOrDefault()?["MigrationId"] != B6Contract.ExpectedMigrationHead || migrationDigest != metadata.SourceSchemaVersion)
            throw new B6PackageException("Snapshot does not match the authoritative EF migration history.");
        var tenant = metadata.AuthorizedTenantSnapshotKey.ToString(CultureInfo.InvariantCulture);
        if (rows["AbpTenants"].Select(row => row["Id"]).ToHashSet(StringComparer.Ordinal).SetEquals([tenant]) == false ||
            rows["EntryParticipations"].Any(row => row["TenantId"] != tenant))
            throw new B6PackageException("Snapshot is not mechanically bounded to its declared Tenant.");
        foreach (var table in _allowlist.Value.Tables.Where(table => table.SourceTable is not ("AbpTenants" or "__EFMigrationsHistory")))
            if (rows[table.SourceTable].Any(row => row.TryGetValue("TenantId", out var value) && value is not B6Contract.Null && value != tenant))
                throw new B6PackageException($"Snapshot table {table.SourceTable} contains another Tenant.");
    }

    private void ValidateTypes(Dictionary<string, List<Dictionary<string, string>>> rows)
    {
        var prohibited = _allowlist.Value.ProhibitedOutputColumnPatterns.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToList();
        foreach (var table in _allowlist.Value.Tables)
        foreach (var field in table.Fields)
        {
            if (field.Transform != "presence-bool" && prohibited.Any(pattern => pattern.IsMatch(field.Output)))
                throw new B6PackageException($"Prohibited output column {table.SourceTable}.{field.Output}.");
            foreach (var row in rows[table.SourceTable])
                if (B6Values.CanonicalScalar(row[field.Output], field.Type, field.Transform) != row[field.Output])
                    throw new B6PackageException($"Non-canonical value in {table.SourceTable}.{field.Output}.");
        }
    }

    private static void ValidateRelationships(Dictionary<string, List<Dictionary<string, string>>> rows)
    {
        Dictionary<string, Dictionary<string, string>> Map(string name) =>
            rows[name].ToDictionary(row => row["Id"], StringComparer.Ordinal);
        Dictionary<string, string> Require(Dictionary<string, Dictionary<string, string>> map, string key, string relation)
        {
            if (key == B6Contract.Null || !map.TryGetValue(key, out var value))
                throw new B6PackageException($"Broken relationship {relation} -> {key}.");
            return value;
        }
        var tenants = Map("AbpTenants"); var areas = Map("Areas"); var customers = Map("Customers");
        var participants = Map("EntryParticipations"); var attributions = Map("AQGreenRecruitmentAttributions");
        var scopes = Map("AQGreenPlacementTreeScopes"); var invitations = Map("ProgrammeInvitations");
        var periods = Map("EntryCommissionPeriods"); var loans = Map("OnyxLoanAgreements"); var onyx = Map("OnyxParticipations");
        foreach (var area in areas.Values) Require(tenants, area["TenantId"], "Area.TenantId");
        foreach (var customer in customers.Values)
        {
            if (customer["TenantId"] != B6Contract.Null) Require(tenants, customer["TenantId"], "Customer.TenantId");
            if (customer["AreaId"] != B6Contract.Null && Require(areas, customer["AreaId"], "Customer.AreaId")["TenantId"] != customer["TenantId"])
                throw new B6PackageException("Customer Area crosses Tenant boundary.");
        }
        foreach (var participant in participants.Values)
        {
            var customer = Require(customers, participant["CustomerId"], "Participation.Customer");
            Require(tenants, participant["TenantId"], "Participation.Tenant");
            if (customer["TenantId"] != participant["TenantId"]) throw new B6PackageException("Participation Customer crosses Tenant boundary.");
            if (participant["RecruiterCustomerId"] != B6Contract.Null && Require(customers, participant["RecruiterCustomerId"], "Participation.Recruiter")["TenantId"] != participant["TenantId"])
                throw new B6PackageException("Recruiter crosses Tenant boundary.");
            if (!Enum.IsDefined((EntryParticipationStatus)int.Parse(participant["Status"], CultureInfo.InvariantCulture)))
                throw new B6PackageException("Unsupported AQGreen participation status.");
        }
        foreach (var correction in rows["EntryRecruiterCorrections"])
        {
            var participant = Require(participants, correction["EntryParticipationId"], "Correction.Participation");
            foreach (var field in new[] { "PreviousRecruiterCustomerId", "NewRecruiterCustomerId" })
                if (correction[field] != B6Contract.Null && Require(customers, correction[field], $"Correction.{field}")["TenantId"] != participant["TenantId"])
                    throw new B6PackageException("Recruiter correction crosses Tenant boundary.");
        }
        foreach (var decision in rows["EntryParticipationApprovalDecisions"])
            Require(participants, decision["EntryParticipationId"], "Approval.Participation");
        foreach (var attribution in attributions.Values)
        {
            var participant = Require(participants, attribution["ParticipantId"], "Attribution.Participant");
            if (participant["TenantId"] != attribution["TenantId"]) throw new B6PackageException("Attribution participant crosses Tenant boundary.");
            var kind = (AQGreenRecruitmentAttributionKind)int.Parse(attribution["AttributionKind"], CultureInfo.InvariantCulture);
            var source = (AQGreenAcquisitionSource)int.Parse(attribution["AcquisitionSource"], CultureInfo.InvariantCulture);
            if (kind == AQGreenRecruitmentAttributionKind.SponsoredParticipant && source == AQGreenAcquisitionSource.MemberInvitation)
            {
                if (attribution["CreditedSponsorParticipantId"] == B6Contract.Null || attribution["AssignmentReasonPresent"] != "false")
                    throw new B6PackageException("MemberInvitation attribution has invalid source shape.");
                var sponsor = Require(participants, attribution["CreditedSponsorParticipantId"], "Attribution.Sponsor");
                var invitation = Require(invitations, attribution["SourceReferenceId"], "Attribution.Invitation");
                if (sponsor["TenantId"] != attribution["TenantId"] || invitation["TenantId"] != attribution["TenantId"] ||
                    invitation["ProgrammeKey"] != "AQGREEN" || invitation["ProgrammeParticipationId"] != sponsor["Id"] || invitation["IsDeleted"] != "false")
                    throw new B6PackageException("MemberInvitation lacks current non-deleted AQGreen invitation/sponsor evidence.");
            }
            else if (kind == AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot && source == AQGreenAcquisitionSource.AuthorisedDirectAdmission)
            {
                if (attribution["CreditedSponsorParticipantId"] != B6Contract.Null || attribution["ActorPresent"] != "true" || attribution["AssignmentReasonPresent"] != "true")
                    throw new B6PackageException("AuthorisedDirectAdmission lacks actor/reason/root source shape.");
            }
            else throw new B6PackageException("Attribution enum/source shape is not authoritative.");
            if (attribution["SourceReferenceId"] == Guid.Empty.ToString() || attribution["CreditedSponsorParticipantId"] == attribution["ParticipantId"])
                throw new B6PackageException("Attribution source/sponsor identity is invalid.");
        }
        foreach (var confirmation in rows["AQGreenRecruitmentAttributionConfirmations"])
        {
            var attribution = Require(attributions, confirmation["AttributionId"], "Confirmation.Attribution");
            var method = (AQGreenAttributionConfirmationMethod)int.Parse(confirmation["ConfirmationMethod"], CultureInfo.InvariantCulture);
            var expected = (AQGreenAcquisitionSource)int.Parse(attribution["AcquisitionSource"], CultureInfo.InvariantCulture) == AQGreenAcquisitionSource.MemberInvitation
                ? AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance
                : AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation;
            if (confirmation["TenantId"] != attribution["TenantId"] || method != expected || confirmation["ActorPresent"] != "true" ||
                B6Values.ParseTimestamp(confirmation["ConfirmedAt"]) < B6Values.ParseTimestamp(attribution["AttributedAt"]) ||
                confirmation["EvidenceReferenceId"] == Guid.Empty.ToString())
                throw new B6PackageException("Attribution confirmation Tenant/method/chronology/evidence is invalid.");
        }
        foreach (var scope in scopes.Values) Require(tenants, scope["TenantId"], "PlacementScope.Tenant");
        foreach (var placement in rows["AQGreenNetworkPlacements"])
        {
            var scope = Require(scopes, placement["PlacementTreeScopeId"], "Placement.Scope");
            var participant = Require(participants, placement["ParticipantId"], "Placement.Participant");
            if (scope["TenantId"] != placement["TenantId"] || participant["TenantId"] != placement["TenantId"])
                throw new B6PackageException("Existing V2 placement crosses Tenant boundary.");
            if (placement["PlacementParentParticipantId"] != B6Contract.Null && Require(participants, placement["PlacementParentParticipantId"], "Placement.Parent")["TenantId"] != placement["TenantId"])
                throw new B6PackageException("Existing V2 parent crosses Tenant boundary.");
        }
        foreach (var invitation in invitations.Values)
            if (invitation["ProgrammeKey"] != "AQGREEN" || invitation["IsDeleted"] != "false" ||
                Require(participants, invitation["ProgrammeParticipationId"], "Invitation.Sponsor")["TenantId"] != invitation["TenantId"])
                throw new B6PackageException("Delivered invitation is not valid non-deleted AQGreen evidence.");
        foreach (var commission in rows["EntryWeeklyCommissions"])
        {
            var participant = Require(participants, commission["EntryParticipationId"], "Commission.Participation");
            var customer = Require(customers, commission["CustomerId"], "Commission.Customer");
            var period = Require(periods, commission["CommissionPeriodId"], "Commission.Period");
            if (new[] { participant["TenantId"], customer["TenantId"], period["TenantId"], commission["TenantId"] }.Distinct().Count() != 1)
                throw new B6PackageException("Commission crosses Tenant boundary.");
            var payout = (WeeklyCommissionPayoutStatus)int.Parse(commission["PayoutStatus"], CultureInfo.InvariantCulture);
            if (!Enum.IsDefined(payout) || payout == WeeklyCommissionPayoutStatus.Paid &&
                (commission["PaidAt"] == B6Contract.Null || commission["ReleasedAt"] == B6Contract.Null))
                throw new B6PackageException("Commission payout state/timestamps are incoherent.");
        }
        foreach (var loan in loans.Values)
        {
            var participant = Require(participants, loan["EntryParticipationId"], "Loan.Participation");
            var customer = Require(customers, loan["CustomerId"], "Loan.Customer");
            if (new[] { participant["TenantId"], customer["TenantId"], loan["TenantId"] }.Distinct().Count() != 1 ||
                !Enum.IsDefined((OnyxLoanAgreementStatus)int.Parse(loan["Status"], CultureInfo.InvariantCulture)))
                throw new B6PackageException("Loan relationship/state is invalid.");
        }
        foreach (var item in onyx.Values)
        {
            Require(tenants, item["TenantId"], "Onyx.Tenant");
            if (Require(customers, item["CustomerId"], "Onyx.Customer")["TenantId"] != item["TenantId"])
                throw new B6PackageException("Onyx participation customer crosses Tenant boundary.");
            if (item["EntryParticipationId"] != B6Contract.Null && Require(participants, item["EntryParticipationId"], "Onyx.EntryParticipation")["TenantId"] != item["TenantId"])
                throw new B6PackageException("Onyx AQGreen link crosses Tenant boundary.");
            if (item["LoanAgreementId"] != B6Contract.Null && Require(loans, item["LoanAgreementId"], "Onyx.Loan")["TenantId"] != item["TenantId"])
                throw new B6PackageException("Onyx loan link crosses Tenant boundary.");
        }
        foreach (var graduation in rows["OnyxGraduationDecisions"])
        {
            var participant = Require(participants, graduation["EntryParticipationId"], "Graduation.Participation");
            var loan = Require(loans, graduation["LoanAgreementId"], "Graduation.Loan");
            var result = Require(onyx, graduation["OnyxParticipationId"], "Graduation.Onyx");
            var customer = Require(customers, graduation["CustomerId"], "Graduation.Customer");
            if (new[] { participant["TenantId"], loan["TenantId"], result["TenantId"], customer["TenantId"], graduation["TenantId"] }.Distinct().Count() != 1 ||
                result["EntryParticipationId"] != graduation["EntryParticipationId"] || result["LoanAgreementId"] != graduation["LoanAgreementId"])
                throw new B6PackageException("Graduation relationship tuple is inconsistent.");
        }
        foreach (var group in participants.Values.GroupBy(row => row["TenantId"], StringComparer.Ordinal))
        {
            var ordinals = group.Select(row => long.Parse(row["LegacyOrderingTieBreakOrdinal"], CultureInfo.InvariantCulture)).Order().ToList();
            if (!ordinals.SequenceEqual(Enumerable.Range(1, ordinals.Count).Select(value => (long)value)))
                throw new B6PackageException("Participant ordering ordinals are missing or duplicated.");
        }
    }

    private static string RelationshipDigest(Dictionary<string, List<Dictionary<string, string>>> rows)
    {
        var specs = new[]
        {
            new[]{"EntryParticipations","Id","TenantId","CustomerId","RecruiterCustomerId"},
            new[]{"EntryRecruiterCorrections","Id","EntryParticipationId","PreviousRecruiterCustomerId","NewRecruiterCustomerId"},
            new[]{"EntryParticipationApprovalDecisions","Id","EntryParticipationId"},
            new[]{"AQGreenRecruitmentAttributions","Id","TenantId","ParticipantId","CreditedSponsorParticipantId","SourceReferenceId"},
            new[]{"AQGreenRecruitmentAttributionConfirmations","Id","TenantId","AttributionId","EvidenceReferenceId"},
            new[]{"AQGreenNetworkPlacements","Id","TenantId","PlacementTreeScopeId","ParticipantId","PlacementParentParticipantId"},
            new[]{"ProgrammeInvitations","Id","TenantId","ProgrammeParticipationId"},
            new[]{"EntryWeeklyCommissions","Id","TenantId","EntryParticipationId","CustomerId","CommissionPeriodId"},
            new[]{"OnyxLoanAgreements","Id","TenantId","EntryParticipationId","CustomerId"},
            new[]{"OnyxGraduationDecisions","Id","TenantId","CustomerId","EntryParticipationId","LoanAgreementId","OnyxParticipationId"},
            new[]{"OnyxParticipations","Id","TenantId","CustomerId","EntryParticipationId","LoanAgreementId"}
        };
        return B6Digests.Tuples(specs.SelectMany(spec => rows[spec[0]].Select(row =>
            new[] { spec[0] }.Concat(spec.Skip(1).Select(field => row[field])))));
    }

    private static string RecruiterCorrectionDigest(Dictionary<string, List<Dictionary<string, string>>> rows) =>
        B6Digests.Tuples(rows["EntryParticipations"].Select(row => new[] { "P", row["TenantId"], row["Id"], row["RecruiterCustomerId"], row["LegacyOrderingTieBreakOrdinal"] })
            .Concat(rows["EntryRecruiterCorrections"].Select(row => new[] { "C", row["EntryParticipationId"], row["PreviousRecruiterCustomerId"], row["NewRecruiterCustomerId"], row["CorrectedAt"] })));

    private static void AddSelfHash(JsonObject value)
    {
        value.Remove("manifestHash");
        value["manifestHash"] = CanonicalJson.Digest(value);
    }

    private static void VerifySelfHash(JsonObject value)
    {
        var copy = value.DeepClone().AsObject();
        var supplied = copy["manifestHash"]?.GetValue<string>(); copy.Remove("manifestHash");
        if (supplied == null || supplied != CanonicalJson.Digest(copy)) throw new B6PackageException("Manifest hash mismatch or tampering.");
    }

    private static void CheckForbiddenValues(string snapshot, string forbiddenPath)
    {
        List<string> forbidden;
        using (var stream = SecureFiles.OpenRead(forbiddenPath, controlled: false))
        using (var reader = new StreamReader(stream)) forbidden = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var path in Directory.EnumerateFiles(snapshot, "*.csv"))
        {
            using var stream = SecureFiles.OpenRead(path, controlled: true);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            if (forbidden.Any(content.Contains)) throw new B6PackageException("Prohibited representative fixture value was exported.");
        }
    }
}
