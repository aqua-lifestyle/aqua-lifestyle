using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Customer11Canonicalisation;

internal sealed class AdapterValidationException(string category) : Exception(category)
{
    internal string Category { get; } = category;
}

internal sealed record VerifiedInput(JsonElement Manifest, AQGreenLegacyCanonicalisationRequest Request,
    string InputSha256, string CaptureSha256);

internal static class Customer11ManifestReader
{
    internal const string ManifestSha256 = "74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416";
    internal const string CaptureSha256 = "b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494";
    internal const string Clone = "dpg-davoaabncjis73f56rj0-a";
    internal const string EffectiveAt = "2026-10-02T05:37:10Z";
    internal const string ObservationAt = "2026-10-03T07:17:10.764589Z";
    internal static readonly Guid Scope = Guid.Parse("06a452ea-9958-5911-8c5b-5d350e516095");
    internal static readonly IReadOnlyDictionary<int, Guid> Identities = new Dictionary<int, Guid>
    {
        [11] = Guid.Parse("4a5b77ef-4e09-4a61-bb2d-e3f65b8087b0"),
        [14] = Guid.Parse("32b4bf58-3208-4634-ad57-db59ea5c50fb"),
        [15] = Guid.Parse("8081c00d-0328-483b-8421-41b066241190"),
        [17] = Guid.Parse("d8cdae0c-584c-433c-9edc-f5e94baaf476"),
        [19] = Guid.Parse("623ca0b4-8ea5-4cb0-a6a4-b42b04129aa0"),
        [18] = Guid.Parse("61ed1b2a-c4ae-40cf-b199-81f774e9afdf")
    };

    // There is no CLI override for these byte identities. The internal overload permits synthetic
    // fixtures to exercise semantic guards after their own digest verification, not operational use.
    internal static VerifiedInput Read(byte[] manifest, byte[] capture) =>
        ReadBound(manifest, capture, ManifestSha256, CaptureSha256);

