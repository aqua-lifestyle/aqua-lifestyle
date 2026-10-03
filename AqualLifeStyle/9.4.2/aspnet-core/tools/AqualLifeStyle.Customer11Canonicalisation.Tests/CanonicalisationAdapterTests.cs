using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AqualLifeStyle.Customer11Canonicalisation;
using AqualLifeStyle.Domain.AQGreen;
using Xunit;

namespace AqualLifeStyle.Customer11Canonicalisation.Tests;

public sealed class CanonicalisationAdapterTests
{
    [Fact]
    public void UnknownSurvivesRequestDomainAndOutputWithoutBecomingLevelZero()
    {
        var input = new Fixture().Read();
        Assert.All(input.Request.Candidates, p => Assert.Null(p.EffectiveV1StructuralLevel));
        var result = Customer11CanonicalisationAdapter.Invoke(input);
        Assert.All(result.StructuralImpacts, p =>
        {
            Assert.Null(p.EffectiveV1StructuralLevel);
            Assert.Equal(AQGreenLegacyStructuralImpactClassification.Ambiguous, p.Classification);
        });
        using var output = JsonDocument.Parse(CanonicalOutputWriter.Write(input, result));
        Assert.All(output.RootElement.GetProperty("structuralImpacts").EnumerateArray(), p =>
        {
            Assert.Equal(JsonValueKind.Null, p.GetProperty("effectiveV1StructuralLevel").ValueKind);
            Assert.Equal("UNKNOWN", p.GetProperty("v1EvidenceDisposition").GetString());
        });
        Assert.All(output.RootElement.GetProperty("sourceFacts").EnumerateArray(), p =>
        {
            Assert.Equal("UNKNOWN", p.GetProperty("effectiveV1StructuralLevel").GetString());
            Assert.Equal("UNKNOWN", p.GetProperty("historicalParticipantConfirmation").GetString());
            Assert.Equal("UNKNOWN", p.GetProperty("historicalAcquisitionSource").GetString());
            Assert.Equal("UNKNOWN", p.GetProperty("historicalFinancialQualification").GetString());
        });
        Assert.Equal("UNKNOWN", output.RootElement.GetProperty("sourceFacts")[0].GetProperty("historicalAdministratorApproval").GetString());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void KnownNumericEvidenceIsPreserved(int level)
    {
        var f = new Fixture();
        f.Participants[0]!["effectiveV1StructuralLevel"] = level;
        var input = f.Read();
        Assert.Equal((AQGreenStructuralCompletionLevel)level, input.Request.Candidates.First().EffectiveV1StructuralLevel);
        Assert.Equal((AQGreenStructuralCompletionLevel)level,
            Customer11CanonicalisationAdapter.Invoke(input).StructuralImpacts.Single(p => p.ParticipantSnapshotKey == Customer11ManifestReader.Identities[11]).EffectiveV1StructuralLevel);
    }

    [Theory]
    [InlineData("null")] [InlineData("\"unknown\"")] [InlineData("\"0\"")]
    [InlineData("false")] [InlineData("-1")] [InlineData("4")] [InlineData("1.5")]
    public void MalformedOrOutOfRangeStructuralEvidenceFails(string raw)
    {
        var f = new Fixture();
        f.Participants[0]!["effectiveV1StructuralLevel"] = JsonNode.Parse(raw);
        Reject(f, "StructuralEvidence");
    }

    [Fact]
    public void MissingStructuralEvidenceNeverDefaultsToZero()
    {
        var f = new Fixture();
        f.Participants[0]!.AsObject().Remove("effectiveV1StructuralLevel");
        Reject(f, "MissingField:effectiveV1StructuralLevel");
    }

    [Theory]
    [InlineData("ManifestHash")] [InlineData("CaptureHash")]
    public void WrongByteIdentityIsRejectedBeforeParsing(string category)
    {
        var f = new Fixture();
        var (m, c) = f.Bytes();
        var ex = Assert.Throws<AdapterValidationException>(() => Customer11ManifestReader.ReadBound(m, c,
            category == "ManifestHash" ? new string('0', 64) : Customer11ManifestReader.Hash(m),
            category == "CaptureHash" ? new string('0', 64) : Customer11ManifestReader.Hash(c)));
        Assert.Equal(category, ex.Category);
    }

    [Fact]
    public void OperationalEntryPointHasNoFixtureHashOverride()
    {
        var f = new Fixture();
        var (m, c) = f.Bytes();
        Assert.Equal("ManifestHash", Assert.Throws<AdapterValidationException>(() => Customer11ManifestReader.Read(m, c)).Category);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.Equal(2, Program.Run(["--connection", "must-not-be-used"], stdout, stderr));
        Assert.Equal("", stdout.ToString());
        Assert.Equal("REJECTED: Arguments" + Environment.NewLine, stderr.ToString());
    }

    [Theory]
    [InlineData("tenantId", "2", "Tenant")]
    [InlineData("programme", "\"Onyx\"", "Value:programme")]
    [InlineData("rootCustomerId", "14", "Root")]
    [InlineData("rootParticipationId", "\"00000000-0000-0000-0000-000000000001\"", "Root")]
    [InlineData("candidatePlacementTreeScopeId", "\"00000000-0000-0000-0000-000000000001\"", "Scope")]
    [InlineData("canonicalisationEffectiveAtUtc", "\"2026-10-03T07:17:10Z\"", "EffectiveTimestamp")]
    [InlineData("rulesVersion", "\"other\"", "Value:rulesVersion")]
    [InlineData("comparatorVersion", "\"other\"", "Value:comparatorVersion")]
    public void WrongManifestBoundaryFails(string field, string raw, string category)
    {
        var f = new Fixture(); f.Manifest[field] = JsonNode.Parse(raw); Reject(f, category);
    }

    [Theory]
    [InlineData("participationId", "\"00000000-0000-0000-0000-000000000001\"", "ParticipantIdentity")]
    [InlineData("tenantId", "2", "ParticipantTenant")]
    [InlineData("programme", "\"Onyx\"", "Value:programme")]
    [InlineData("acceptedLegacySponsorParticipationId", "\"00000000-0000-0000-0000-000000000001\"", "Sponsor")]
    [InlineData("legacyRecruiterCustomerId", "14", "Sponsor")]
    [InlineData("qualifiedUnderRecruiterAtUtc", "\"2026-08-01T00:00:00Z\"", "OrderingEvidence")]
    [InlineData("historicalAcquisitionSource", "\"MemberInvitation\"", "Value:historicalAcquisitionSource")]
    [InlineData("historicalParticipantConfirmation", "\"Confirmed\"", "Value:historicalParticipantConfirmation")]
    public void ParticipantSubstitutionAndEvidenceFabricationFail(string field, string raw, string category)
    {
        var f = new Fixture(); f.Participants[1]![field] = JsonNode.Parse(raw); Reject(f, category);
    }

    [Fact]
    public void DuplicateOrMissingParticipantFails()
    {
        var f = new Fixture(); f.Participants[1] = f.Participants[0]!.DeepClone(); Reject(f, "ParticipantIdentity");
        f = new Fixture(); f.Participants.RemoveAt(1); Reject(f, "Population");
    }

    [Fact]
    public void ParticipantDigestIsIndependentlyVerified()
    {
        var f = new Fixture(); var (m, c) = f.Bytes();
        var altered = JsonNode.Parse(m)!.AsObject();
        altered["participants"]![0]!["sourceEvidenceDigest"] = new string('0', 64);
        m = Encoding.UTF8.GetBytes(altered.ToJsonString());
        Assert.Equal("ParticipantDigest", Assert.Throws<AdapterValidationException>(() => Customer11ManifestReader.ReadBound(m, c,
            Customer11ManifestReader.Hash(m), Customer11ManifestReader.Hash(c))).Category);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EachFailedGuardFailsClosed(int index)
    {
        var f = new Fixture(); f.Records[index]["result"] = "FAIL"; Reject(f, "Value:result");
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("misordered")]
    public void CompleteSevenRecordShapeIsRequired(string mutation)
    {
        var f = new Fixture();
        if (mutation == "missing") f.Records.RemoveAt(0);
        else if (mutation == "duplicate") f.Records.Insert(0, f.Records[0].DeepClone().AsObject());
        else f.Records[1] = f.Records[0].DeepClone().AsObject();
        Reject(f, mutation == "misordered" ? "Value:category" : "CaptureRecordCount");
    }

    [Fact]
    public void RecordedPassDoesNotOverrideWrongObservedNumericVersion()
    {
        var f = new Fixture(); f.Records[2]["observed"] = 180004; Reject(f, "ServerVersionNumber");
    }

    [Fact]
    public void VersionPresentationMayIncludeDistributionSuffix()
    {
        var f = new Fixture(); f.Records[2]["server_version"] = "18.6 (synthetic distribution label)";
        Assert.Equal(6, f.Read().Request.Candidates.Count);
    }

    [Theory]
    [InlineData("database", "other")]
    [InlineData("clone_service_id", "dpg-d9f0jne1a83c73d8m780-a")]
    [InlineData("transaction_read_only", "off")]
    [InlineData("isolation", "read committed")]
    public void CaptureIdentityMismatchFails(string field, string value)
    {
        var f = new Fixture(); f.Records[4][field] = value; Reject(f, "Value:" + field);
    }

    [Theory]
    [InlineData("scope_count")] [InlineData("placement_count")]
    [InlineData("attribution_count")] [InlineData("confirmation_count")]
    public void ExistingRowsFailClosed(string field)
    {
        var f = new Fixture(); f.Records[6][field] = 1; Reject(f, "ExistingRows:" + field[..^6]);
    }

    [Fact]
    public void NegativeApprovalIsNotAcceptedBecauseARowExists()
    {
        var f = new Fixture(); f.Records[5]["approval_decisions"]![0]!["Approved"] = false;
        Reject(f, "ApprovalBinding");
    }

    [Fact]
    public void MissingApprovalActorFails()
    {
        var f = new Fixture(); f.Records[5]["approval_decisions"]![0]!["ActorExists"] = false;
        Reject(f, "ApprovalBinding");
    }

    [Theory]
    [InlineData("Status", "0")] [InlineData("Purpose", "1")]
    [InlineData("Amount", "0")] [InlineData("Currency", "\"USD\"")]
    public void PaymentContradictionFails(string field, string raw)
    {
        var f = new Fixture(); f.Records[5]["payments"]![0]![field] = JsonNode.Parse(raw); Reject(f, "PaymentBinding");
    }

    [Fact]
    public void ReceiptBindingContradictionFails()
    {
        var f = new Fixture(); f.Records[5]["webhook_receipts"]![0]!["CheckoutAndPaymentBindingMatch"] = false;
        Reject(f, "ReceiptBinding");
    }

    [Fact]
    public void UnknownSecurityObservationIsNotConvertedToFalse()
    {
        var f = new Fixture(); Assert.Null(f.Records[5]["customer_security"]![0]!["UserCurrentlyLockedOut"]);
        var input = f.Read();
        Assert.Equal("UNKNOWN", input.Manifest.GetProperty("participants")[0].GetProperty("historicalSecurityState").GetString());
        f.Records[5]["customer_security"]![0]!["UserCurrentlyLockedOut"] = true;
        Reject(f, "SecurityLockout");
    }

    [Fact]
    public void AdapterOutputEqualsDirectDomainInvocationIncludingAllProposalFields()
    {
        var input = new Fixture().Read();
        var direct = AQGreenLegacyCanonicalisationSimulator.Simulate(input.Request);
        var adapted = Customer11CanonicalisationAdapter.Invoke(input);
        Assert.Equal(direct.ManifestDigest, adapted.ManifestDigest);
        Assert.Equal(CanonicalOutputWriter.Write(input, direct), CanonicalOutputWriter.Write(input, adapted));
    }

    [Fact]
    public void DuplicateInvocationsAreByteIdenticalAndPreserveDomainOrder()
    {
        var input = new Fixture().Read();
        var first = CanonicalOutputWriter.Write(input, Customer11CanonicalisationAdapter.Invoke(input));
        var second = CanonicalOutputWriter.Write(input, Customer11CanonicalisationAdapter.Invoke(input));
        Assert.Equal(first, second);
        Assert.Equal((byte)10, first[^1]); Assert.DoesNotContain((byte)13, first);
        Assert.False(first.AsSpan().StartsWith(new byte[] { 239, 187, 191 }));
        using var parsed = JsonDocument.Parse(first);
        Assert.Equal(new[] { 1L, 2L, 3L, 4L, 5L, 6L }, parsed.RootElement.GetProperty("placements").EnumerateArray().Select(p => p.GetProperty("placementSequence").GetInt64()));
        Assert.False(parsed.RootElement.TryGetProperty("outputSha256", out _));
    }

    [Fact]
    public void DomainRemainsResponsibleForTieBreakAndTraversal()
    {
        var input = new Fixture().Read();
        var candidates = input.Request.Candidates.Select(p => new AQGreenLegacyCanonicalisationCandidate
        {
            TenantSnapshotKey = p.TenantSnapshotKey, ParticipantSnapshotKey = p.ParticipantSnapshotKey,
            AcceptedLegacySponsorSnapshotKey = p.AcceptedLegacySponsorSnapshotKey,
            QualifiedUnderRecruiterAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            LegacyOrderingTieBreakOrdinal = p.LegacyOrderingTieBreakOrdinal, RootEvidenceStatus = p.RootEvidenceStatus,
            SponsorEvidenceStatus = p.SponsorEvidenceStatus, AcquisitionProvenance = p.AcquisitionProvenance,
            EffectiveV1StructuralLevel = p.EffectiveV1StructuralLevel, SourceEvidenceDigest = p.SourceEvidenceDigest
        }).Reverse().ToArray();
        var changed = input with { Request = new AQGreenLegacyCanonicalisationRequest
        {
            TenantSnapshotKey = 1, PlacementTreeScopeSnapshotKey = Customer11ManifestReader.Scope,
            AnalysisSnapshotCutoffUtc = input.Request.AnalysisSnapshotCutoffUtc, Candidates = candidates
        } };
        var direct = AQGreenLegacyCanonicalisationSimulator.Simulate(changed.Request);
        var adapted = Customer11CanonicalisationAdapter.Invoke(changed);
        Assert.Equal(direct.Placements.Select(p => p.ParticipantSnapshotKey), adapted.Placements.Select(p => p.ParticipantSnapshotKey));
        Assert.Equal(CanonicalOutputWriter.Write(changed, direct), CanonicalOutputWriter.Write(changed, adapted));
    }

    [Fact]
    public void AdapterHasNoDatabaseNetworkEnvironmentOrWriterCalls()
    {
        var assembly = typeof(Customer11ManifestReader).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFrameworkCore") || a.Name.Contains("Npgsql") || a.Name.Contains("SnapshotRunner") || a.Name.Contains("Backfill"));
        var methods = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)).Where(m => m.DeclaringType!.Assembly == assembly);
        var calls = methods.SelectMany(Calls).ToList();
        Assert.Contains(calls, m => m.DeclaringType == typeof(AQGreenLegacyCanonicalisationSimulator) && m.Name == "Simulate");
        Assert.DoesNotContain(calls, m =>
            (m.DeclaringType?.FullName ?? "").StartsWith("System.Net", StringComparison.Ordinal) ||
            (m.DeclaringType?.FullName ?? "").Contains("EntityFramework") ||
            (m.DeclaringType?.FullName ?? "").Contains("Backfill") ||
            (m.DeclaringType?.FullName ?? "").Contains("Npgsql") ||
            m.DeclaringType == typeof(Environment) && m.Name.Contains("EnvironmentVariable"));
        Assert.Equal(1, calls.Count(m => m.DeclaringType == typeof(AQGreenLegacyCanonicalisationSimulator) && m.Name == "Simulate"));
    }

