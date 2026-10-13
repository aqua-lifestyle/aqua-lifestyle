using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public class AQGreenD10CommissioningTests
    {
        [Fact]
        public void ExactDecisionAndIndependentAuthorityBindWithoutMutatingCallerEvidence()
        {
            var evidence = AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration());
            var bytes = AQGreenB6CanonicalEvidence.Bytes(evidence.Decision); var bound = evidence.Bind();
            bound.EnsureCurrent(DateTime.UtcNow); bound.Decision.TenantId.ShouldBe(1);
            AQGreenB6CanonicalEvidence.Bytes(evidence.Decision).ShouldBe(bytes);
            evidence.Decision.TenantId = 2; bound.Decision.TenantId.ShouldBe(1); // Frozen binding is detached.
        }

        [Theory]
        [InlineData("owner")][InlineData("B6-authority")][InlineData("execution")][InlineData("backup")][InlineData("release")]
        [InlineData("financial")][InlineData("fence")][InlineData("digest")][InlineData("tenant")][InlineData("effective")]
        [InlineData("population")][InlineData("duplicate")][InlineData("missing-capability")][InlineData("partial-B6")]
        [InlineData("unreviewed-B6")][InlineData("stale-source")]
        [InlineData("native-key")][InlineData("omitted-canonical-placement")]
        public void MissingOrMismatchedAuthorityCannotBind(string failure)
        {
            var e = AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration());
            switch (failure)
            {
                case "owner": e.Authority.OwnerAuthorizationEvidenceDigest = null; break;
                case "B6-authority": e.Authority.B6ReconciliationAuthorityEvidenceDigest = null; break;
                case "execution": e.Authority.B6AppliedReconciliationEvidenceDigest = null; break;
                case "backup": e.Authority.BackupRestoreEvidenceDigest = null; break;
                case "release": e.Authority.ReleasePrerequisiteEvidenceDigest = null; break;
                case "financial": e.Authority.FinancialBoundaryEvidenceDigest = null; break;
                case "fence": e.Authority.WriteFenceEvidenceDigest = null; break;
                case "digest": e.Authority.DecisionDigest = AQGreenD10SyntheticEvidence.Hash("different"); break;
                case "tenant": e.Authority.TenantId = 2; break;
                case "effective": e.Authority.V2EffectiveAtUtc = e.Decision.V2EffectiveAtUtc.AddDays(7); break;
                case "population": e.Decision.PopulationScope = "PARTIAL_TENANT"; break;
                case "duplicate": e.Decision.HistoricalMembers.Add(e.Decision.HistoricalMembers[0]); break;
                case "missing-capability": e.Decision.EnableGraduation = null; break;
                case "partial-B6": e.Final["analysisDigests"]["quarantinedComponentDigest"] = AQGreenD10SyntheticEvidence.Hash("quarantine"); break;
                case "unreviewed-B6": e.Final["manifestStage"] = "OFFLINE_REVIEW_BINDING_ONLY"; break;
                case "stale-source": e.Decision.SourceSnapshotDigest = AQGreenD10SyntheticEvidence.Hash("changed-source"); break;
                case "native-key": e.Decision.PopulationParticipantKeys["canonicalisation"][0] = Guid.NewGuid().ToString("D"); break;
                case "omitted-canonical-placement": e.Decision.ReconciledPlacements.Clear(); break;
            }
            Should.Throw<InvalidOperationException>(() => e.Bind());
        }

        [Fact]
        public void ExpiredAuthorityAndUncommissionedFinancialSwitchesFailClosed()
        {
            var config = AQGreenD10SyntheticEvidence.Configuration(); var e = AQGreenD10SyntheticEvidence.Create(config);
            var bound = e.Bind(); Should.Throw<InvalidOperationException>(() => bound.EnsureCurrent(e.Authority.ExpiresAtUtc.AddTicks(1)));
            config["App:WeeklyCommissions:Enabled"] = "true";
            Should.Throw<InvalidOperationException>(() => bound.EnsureConfiguration(config)).Message.ShouldBe("D10_CONFIGURATION_MISMATCH");
            e = AQGreenD10SyntheticEvidence.Create(config); bound = e.Bind();
            Should.Throw<InvalidOperationException>(() => bound.EnsureConfiguration(config)).Message.ShouldBe("D10_FINANCIAL_WORKER_AUTHORITY_MISSING");
            e.Authority.FinancialWorkersCommissioningEvidenceDigest = AQGreenD10SyntheticEvidence.Hash("synthetic-weekly-commissioning");
            Should.NotThrow(() => e.Bind().EnsureConfiguration(config));
        }

        [Fact]
        public async Task RecoveryCannotBeClearedByReloadForHttpOrProviders()
        {
            var config = AQGreenD10SyntheticEvidence.Configuration(); config[ApplicationRecoveryMode.EnabledSetting] = "true";
            var reached = false; var middleware = new ApplicationRecoveryMiddleware(_ => { reached = true; return Task.CompletedTask; }, config);
            config[ApplicationRecoveryMode.EnabledSetting] = "false";
            var context = new DefaultHttpContext(); context.Request.Method = "POST"; context.Request.Path = "/api/services/app/AdminCommission/CalculateLatestClosedWeek";
            context.Response.Body = new MemoryStream(); await middleware.InvokeAsync(context);
            context.Response.StatusCode.ShouldBe(503); reached.ShouldBeFalse(); ApplicationRecoveryMode.IsEnabled(config).ShouldBeTrue();
            var handler = new NeverNetwork(); var gateway = new YocoCheckoutGateway(new HttpClient(handler), config);
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.CreateAsync(null)); handler.Calls.ShouldBe(0);
        }

        [Fact]
        public void OperatorSourceRequiresIndependentPinsAndDetectsChangedBytesAndCustody()
        {
            var directory = Path.Combine(Path.GetTempPath(), "aqgreen-d10-synthetic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            try
            {
                var config = AQGreenD10SyntheticEvidence.Configuration(); var e = AQGreenD10SyntheticEvidence.Create(config);
                config[AQGreenD10OperatorEvidenceSource.Prefix + "Enabled"] = "true";
                var objects = new object[] { e.Decision, e.Authority, e.Final }; var keys = new[] { "Decision", "VerifiedAuthority", "B6FinalManifest" };
                for (var i = 0; i < keys.Length; i++)
                {
                    var path = Path.Combine(directory, keys[i] + ".json"); var bytes = AQGreenB6CanonicalEvidence.Bytes(objects[i]); File.WriteAllBytes(path, bytes);
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    config[AQGreenD10OperatorEvidenceSource.Prefix + keys[i] + "Path"] = path;
                    config[AQGreenD10OperatorEvidenceSource.Prefix + keys[i] + "Sha256"] = AQGreenB6CanonicalEvidence.Hash(bytes);
                }
                var source = new AQGreenD10OperatorEvidenceSource(config); source.Read().Decision.TenantId.ShouldBe(1);
                config[AQGreenD10OperatorEvidenceSource.Prefix + "VerifiedAuthoritySha256"] = "";
                Should.Throw<InvalidOperationException>(() => new AQGreenD10OperatorEvidenceSource(config).Read()).Message.ShouldBe("D10_PINNED_INPUT_MISSING");
                File.AppendAllText(config[AQGreenD10OperatorEvidenceSource.Prefix + "DecisionPath"], " ");
                Should.Throw<InvalidOperationException>(() => source.Read()).Message.ShouldBe("D10_PINNED_INPUT_CHANGED");
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead);
                Should.Throw<InvalidOperationException>(() => source.Read()).Message.ShouldBe("D10_INPUT_CUSTODY_INVALID");
            }
            finally { Directory.Delete(directory, true); }
        }

        private sealed class NeverNetwork : HttpMessageHandler
        {
            public int Calls;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            { Calls++; throw new InvalidOperationException("Synthetic test forbids provider requests."); }
        }
    }
}