    internal static VerifiedInput ReadBound(byte[] manifest, byte[] capture, string expectedManifestHash,
        string expectedCaptureHash)
    {
        Check(Hash(manifest) == expectedManifestHash, "ManifestHash");
        Check(Hash(capture) == expectedCaptureHash, "CaptureHash");
        var m = Parse(manifest);
        var source = Get(m, "sourceSnapshot");
        Equal(m, "label", "NEW-LINEAGE CUSTOMER-11 CANDIDATE INPUT");
        Equal(m, "manifestVersion", "Customer11NewLineageCanonicalisationCandidateV1");
        Equal(m, "status", "CANDIDATE_CANONICALISATION_NOT_EXECUTED");
        Check(Int(m, "tenantId") == 1, "Tenant");
        Equal(m, "programme", "AQGreen");
        Check(Int(m, "rootCustomerId") == 11 && Id(m, "rootParticipationId") == Identities[11], "Root");
        Check(Id(m, "candidatePlacementTreeScopeId") == Scope, "Scope");
        Equal(m, "placementSource", AQGreenLegacyCanonicalisationRules.PlacementSource);
        Equal(m, "rulesVersion", AQGreenPlacementRules.CurrentVersion);
        Equal(m, "simulationVersion", AQGreenLegacyCanonicalisationRules.SimulationVersion);
        Equal(m, "comparatorVersion", AQGreenLegacyCanonicalisationRules.ComparatorVersion);
        Equal(m, "uuidOrderingAdapterVersion", "DotNetGuidCompareToOrdinalV2");
        Check(Time(m, "canonicalisationEffectiveAtUtc") == ParseTime(EffectiveAt) &&
              Time(m, "analysisSnapshotCutoffUtc") == ParseTime(EffectiveAt), "EffectiveTimestamp");
        Check(Time(m, "capturedObservationAtUtc") == ParseTime(ObservationAt), "ObservationTimestamp");
        Equal(source, "boundedCaptureSha256", expectedCaptureHash);
        Equal(source, "captureLabel", "NEW RECOVERY-CLONE EVIDENCE CAPTURE");
        Equal(source, "serviceId", Clone);
        Equal(source, "database", "aqualifestyle_1t0a");
        Equal(source, "databaseUser", "aqualifestyle");
        Check(Time(source, "capturedAtUtc") == ParseTime(ObservationAt) &&
              Time(source, "historicalPitrCutoffUtc") == ParseTime(EffectiveAt), "SourceTime");
        Equal(source, "historicalOriginProductionServiceId", "dpg-d9f0jne1a83c73d8m780-a");
        Equal(Get(m, "custody"), "classification", "NEW ARTIFACT — FRESH ACCEPTANCE REQUIRED");
        Equal(Get(m, "custody"), "uninterruptedCustody", "NOT CLAIMED");
        Equal(Get(m, "custody"), "originalAcceptedBytes", "UNAVAILABLE");

        var records = ReadCapture(capture);
        ValidateIdentity(records, source);
        var evidence = records[5];
        var supplement = records[6];
        ValidateCapturePopulation(evidence, supplement);
        var participants = Array(m, "participants");
        Check(participants.Count == 6, "Population");
        var seen = new HashSet<int>();
        var candidates = new List<AQGreenLegacyCanonicalisationCandidate>();
        foreach (var p in participants)
        {
            int customer = Int(p, "customerId");
            Check(Identities.ContainsKey(customer) && seen.Add(customer) &&
                  Id(p, "participationId") == Identities[customer], "ParticipantIdentity");
            Check(Int(p, "tenantId") == 1, "ParticipantTenant");
            Equal(p, "programme", "AQGreen");
            Equal(p, "currentStatus", "Active");
            Check(Int(p, "rawStatus") == 2 && !Bool(p, "isDeleted"), "Lifecycle");
            Equal(p, "sourceEvidenceCaptureSha256", expectedCaptureHash);
            Check(IsDigest(Text(p, "sourceEvidenceDigest")) &&
                  Hash(CanonicalOutputWriter.CanonicalBytes(p, "sourceEvidenceDigest", false)) ==
                  Text(p, "sourceEvidenceDigest"), "ParticipantDigest");
            Equal(p, "acquisitionProvenance", "LegacyUnknownAcquisition");
            foreach (var unknown in new[] { "historicalAcquisitionSource", "historicalInvitation",
                         "historicalParticipantConfirmation", "historicalFinancialQualification",
                         "historicalSecurityState", "historicalActorPermissionSnapshot", "independentBankSettlement" })
                Equal(p, unknown, "UNKNOWN");
            Check(Int(p, "correctionCount") == 0, "Corrections");
            Equal(p, "correctionDisposition", "NO_RETAINED_CORRECTIONS");
            Equal(p, "contradictionDisposition", "NONE_IN_BOUNDED_INSPECTED_EVIDENCE");
            Equal(p, "canonicalisationEligibility", "OWNER_ACCEPTED_BOUNDED_D09_SATISFIED");
            Check(Time(p, "qualifiedUnderRecruiterAtUtc") ==
                  new[] { Time(p, "startedAtUtc"), Time(p, "activatedAtUtc") }.Max(), "OrderingEvidence");
            Check(Time(p, "activatedAtUtc") <= ParseTime(EffectiveAt), "ActivationCutoff");
            bool root = customer == 11;
            Check(root ? IsNull(p, "acceptedLegacySponsorParticipationId") && IsNull(p, "legacyRecruiterCustomerId") :
                  Id(p, "acceptedLegacySponsorParticipationId") == Identities[11] &&
                  Int(p, "legacyRecruiterCustomerId") == 11, "Sponsor");
            Equal(p, "rootEvidenceStatus", root ? "AcceptedRoot" : "NotRoot");
            Equal(p, "sponsorEvidenceStatus", root ? "NotApplicableToRoot" : "AcceptedSponsor");
            Equal(p, "historicalAdministratorApproval", root ? "UNKNOWN" : "RETAINED_POSITIVE_DECISION");
            Equal(p, "approvalEvidenceDisposition", root ? "NO_RETAINED_APPROVAL_ROW" :
                "RETAINED_APPROVED_TRUE_ACTOR_REFERENCE_PRESENT");
            ValidateParticipantEvidence(p, evidence, supplement);
            candidates.Add(new AQGreenLegacyCanonicalisationCandidate
            {
                TenantSnapshotKey = 1,
                ParticipantSnapshotKey = Id(p, "participationId"),
                AcceptedLegacySponsorSnapshotKey = root ? null : Id(p, "acceptedLegacySponsorParticipationId"),
                QualifiedUnderRecruiterAtUtc = Time(p, "qualifiedUnderRecruiterAtUtc"),
                LegacyOrderingTieBreakOrdinal = Long(p, "legacyOrderingTieBreakOrdinal"),
                RootEvidenceStatus = root ? AQGreenLegacyRootEvidenceStatus.AcceptedRoot : AQGreenLegacyRootEvidenceStatus.NotRoot,
                SponsorEvidenceStatus = root ? AQGreenLegacySponsorEvidenceStatus.NotApplicableToRoot : AQGreenLegacySponsorEvidenceStatus.AcceptedSponsor,
                AcquisitionProvenance = AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition,
                EffectiveV1StructuralLevel = StructuralLevel(p),
                SourceEvidenceDigest = Text(p, "sourceEvidenceDigest")
            });
        }
        return new VerifiedInput(m, new AQGreenLegacyCanonicalisationRequest
        {
            TenantSnapshotKey = 1, PlacementTreeScopeSnapshotKey = Scope,
            AnalysisSnapshotCutoffUtc = Time(m, "canonicalisationEffectiveAtUtc"), Candidates = candidates.AsReadOnly()
        }, expectedManifestHash, expectedCaptureHash);
    }

