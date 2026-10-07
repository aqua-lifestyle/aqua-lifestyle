using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace AqualLifeStyle.B6SnapshotRunner.Tests;

/// <summary>
/// Test-only fixture authoring step. This deliberately lives outside the
/// operational runner so the authoritative tool cannot manufacture reviewed
/// root evidence.
/// </summary>
public sealed class B6SyntheticRootDispositionWriterTest
{
    [Fact]
    public void WriteReviewedSyntheticRootDisposition()
    {
        var rawDirectory = Required("B6_FIXTURE_RAW");
        var snapshotDirectory = Required("B6_FIXTURE_SNAPSHOT");
        var baseManifestPath = Required("B6_FIXTURE_BASE_MANIFEST");
        var output = Required("B6_FIXTURE_ROOT_DISPOSITIONS");
        var allowlist = new B6Allowlist(null);
        var table = allowlist.ByName["EntryParticipations"];
        var raw = CsvContract.Read(Path.Combine(rawDirectory, "EntryParticipations.csv"),
            table.SourceFields.Select(field => field.Output).ToList());
        var safe = CsvContract.Read(Path.Combine(snapshotDirectory, "EntryParticipations.csv"),
            table.Fields.Select(field => field.Output).ToList());
        var index = raw.FindIndex(row => row["Id"] == "10000000-0000-0000-8000-000000000010");
        Assert.True(index >= 0);
        var baseManifest = CanonicalJson.Read(baseManifestPath).AsObject();
        var disposition = new JsonObject
        {
            ["schemaVersion"] = B6Contract.RootReviewVersion,
            ["snapshotId"] = baseManifest["snapshotId"]!.DeepClone(),
            ["sourceSnapshotDigest"] = baseManifest["overallSnapshotDigest"]!.DeepClone(),
            ["baseManifestDigest"] = baseManifest["manifestHash"]!.DeepClone(),
            ["dispositions"] = new JsonArray(new JsonObject
            {
                ["participantSnapshotKey"] = safe[index]["Id"],
                ["disposition"] = "AcceptedRoot",
                ["evidenceType"] = "ReviewedLegacyRootEvidenceV1",
                ["evidenceDigest"] = B6Values.Sha256Hex(Encoding.UTF8.GetBytes("TEST ONLY reviewed synthetic root evidence")),
                ["decisionReference"] = "B6-TEST-ROOT-REVIEW-001",
                ["reviewedAt"] = "2026-09-03T01:00:00.000000Z",
                ["reviewedByRole"] = "AuthorisedB6RootReviewer"
            })
        };
        CanonicalJson.WriteNew(output, disposition);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required for the synthetic fixture-authoring test.");
}
