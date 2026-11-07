using System;
using System.Linq;
using System.Text.Json;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public class D10OneTimeActivationTests
    {
        private static (AQGreenD10Commissioning Authority, YocoCutoverSnapshot State, DateTime Expires) Fixture()
        {
            var (x,clean)=AQGreenCleanStartCommissioningTests.Create();
            AQGreenCleanStartCommissioningTests.OwnerAttest(clean);
            AQGreenCleanStartCommissioningTests.PostReset(x,clean);
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            var a=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,clean);
            var at=DateTime.UtcNow;
            var s=new YocoCutoverSnapshot {Phase="Normal",HasMigration55=true};
            s.PhaseEvidence.Add(new(){Phase="Replay",OperatorUserId=7,CommittedAtUtc=new DateTimeOffset(at.AddSeconds(-1))});
            s.PhaseEvidence.Add(new(){Phase="Normal",OperatorUserId=7,CommittedAtUtc=new DateTimeOffset(at),
                AdmissionKind=YocoCutoverPhaseEvidence.EmptyProspectiveV2,AdmissionEvidenceDigest=new string('a',64),
                D10Activation=a.CreateActivationReceipt(at)});
            return(a,s,x.Authority.ExpiresAtUtc);
        }

        [Fact]
        public void ExpiryIsStrictForUnusedAuthorizationButNotForTimelyCommittedActivation()
        {
            var (a,s,expires)=Fixture();
            foreach(var time in new[]{expires,expires.AddTicks(1)})
                Should.Throw<InvalidOperationException>(()=>a.CreateActivationReceipt(time)).Message.ShouldBe("D10_AUTHORITY_EXPIRED");
            var original=AQGreenB6CanonicalEvidence.Digest(s);
            a.VerifyCommittedActivation(s,7,expires.AddDays(365)).ShouldNotBeNull();
            AQGreenB6CanonicalEvidence.Digest(s).ShouldBe(original);
            s.Phase="Replay";s.PhaseEvidence.RemoveAt(1);
            a.VerifyCommittedActivation(s,7,expires.AddDays(365)).ShouldBeNull();
            Should.Throw<InvalidOperationException>(()=>a.EnsureCurrent(expires.AddDays(365))).Message.ShouldBe("D10_AUTHORITY_EXPIRED");
        }

        [Theory]
        [InlineData("missing-receipt")][InlineData("wrong-contract")][InlineData("foreign-decision")][InlineData("foreign-authority")]
        [InlineData("changed-expiry")][InlineData("late-activation")][InlineData("future-commit")][InlineData("missing-replay")]
        [InlineData("wrong-operator")][InlineData("missing-admission")][InlineData("mixed-admission")][InlineData("no-migration")]
        [InlineData("null-history")][InlineData("bare-normal")][InlineData("duplicate-normal")][InlineData("wrong-current-phase")]
        [InlineData("receipt-on-replay")][InlineData("non-utc")][InlineData("wrong-tenant")]
        [InlineData("zero-admission")][InlineData("zero-reconciliation")]
        public void IncompleteFabricatedOrForeignNormalNeverWaivesExpiry(string failure)
        {
            var (a,s,expires)=Fixture();var e=s.PhaseEvidence.Last();
            switch(failure)
            {
                case "missing-receipt":e.D10Activation=null;break;
                case "wrong-contract":e.D10Activation.ContractVersion="unknown";break;
                case "foreign-decision":e.D10Activation.DecisionDigest=new string('0',64);break;
                case "foreign-authority":e.D10Activation.VerifiedAuthorityDigest=new string('0',64);break;
                case "changed-expiry":e.D10Activation.AuthorizationExpiresAtUtc=expires.AddDays(1);break;
                case "late-activation":e.D10Activation.ActivatedAtUtc=expires;e.CommittedAtUtc=new DateTimeOffset(expires);break;
                case "future-commit":e.CommittedAtUtc=new DateTimeOffset(expires.AddDays(366));break;
                case "missing-replay":s.PhaseEvidence.RemoveAt(0);break;
                case "wrong-operator":e.OperatorUserId=8;break;
                case "missing-admission":e.AdmissionEvidenceDigest=null;break;
                case "mixed-admission":e.ReconciliationDigest=new string('b',64);break;
                case "no-migration":s.HasMigration55=false;break;
                case "null-history":s.PhaseEvidence=null;break;
                case "bare-normal":s.PhaseEvidence.Clear();break;
                case "duplicate-normal":s.PhaseEvidence.Add(e);break;
                case "wrong-current-phase":s.Phase="Uninitialized";break;
                case "receipt-on-replay":s.PhaseEvidence[0].D10Activation=e.D10Activation;break;
                case "non-utc":e.D10Activation.ActivatedAtUtc=DateTime.SpecifyKind(e.D10Activation.ActivatedAtUtc,DateTimeKind.Unspecified);break;
                case "wrong-tenant":e.D10Activation.TenantId=2;break;
                case "zero-admission":e.AdmissionEvidenceDigest=new string('0',64);break;
                case "zero-reconciliation":e.AdmissionEvidenceDigest=null;e.AdmissionKind=null;e.ReconciliationDigest=new string('0',64);break;
            }
            Should.Throw<InvalidOperationException>(()=>a.VerifyCommittedActivation(s,7,expires.AddDays(365)))
                .Message.ShouldBe("D10_COMMITTED_ACTIVATION_INVALID");
        }

        [Fact]
        public void LaterRecoveryRetainsTheOriginalActivationAndCannotRefreshItsDeadline()
        {
            var (a,s,expires)=Fixture();var first=s.PhaseEvidence.Last().D10Activation;
            s.PhaseEvidence.Add(new(){Phase="Replay",OperatorUserId=7,CommittedAtUtc=new DateTimeOffset(expires.AddDays(2))});
            s.PhaseEvidence.Add(new(){Phase="Normal",OperatorUserId=7,CommittedAtUtc=new DateTimeOffset(expires.AddDays(3)),
                ReconciliationDigest=new string('b',64),D10Activation=JsonSerializer.Deserialize<AQGreenD10ActivationReceipt>(JsonSerializer.Serialize(first))});
            a.VerifyCommittedActivation(s,7,expires.AddDays(4)).ActivatedAtUtc.ShouldBe(first.ActivatedAtUtc);
            s.PhaseEvidence.Last().D10Activation.ActivatedAtUtc=expires.AddDays(3);
            Should.Throw<InvalidOperationException>(()=>a.VerifyCommittedActivation(s,7,expires.AddDays(4)));
        }

        [Fact]
        public void ExpiryAtPersistenceCannotBecomeASuccessfulActivation()
        {
            var (_,s,expires)=Fixture();var receipt=s.PhaseEvidence.Last().D10Activation;
            Should.NotThrow(()=>receipt.EnsureNewCommitAllowed(expires.AddTicks(-1)));
            Should.Throw<InvalidOperationException>(()=>receipt.EnsureNewCommitAllowed(expires))
                .Message.ShouldBe("D10_AUTHORITY_EXPIRED_BEFORE_ACTIVATION_COMMIT");
            Should.Throw<JsonException>(()=>JsonSerializer.Deserialize<AQGreenD10ActivationReceipt>("{}"));
        }
    }
}