    private static IEnumerable<MethodBase> Calls(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null) yield break;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
        int i = 0;
        while (i < il.Length)
        {
            ushort code = il[i++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]);
            var op = opcodes[code];
            if (op.OperandType == OperandType.InlineMethod)
                yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, i), method.DeclaringType!.GetGenericArguments(), method.GetGenericArguments())!;
            i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4
            };
        }
    }

    private static void Reject(Fixture f, string category) => Assert.Equal(category, Assert.Throws<AdapterValidationException>(() => f.Read()).Category);

    // Exact component identifiers are contract keys. All payment/actor/security data and chronology
    // below are synthetic fixture values, not copied production records or historical evidence.
    private sealed class Fixture
    {
        internal JsonObject Manifest { get; }
        internal JsonArray Participants => Manifest["participants"]!.AsArray();
        internal List<JsonObject> Records { get; } = [];
        internal Fixture()
        {
            var source = Object(new
            {
                boundedCaptureSha256 = "pending", captureLabel = "NEW RECOVERY-CLONE EVIDENCE CAPTURE",
                serviceId = Customer11ManifestReader.Clone, database = "aqualifestyle_1t0a", databaseUser = "aqualifestyle",
                capturedAtUtc = Customer11ManifestReader.ObservationAt, historicalPitrCutoffUtc = Customer11ManifestReader.EffectiveAt,
                historicalOriginProductionServiceId = "dpg-d9f0jne1a83c73d8m780-a"
            });
            Manifest = Object(new
            {
                label = "NEW-LINEAGE CUSTOMER-11 CANDIDATE INPUT", manifestVersion = "Customer11NewLineageCanonicalisationCandidateV1",
                status = "CANDIDATE_CANONICALISATION_NOT_EXECUTED", tenantId = 1, programme = "AQGreen", rootCustomerId = 11,
                rootParticipationId = Customer11ManifestReader.Identities[11], candidatePlacementTreeScopeId = Customer11ManifestReader.Scope,
                canonicalisationEffectiveAtUtc = Customer11ManifestReader.EffectiveAt, analysisSnapshotCutoffUtc = Customer11ManifestReader.EffectiveAt,
                capturedObservationAtUtc = Customer11ManifestReader.ObservationAt, placementSource = "LegacyCanonicalisation",
                rulesVersion = AQGreenPlacementRules.CurrentVersion, simulationVersion = AQGreenLegacyCanonicalisationRules.SimulationVersion,
                comparatorVersion = AQGreenLegacyCanonicalisationRules.ComparatorVersion, uuidOrderingAdapterVersion = "DotNetGuidCompareToOrdinalV2",
                effectiveTimeMeaning = "Synthetic fixture; not historical evidence",
                custody = new { classification = "NEW ARTIFACT — FRESH ACCEPTANCE REQUIRED", uninterruptedCustody = "NOT CLAIMED", originalAcceptedBytes = "UNAVAILABLE" }
            });
            Manifest["sourceSnapshot"] = source;
            Manifest["participants"] = new JsonArray();
            string[] categories = ["DatabaseIdentity", "DatabaseUser", "ServerVersionNumber", "ReadOnlyMode"];
            for (int i = 0; i < 4; i++) Records.Add(Object(new { kind = "identity_guard", result = "PASS", category = categories[i], expected = "pending", observed = "pending" }));
            string[] values = ["aqualifestyle_1t0a", "aqualifestyle", "", "on"];
            for (int i = 0; i < 4; i++)
            {
                Records[i]["expected"] = i == 2 ? JsonValue.Create(180006) : JsonValue.Create(values[i]);
                Records[i]["observed"] = Records[i]["expected"]!.DeepClone();
            }
            Records[2]["server_version"] = "18.6 (synthetic packaging)";
            Records.Add(Object(new { kind = "identity", database = "aqualifestyle_1t0a", user = "aqualifestyle", clone_service_id = Customer11ManifestReader.Clone,
                capture_label = "NEW RECOVERY-CLONE EVIDENCE CAPTURE", transaction_read_only = "on", isolation = "repeatable read",
                version = "PostgreSQL 18.6 (synthetic fixture)", snapshot_utc = Customer11ManifestReader.ObservationAt,
                historical_pitr_cutoff = Customer11ManifestReader.EffectiveAt, historical_canonicalisation_effective_at = Customer11ManifestReader.EffectiveAt }));
            var e = new JsonObject { ["kind"] = "bounded_evidence", ["recruiter_corrections"] = null };
            foreach (var name in new[] { "participants", "expected", "checks", "customer_security", "joining_checkouts", "payments", "webhook_receipts", "approval_decisions" }) e[name] = new JsonArray();
            var s = Object(new { kind = "bounded_supplement", scope_count = 0, placement_count = 0, attribution_count = 0, confirmation_count = 0, additional_active_descendant_count = 0,
                tenant = new { Id = 1, IsActive = true, IsDeleted = false } });
            foreach (var name in new[] { "scopes", "placements", "attributions" }) s[name] = null;
            s["approval_actor_checks"] = new JsonArray(); s["checkout_checks"] = new JsonArray();
            Records.Add(e); Records.Add(s);
            int order = 0;
            foreach (var pair in Customer11ManifestReader.Identities)
            {
                int customer = pair.Key; Guid id = pair.Value; bool root = customer == 11;
                var payment = SyntheticGuid(100 + customer); var checkout = SyntheticGuid(200 + customer);
                var receipt = SyntheticGuid(300 + customer); var decision = SyntheticGuid(400 + customer);
                string time = new DateTime(2026, 8, 1, 0, ++order, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                var p = Object(new
                {
                    customerId = customer, participationId = id, tenantId = 1, programme = "AQGreen", currentStatus = "Active", rawStatus = 2, isDeleted = false,
                    startedAtUtc = time, activatedAtUtc = time, qualifiedUnderRecruiterAtUtc = time, legacyOrderingTieBreakOrdinal = order,
                    sourceEvidenceDigest = "pending", sourceEvidenceCaptureSha256 = "pending", effectiveV1StructuralLevel = "UNKNOWN",
                    acquisitionProvenance = "LegacyUnknownAcquisition", historicalAcquisitionSource = "UNKNOWN", historicalInvitation = "UNKNOWN",
                    historicalParticipantConfirmation = "UNKNOWN", historicalFinancialQualification = "UNKNOWN", historicalSecurityState = "UNKNOWN",
                    historicalActorPermissionSnapshot = "UNKNOWN", independentBankSettlement = "UNKNOWN", correctionCount = 0,
                    correctionDisposition = "NO_RETAINED_CORRECTIONS", contradictionDisposition = "NONE_IN_BOUNDED_INSPECTED_EVIDENCE",
                    canonicalisationEligibility = "OWNER_ACCEPTED_BOUNDED_D09_SATISFIED", rootEvidenceStatus = root ? "AcceptedRoot" : "NotRoot",
                    sponsorEvidenceStatus = root ? "NotApplicableToRoot" : "AcceptedSponsor", historicalAdministratorApproval = root ? "UNKNOWN" : "RETAINED_POSITIVE_DECISION",
                    approvalEvidenceDisposition = root ? "NO_RETAINED_APPROVAL_ROW" : "RETAINED_APPROVED_TRUE_ACTOR_REFERENCE_PRESENT",
                    joiningPaymentId = payment, joiningCheckoutId = checkout, retainedReceiptId = receipt, retainedReceiptProcessedAtUtc = time,
                    paymentConfirmedAtUtc = time, paymentDisposition = "RETAINED_AQUA_CONFIRMED_COMPLETED_JOINING_WITH_MATCHING_YOCO_RECEIPT",
                    paymentOwnershipConsistency = "PASS", paymentPurpose = "AQGreenJoining", currency = "ZAR", joiningAmount = 1200.00m,
                    receiptBindingDisposition = "INTERNAL_TENANT_PROGRAMME_CHECKOUT_PAYMENT_BINDINGS_VERIFIED"
                });
                p["acceptedLegacySponsorParticipationId"] = root ? null : JsonValue.Create(Customer11ManifestReader.Identities[11]);
                p["legacyRecruiterCustomerId"] = root ? null : JsonValue.Create(11);
                p["approvalDecisionId"] = root ? null : JsonValue.Create(decision);
                p["approvalDecidedAtUtc"] = root ? null : JsonValue.Create(time);
                Participants.Add(p);
                Add(e, "participants", new { CustomerId = customer, Id = id, TenantId = 1, Status = 2, IsDeleted = false, StartedAt = time, ActivatedAt = time, JoiningPaymentId = payment });
                e["participants"]!.AsArray()[^1]!["RecruiterCustomerId"] = root ? null : JsonValue.Create(11);
                Add(e, "expected", new { CustomerId = customer, ParticipationId = id });
                Add(e, "checks", new { CustomerId = customer, ParticipationId = id, CorrectionCount = 0, DecisionCount = root ? 0 : 1,
                    ExpectedRecruiterEdge = true, RetainedActiveChronology = true, ExpectedIdentityAndTenant = true, ConfirmedSingleJoiningPaymentBound = true,
                    RetainedRejectionExists = false, PositiveDecisionActorAndActivationMatch = !root });
                Add(e, "customer_security", new { CustomerId = customer, ParticipationId = id, CustomerTenantId = 1, UserTenantId = 1, CustomerExists = true,
                    UserExists = true, CustomerIsActive = true, UserIsActive = true, CustomerIsDeleted = false, UserIsDeleted = false });
                e["customer_security"]!.AsArray()[^1]!["UserCurrentlyLockedOut"] = null;
                Add(e, "payments", new { Id = payment, CustomerId = customer, TenantId = 1, IsDeleted = false, Status = 1, Purpose = 7, Amount = 1200m,
                    Currency = "ZAR", Provider = "YOCO", MatchingWebhookReceiptCount = 1, ExternalReferencePresent = true, ConfirmedAt = time });
                Add(e, "joining_checkouts", new { Id = checkout, ParticipationId = id, PaymentId = payment, CustomerId = customer, TenantId = 1, IsDeleted = false,
                    Status = 2, Stage = 0, Amount = 1200m, Currency = "ZAR", PaymentOwnershipAmountCurrencyMatch = true, ProviderCheckoutIdPresent = true,
                    TerminalEvidencePresent = false, CompletedAt = time });
                e["joining_checkouts"]!.AsArray()[^1]!["TerminatedAt"] = null;
                e["joining_checkouts"]!.AsArray()[^1]!["TerminatedByAdministratorUserId"] = null;
                Add(e, "webhook_receipts", new { Id = receipt, CheckoutReferenceId = checkout, TenantId = 1, Programme = 0, ProcessedAt = time,
                    EventIdPresent = true, PayloadHashCanonical = true, ProviderPaymentIdPresent = true, CheckoutAndPaymentBindingMatch = true });
                Add(s, "checkout_checks", new { CustomerId = customer, ParticipationId = id, CheckoutCount = 1, ReceiptCount = 1, RequiredCompletedCheckoutCount = 1,
                    ReceiptValidation = new[] { new { ReceiptId = receipt, ProcessedAt = time, HashLength = 64, HashNonNull = true, HashAsciiHex = true,
                        EventNonWhitespace = true, HashWithoutWhitespace = true } } });
                if (!root)
                {
                    Add(e, "approval_decisions", new { Id = decision, EntryParticipationId = id, Approved = true, AdministratorUserId = 999L,
                        ActorExists = true, ActorIsDeleted = false, ActorTenantId = 1, DecidedAt = time, ActorCreationTime = "2026-07-01T00:00:00Z" });
                    Add(s, "approval_actor_checks", new { ParticipationId = id, DecisionId = decision, Approved = true, ActorExists = true, ActorNonDeleted = true,
                        ActorTenantPermitted = true, PositiveActorReference = true, DecisionMatchesActivation = true, ActorCreatedBeforeDecision = true });
                }
            }
        }

        internal (byte[] Manifest, byte[] Capture) Bytes()
        {
            byte[] capture = Encoding.UTF8.GetBytes(string.Join('\n', Records.Select(r => r.ToJsonString())) + "\n");
            string digest = Customer11ManifestReader.Hash(capture);
            Manifest["sourceSnapshot"]!["boundedCaptureSha256"] = digest;
            foreach (var p in Participants)
            {
                p!["sourceEvidenceCaptureSha256"] = digest;
                p["sourceEvidenceDigest"] = Customer11ManifestReader.Hash(CanonicalOutputWriter.CanonicalBytes(JsonSerializer.SerializeToElement(p), "sourceEvidenceDigest", false));
            }
            return (Encoding.UTF8.GetBytes(Manifest.ToJsonString()), capture);
        }
        internal VerifiedInput Read()
        {
            var (m, c) = Bytes();
            return Customer11ManifestReader.ReadBound(m, c, Customer11ManifestReader.Hash(m), Customer11ManifestReader.Hash(c));
        }
        private static JsonObject Object(object value) => JsonSerializer.SerializeToNode(value)!.AsObject();
        private static void Add(JsonObject target, string name, object row) => target[name]!.AsArray().Add(Object(row));
        private static Guid SyntheticGuid(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:x12}");
    }
}
