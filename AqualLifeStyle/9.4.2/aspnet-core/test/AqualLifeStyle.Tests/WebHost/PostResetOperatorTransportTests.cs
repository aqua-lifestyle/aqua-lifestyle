using System;
using System.Text;
using System.Text.Json;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public class PostResetOperatorTransportTests
    {
        [Fact]
        public void CompletePinnedPrivateProcessInputsBindTheNewContractWithoutFilesOrSelectingDisabledAuthority()
        {
            var (x,c)=AQGreenCleanStartCommissioningTests.Create();AQGreenCleanStartCommissioningTests.PostReset(x,c);
            AQGreenCleanStartCommissioningTests.Sign(x,c);var config=AQGreenD10SyntheticEvidence.Configuration();
            Install(config,x.Decision,x.Authority,c);
            var bound=new AQGreenD10OperatorEvidenceSource(config).Read();
            bound.Decision.ContractVersion.ShouldBe(AQGreenD10Decision.PostResetContract);
            bound.CleanStartEvidence.State.BoundaryUtc.ShouldBe(c.State.BoundaryUtc);
            config[AQGreenD10OperatorEvidenceSource.Prefix+"Enabled"]="false";
            Should.Throw<InvalidOperationException>(()=>new AQGreenD10OperatorEvidenceSource(config))
                .Message.ShouldBe("D10_EVIDENCE_CONFIGURED_WITHOUT_EXPLICIT_SELECTION");
        }

        [Theory]
        [InlineData("missing")][InlineData("digest")][InlineData("selector")][InlineData("mixed")][InlineData("legacy")][InlineData("duplicate")]
        public void PrivateProcessInputsCannotBypassPinsSelectionVersionOrDuplicateValidation(string failure)
        {
            var (x,c)=AQGreenCleanStartCommissioningTests.Create();AQGreenCleanStartCommissioningTests.PostReset(x,c);
            AQGreenCleanStartCommissioningTests.Sign(x,c);var config=AQGreenD10SyntheticEvidence.Configuration();Install(config,x.Decision,x.Authority,c);
            var p=AQGreenD10OperatorEvidenceSource.Prefix;
            switch(failure)
            {
                case "missing":config[p+"DecisionJsonBase64"]=null;break;
                case "digest":config[p+"DecisionSha256"]=new string('a',64);break;
                case "selector":config[p+"DecisionPath"]="environment:ConnectionStrings:Default";break;
                case "mixed":config[p+"DecisionPath"]="/unavailable/operator/decision.json";break;
                case "legacy":x.Decision.ContractVersion="AQGreenD10CutoverV1";Install(config,x.Decision,x.Authority,c);break;
                case "duplicate":
                    var bytes=Encoding.UTF8.GetBytes("{\"contractVersion\":\"a\",\"contractVersion\":\"b\"}");
                    config[p+"DecisionJsonBase64"]=Convert.ToBase64String(bytes);config[p+"DecisionSha256"]=AQGreenB6CanonicalEvidence.Hash(bytes);break;
            }
            Should.Throw<InvalidOperationException>(()=>new AQGreenD10OperatorEvidenceSource(config).Read());
        }

        [Fact]
        public void YocoPrivateManifestTransportRetainsExactHashAndDuplicateChecksAndRejectsOversizedInput()
        {
            var config=AQGreenD10SyntheticEvidence.Configuration();var p=YocoCutoverOptions.Prefix;
            var bytes=Encoding.UTF8.GetBytes("{\"Contract\":\"Aqua.YocoCutover.ReconciliationV1\",\"Entries\":[]}");
            config[p+"ReconciliationPath"]="environment:"+p+"ReconciliationJsonBase64";
            config[p+"ReconciliationJsonBase64"]=Convert.ToBase64String(bytes);config[p+"ReconciliationSha256"]=AQGreenB6CanonicalEvidence.Hash(bytes);
            new YocoCutoverReconciliationSource(config).Read().Item1.Contract.ShouldBe("Aqua.YocoCutover.ReconciliationV1");
            config[p+"ReconciliationSha256"]=new string('b',64);
            Should.Throw<InvalidOperationException>(()=>new YocoCutoverReconciliationSource(config).Read()).Message.ShouldBe("YOCO_RECONCILIATION_PIN_CHANGED");
            config["private:test"]=Convert.ToBase64String(new byte[17]);
            Should.Throw<InvalidOperationException>(()=>PrivateOperatorJson.TryRead(config,"environment:private:test","private:test",16,out _));
        }

        public static void Install(IConfiguration config,AQGreenD10Decision d,AQGreenD10VerifiedAuthority a,AQGreenCleanStartEvidence e)
        {
            var p=AQGreenD10OperatorEvidenceSource.Prefix;config[p+"AdmissionMode"]="CLEAN_START";config[p+"Enabled"]="true";
            var names=new[]{"Decision","VerifiedAuthority","CleanStartEvidence"};
            var bytes=new[]{AQGreenB6CanonicalEvidence.Bytes(d),AQGreenB6CanonicalEvidence.Bytes(a),AQGreenB6CanonicalEvidence.Bytes(e)};
            for(var i=0;i<names.Length;i++)
            {
                var key=p+names[i]+"JsonBase64";config[p+names[i]+"Path"]="environment:"+key;
                config[key]=Convert.ToBase64String(bytes[i]);config[p+names[i]+"Sha256"]=AQGreenB6CanonicalEvidence.Hash(bytes[i]);
            }
        }
    }
}