    private static List<JsonElement> ReadCapture(byte[] bytes)
    {
        string text = new UTF8Encoding(false, true).GetString(bytes);
        var lines = text.Split('\n').ToList();
        if (lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        Check(lines.Count == 7 && lines.All(s => !string.IsNullOrWhiteSpace(s)), "CaptureRecordCount");
        var records = lines.Select(s => Parse(Encoding.UTF8.GetBytes(s))).ToList();
        string[] categories = ["DatabaseIdentity", "DatabaseUser", "ServerVersionNumber", "ReadOnlyMode"];
        for (int i = 0; i < 4; i++)
        {
            Equal(records[i], "kind", "identity_guard");
            Equal(records[i], "category", categories[i]);
            Equal(records[i], "result", "PASS");
            if (i == 2)
                Check(Int(records[i], "expected") == 180006 && Int(records[i], "observed") == 180006, "ServerVersionNumber");
            else
            {
                string expected = i == 0 ? "aqualifestyle_1t0a" : i == 1 ? "aqualifestyle" : "on";
                Equal(records[i], "expected", expected);
                Equal(records[i], "observed", expected);
            }
        }
        Equal(records[4], "kind", "identity");
        Equal(records[5], "kind", "bounded_evidence");
        Equal(records[6], "kind", "bounded_supplement");
        return records;
    }

    private static void ValidateIdentity(IReadOnlyList<JsonElement> r, JsonElement source)
    {
        var identity = r[4];
        Equal(identity, "database", Text(source, "database"));
        Equal(identity, "user", Text(source, "databaseUser"));
        Equal(identity, "clone_service_id", Clone);
        Equal(identity, "capture_label", "NEW RECOVERY-CLONE EVIDENCE CAPTURE");
        Equal(identity, "transaction_read_only", "on");
        Equal(identity, "isolation", "repeatable read");
        Check(Time(identity, "snapshot_utc") == Time(source, "capturedAtUtc") &&
              Time(identity, "historical_pitr_cutoff") == ParseTime(EffectiveAt) &&
              Time(identity, "historical_canonicalisation_effective_at") == ParseTime(EffectiveAt), "CaptureIdentityTime");
        // Presentation string is observation evidence; numeric version is the guard.
        Check(!string.IsNullOrWhiteSpace(Text(identity, "version")) &&
              !string.IsNullOrWhiteSpace(Text(r[2], "server_version")), "VersionObservation");
    }

    private static void ValidateCapturePopulation(JsonElement e, JsonElement s)
    {
        foreach (string name in new[] { "participants", "expected", "checks", "customer_security", "joining_checkouts", "payments" })
        {
            var rows = Array(e, name);
            Check(rows.Count == 6 && rows.Select(p => Int(p, "CustomerId")).Distinct().Count() == 6 &&
                  rows.All(p => Identities.ContainsKey(Int(p, "CustomerId"))), "CapturePopulation:" + name);
        }
        Check(Array(e, "webhook_receipts").Count == 6 && Array(e, "approval_decisions").Count == 5,
            "CaptureEvidencePopulation");
        var corrections = Get(e, "recruiter_corrections");
        Check(corrections.ValueKind == JsonValueKind.Null ||
              corrections.ValueKind == JsonValueKind.Array && corrections.GetArrayLength() == 0, "CaptureCorrections");
        foreach (string prefix in new[] { "scope", "placement", "attribution", "confirmation" })
            Check(Int(s, prefix + "_count") == 0, "ExistingRows:" + prefix);
        foreach (string name in new[] { "scopes", "placements", "attributions" })
        {
            var value = Get(s, name);
            Check(value.ValueKind == JsonValueKind.Null ||
                  value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0, "ExistingRows:" + name);
        }
        Check(Int(s, "additional_active_descendant_count") == 0, "AdditionalDescendants");
        var tenant = Get(s, "tenant");
        Check(Int(tenant, "Id") == 1 && Bool(tenant, "IsActive") && !Bool(tenant, "IsDeleted"), "CaptureTenant");
        Check(Array(s, "approval_actor_checks").Count == 5 && Array(s, "checkout_checks").Count == 6, "SupplementPopulation");
    }

    private static void ValidateParticipantEvidence(JsonElement p, JsonElement e, JsonElement s)
    {
        int customer = Int(p, "customerId");
        Guid id = Id(p, "participationId");
        var row = Single(Array(e, "participants"), r => Int(r, "CustomerId") == customer, "CaptureParticipant");
        Check(Id(row, "Id") == id && Int(row, "TenantId") == 1 && Int(row, "Status") == 2 && !Bool(row, "IsDeleted"), "CaptureLifecycle");
        Check(customer == 11 ? IsNull(row, "RecruiterCustomerId") : Int(row, "RecruiterCustomerId") == 11, "CaptureSponsor");
        Check(Time(row, "StartedAt") == Time(p, "startedAtUtc") && Time(row, "ActivatedAt") == Time(p, "activatedAtUtc"), "CaptureOrdering");
        var expected = Single(Array(e, "expected"), r => Int(r, "CustomerId") == customer, "CaptureExpected");
        Check(Id(expected, "ParticipationId") == id, "CaptureExpectedIdentity");
        var check = Single(Array(e, "checks"), r => Int(r, "CustomerId") == customer, "CaptureCheck");
        Check(Id(check, "ParticipationId") == id && Int(check, "CorrectionCount") == 0 &&
              Int(check, "DecisionCount") == (customer == 11 ? 0 : 1), "CaptureCounts");
        foreach (string field in new[] { "ExpectedRecruiterEdge", "RetainedActiveChronology", "ExpectedIdentityAndTenant", "ConfirmedSingleJoiningPaymentBound" })
            Check(Bool(check, field), "CaptureCheck:" + field);
        Check(!Bool(check, "RetainedRejectionExists"), "RetainedRejection");
        Check(Bool(check, "PositiveDecisionActorAndActivationMatch") == (customer != 11), "ApprovalCheck");
        var security = Single(Array(e, "customer_security"), r => Int(r, "CustomerId") == customer, "Security");
        Check(Id(security, "ParticipationId") == id && Int(security, "CustomerTenantId") == 1 && Int(security, "UserTenantId") == 1, "SecurityOwnership");
        foreach (string field in new[] { "CustomerExists", "UserExists", "CustomerIsActive", "UserIsActive" }) Check(Bool(security, field), "Security:" + field);
        Check(!Bool(security, "CustomerIsDeleted") && !Bool(security, "UserIsDeleted"), "SecurityDeleted");
        var locked = Get(security, "UserCurrentlyLockedOut");
        Check(locked.ValueKind == JsonValueKind.Null || locked.ValueKind == JsonValueKind.False, "SecurityLockout");

        Equal(p, "paymentDisposition", "RETAINED_AQUA_CONFIRMED_COMPLETED_JOINING_WITH_MATCHING_YOCO_RECEIPT");
        Equal(p, "paymentOwnershipConsistency", "PASS");
        Equal(p, "paymentPurpose", "AQGreenJoining");
        Equal(p, "currency", "ZAR");
        Check(Decimal(p, "joiningAmount") == 1200m, "Amount");
        Equal(p, "receiptBindingDisposition", "INTERNAL_TENANT_PROGRAMME_CHECKOUT_PAYMENT_BINDINGS_VERIFIED");
        var payment = Single(Array(e, "payments"), r => Id(r, "Id") == Id(p, "joiningPaymentId"), "Payment");
        Check(Id(row, "JoiningPaymentId") == Id(payment, "Id") && Int(payment, "CustomerId") == customer &&
              Int(payment, "TenantId") == 1 && !Bool(payment, "IsDeleted") && Int(payment, "Status") == 1 &&
              Int(payment, "Purpose") == 7 && Decimal(payment, "Amount") == 1200m && Text(payment, "Currency") == "ZAR" &&
              Text(payment, "Provider") == "YOCO" && Int(payment, "MatchingWebhookReceiptCount") == 1 &&
              Bool(payment, "ExternalReferencePresent") && Time(payment, "ConfirmedAt") == Time(p, "paymentConfirmedAtUtc"), "PaymentBinding");
        var checkout = Single(Array(e, "joining_checkouts"), r => Id(r, "Id") == Id(p, "joiningCheckoutId"), "Checkout");
        Check(Id(checkout, "ParticipationId") == id && Id(checkout, "PaymentId") == Id(payment, "Id") &&
              Int(checkout, "CustomerId") == customer && Int(checkout, "TenantId") == 1 && !Bool(checkout, "IsDeleted") &&
              Int(checkout, "Status") == 2 && Int(checkout, "Stage") == 0 && Decimal(checkout, "Amount") == 1200m &&
              Text(checkout, "Currency") == "ZAR" && Bool(checkout, "PaymentOwnershipAmountCurrencyMatch") &&
              Bool(checkout, "ProviderCheckoutIdPresent") && !Bool(checkout, "TerminalEvidencePresent") &&
              IsNull(checkout, "TerminatedAt") && IsNull(checkout, "TerminatedByAdministratorUserId") &&
              Time(checkout, "CompletedAt") == Time(payment, "ConfirmedAt"), "CheckoutBinding");
        var receipt = Single(Array(e, "webhook_receipts"), r => Id(r, "CheckoutReferenceId") == Id(checkout, "Id"), "Receipt");
        Check(Id(receipt, "Id") == Id(p, "retainedReceiptId") && Int(receipt, "TenantId") == 1 &&
              Int(receipt, "Programme") == 0 && Time(receipt, "ProcessedAt") == Time(p, "retainedReceiptProcessedAtUtc") &&
              Bool(receipt, "EventIdPresent") && Bool(receipt, "PayloadHashCanonical") &&
              Bool(receipt, "ProviderPaymentIdPresent") && Bool(receipt, "CheckoutAndPaymentBindingMatch"), "ReceiptBinding");
        var checkoutCheck = Single(Array(s, "checkout_checks"), r => Id(r, "ParticipationId") == id, "CheckoutCheck");
        Check(Int(checkoutCheck, "CustomerId") == customer && Int(checkoutCheck, "CheckoutCount") == 1 &&
              Int(checkoutCheck, "ReceiptCount") == 1 && Int(checkoutCheck, "RequiredCompletedCheckoutCount") == 1, "CheckoutCounts");
        var validation = Single(Array(checkoutCheck, "ReceiptValidation"), _ => true, "ReceiptValidationCount");
        Check(Id(validation, "ReceiptId") == Id(receipt, "Id") && Int(validation, "HashLength") == 64 &&
              Time(validation, "ProcessedAt") == Time(receipt, "ProcessedAt"), "ReceiptValidationIdentity");
        foreach (var field in new[] { "HashNonNull", "HashAsciiHex", "EventNonWhitespace", "HashWithoutWhitespace" })
            Check(Bool(validation, field), "ReceiptValidation:" + field);

        var decisions = Array(e, "approval_decisions").Where(r => Id(r, "EntryParticipationId") == id).ToList();
        if (customer == 11)
        {
            Check(decisions.Count == 0 && IsNull(p, "approvalDecisionId") && IsNull(p, "approvalDecidedAtUtc"), "RootApprovalUnknown");
        }
        else
        {
            var decision = Single(decisions, _ => true, "ApprovalCount");
            Check(Id(decision, "Id") == Id(p, "approvalDecisionId") && Bool(decision, "Approved") &&
                  Long(decision, "AdministratorUserId") > 0 && Bool(decision, "ActorExists") && !Bool(decision, "ActorIsDeleted") &&
                  (IsNull(decision, "ActorTenantId") || Int(decision, "ActorTenantId") == 1) &&
                  Time(decision, "DecidedAt") == Time(p, "approvalDecidedAtUtc") &&
                  Time(decision, "DecidedAt") == Time(p, "activatedAtUtc") &&
                  Time(decision, "ActorCreationTime") <= Time(decision, "DecidedAt"), "ApprovalBinding");
            var actor = Single(Array(s, "approval_actor_checks"), r => Id(r, "ParticipationId") == id, "ApprovalActor");
            Check(Id(actor, "DecisionId") == Id(decision, "Id"), "ApprovalActorIdentity");
            foreach (var field in new[] { "Approved", "ActorExists", "ActorNonDeleted", "ActorTenantPermitted",
                         "PositiveActorReference", "DecisionMatchesActivation", "ActorCreatedBeforeDecision" })
                Check(Bool(actor, field), "ApprovalActor:" + field);
        }
    }

    internal static AQGreenStructuralCompletionLevel? StructuralLevel(JsonElement p)
    {
        var value = Get(p, "effectiveV1StructuralLevel");
        if (value.ValueKind == JsonValueKind.String && value.GetString() == "UNKNOWN") return null;
        Check(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n) && n is >= 0 and <= 3, "StructuralEvidence");
        return (AQGreenStructuralCompletionLevel)value.GetInt32();
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static bool IsDigest(string s) => s.Length == 64 && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    internal static void Check(bool condition, string category) { if (!condition) throw new AdapterValidationException(category); }
    internal static JsonElement Get(JsonElement o, string name)
    {
        Check(o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out _), "MissingField:" + name);
        return o.GetProperty(name);
    }
    internal static string Text(JsonElement o, string name)
    {
        var value = Get(o, name);
        Check(value.ValueKind == JsonValueKind.String, "FieldType:" + name);
        return value.GetString()!;
    }
    internal static long Long(JsonElement o, string name)
    {
        var value = Get(o, name);
        Check(value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _), "FieldType:" + name);
        return value.GetInt64();
    }
    internal static int Int(JsonElement o, string name)
    {
        var value = Long(o, name);
        Check(value is >= int.MinValue and <= int.MaxValue, "FieldRange:" + name);
        return (int)value;
    }
    internal static decimal Decimal(JsonElement o, string name)
    {
        var value = Get(o, name);
        Check(value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _), "FieldType:" + name);
        return value.GetDecimal();
    }
    internal static bool Bool(JsonElement o, string name)
    {
        var value = Get(o, name);
        Check(value.ValueKind is JsonValueKind.True or JsonValueKind.False, "FieldType:" + name);
        return value.GetBoolean();
    }
    internal static bool IsNull(JsonElement o, string name) => Get(o, name).ValueKind == JsonValueKind.Null;
    internal static Guid Id(JsonElement o, string name)
    {
        Check(Guid.TryParseExact(Text(o, name), "D", out _), "Identifier:" + name);
        return Guid.ParseExact(Text(o, name), "D");
    }
    internal static DateTime Time(JsonElement o, string name) => ParseTime(Text(o, name));
    private static DateTime ParseTime(string text)
    {
        Check((text.EndsWith('Z') || text.EndsWith("+00:00", StringComparison.Ordinal)) &&
              DateTimeOffset.TryParseExact(text, new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                  "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz" }, CultureInfo.InvariantCulture,
                  DateTimeStyles.None, out _), "Timestamp");
        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture).UtcDateTime;
    }
    internal static List<JsonElement> Array(JsonElement o, string name)
    {
        var value = Get(o, name);
        Check(value.ValueKind == JsonValueKind.Array, "FieldType:" + name);
        return value.EnumerateArray().ToList();
    }
    private static JsonElement Single(IReadOnlyList<JsonElement> items, Func<JsonElement, bool> predicate, string category)
    {
        var matching = items.Where(predicate).ToList();
        Check(matching.Count == 1, category);
        return matching[0];
    }
    private static void Equal(JsonElement o, string name, string value) => Check(Text(o, name) == value, "Value:" + name);
    private static JsonElement Parse(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        NoDuplicateProperties(doc.RootElement);
        return doc.RootElement.Clone();
    }
    private static void NoDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            {
                Check(names.Add(p.Name), "DuplicateJsonProperty");
                NoDuplicateProperties(p.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) NoDuplicateProperties(item);
    }
}
