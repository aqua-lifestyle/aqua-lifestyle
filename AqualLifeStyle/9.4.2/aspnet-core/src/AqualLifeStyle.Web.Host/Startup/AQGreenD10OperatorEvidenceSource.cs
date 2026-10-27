using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Startup
{
    /// <summary>
    /// Reads separately pinned, independently verified operator inputs. No HTTP upload,
    /// role label, self-hash or database value authenticates D10/B6 authority.
    /// The operator must retain actual B6 VerifyFinal and reconciliation/owner evidence.
    /// </summary>
    public sealed class AQGreenD10OperatorEvidenceSource : IAQGreenD10CommissioningSource
    {
        public const string Prefix = "App:AQGreenV2:Commissioning:";
        private readonly bool _requested;
        private readonly string[] _paths;
        private readonly string[] _digests;
        private readonly string _mode;
        public AQGreenD10OperatorEvidenceSource(IConfiguration configuration)
        {
            _requested = configuration.GetValue<bool>(Prefix + "Enabled");
            _mode = configuration[Prefix + "AdmissionMode"] ?? "HISTORICAL_B6";
            AQGreenD10Commissioning.Require(_mode == "HISTORICAL_B6" || _mode == "CLEAN_START", "D10_ADMISSION_MODE_MISMATCH");
            var evidenceKey = _mode == "CLEAN_START" ? "CleanStartEvidence" : "B6FinalManifest";
            var excludedKey = _mode == "CLEAN_START" ? "B6FinalManifest" : "CleanStartEvidence";
            AQGreenD10Commissioning.Require(string.IsNullOrWhiteSpace(configuration[Prefix + excludedKey + "Path"]) &&
                string.IsNullOrWhiteSpace(configuration[Prefix + excludedKey + "Sha256"]), "D10_MIXED_ADMISSION_EVIDENCE");
            _paths = new[] { configuration[Prefix + "DecisionPath"], configuration[Prefix + "VerifiedAuthorityPath"],
                configuration[Prefix + evidenceKey + "Path"] };
            _digests = new[] { configuration[Prefix + "DecisionSha256"], configuration[Prefix + "VerifiedAuthoritySha256"],
                configuration[Prefix + evidenceKey + "Sha256"] };
            AQGreenD10Commissioning.Require(_requested || Array.TrueForAll(_paths, string.IsNullOrWhiteSpace) &&
                Array.TrueForAll(_digests, string.IsNullOrWhiteSpace), "D10_EVIDENCE_CONFIGURED_WITHOUT_EXPLICIT_SELECTION");
        }

        public AQGreenD10Commissioning Read()
        {
            if (!_requested) return null;
            try
            {
                var decision = ReadPinned(0); var authority = ReadPinned(1); var final = ReadPinned(2);
                if (_mode == "CLEAN_START") return AQGreenD10Commissioning.BindCleanStartVerified(
                    JsonSerializer.Deserialize<AQGreenD10Decision>(decision, AQGreenB6CanonicalEvidence.Options),
                    JsonSerializer.Deserialize<AQGreenD10VerifiedAuthority>(authority, AQGreenB6CanonicalEvidence.Options),
                    JsonSerializer.Deserialize<AQGreenCleanStartEvidence>(final, AQGreenB6CanonicalEvidence.Options));
                return AQGreenD10Commissioning.BindVerified(
                    JsonSerializer.Deserialize<AQGreenD10Decision>(decision, AQGreenB6CanonicalEvidence.Options),
                    JsonSerializer.Deserialize<AQGreenD10VerifiedAuthority>(authority, AQGreenB6CanonicalEvidence.Options),
                    JsonNode.Parse(final).AsObject());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException ||
                e is ArgumentException || e is NotSupportedException || e is InvalidOperationException && !e.Message.StartsWith("D10_", StringComparison.Ordinal))
            { throw new InvalidOperationException("D10_EVIDENCE_INPUT_UNAVAILABLE_OR_INVALID"); }
        }

        private byte[] ReadPinned(int index)
        {
            AQGreenD10Commissioning.Require(!string.IsNullOrWhiteSpace(_paths[index]) && !string.IsNullOrWhiteSpace(_digests[index]), "D10_PINNED_INPUT_MISSING");
            var path = Path.GetFullPath(_paths[index]); var info = new FileInfo(path);
            AQGreenD10Commissioning.Require(info.Exists && info.Length <= 32 * 1024 * 1024 &&
                (info.Attributes & FileAttributes.ReparsePoint) == 0, "D10_INPUT_CUSTODY_INVALID");
            for (var directory = info.Directory; directory != null; directory = directory.Parent)
                AQGreenD10Commissioning.Require(!Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                    !File.Exists(Path.Combine(directory.FullName, ".git")) &&
                    (directory.Attributes & FileAttributes.ReparsePoint) == 0, "D10_INPUT_MUST_BE_OUTSIDE_GIT_AND_SYMLINKS");
            if (!OperatingSystem.IsWindows())
            {
                var forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                AQGreenD10Commissioning.Require((File.GetUnixFileMode(path) & forbidden) == 0 &&
                    (File.GetUnixFileMode(info.Directory.FullName) & forbidden) == 0, "D10_INPUT_CUSTODY_INVALID");
            }
            var bytes = File.ReadAllBytes(path);
            AQGreenD10Commissioning.Require(AQGreenB6CanonicalEvidence.Hash(bytes) == _digests[index], "D10_PINNED_INPUT_CHANGED");
            using (var document = JsonDocument.Parse(bytes)) RejectDuplicates(document.RootElement);
            return bytes;
        }

        private static void RejectDuplicates(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                { AQGreenD10Commissioning.Require(keys.Add(property.Name), "D10_DUPLICATED_EVIDENCE_FIELD"); RejectDuplicates(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
        }
    }
}
