using System;
using System.Threading.Tasks;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public class MinimumLiveAdmissionTests
    {
        private sealed class Store : IYocoCutoverStore
        {
            public readonly YocoCutoverSnapshot State=new(){Phase="Replay",HasMigration55=true};
            public Task<T> TransactAsync<T>(Func<YocoCutoverSnapshot,Task<T>> action)=>action(State);
            public Task<IAsyncDisposable> EnterBusinessAsync()=>throw new NotSupportedException();
            public Task ReadOnlyAsync(Func<Task> read)=>throw new NotSupportedException();
        }
        private sealed class Gate : IYocoCutoverReleaseGate
        {
            public int EmptyCalls;
            public bool Accepted=true;
            public Task VerifyAsync()=>Task.CompletedTask;
            public Task<string> VerifyEmptyProspectiveAsync(){EmptyCalls++;return Task.FromResult(Accepted?new string('b',64):null);}
        }
        private sealed class Manifest : IYocoCutoverReconciliationSource
        {
            public int Reads;
            public (YocoCutoverReconciliation,string) Read(){Reads++;throw new InvalidOperationException("synthetic selected manifest invalid");}
        }
        private static IConfiguration Config()=>new ConfigurationBuilder().AddInMemoryCollection(new[] {
            new System.Collections.Generic.KeyValuePair<string,string>(YocoCutoverOptions.Prefix+"Enabled","true"),
            new System.Collections.Generic.KeyValuePair<string,string>(YocoCutoverOptions.Prefix+"OperatorUserId","7")}).Build();

        [Fact]
        public async Task EmptyAdmissionIsDistinctFromMerchantReconciliationAndRequiresItsActualGate()
        {
            var store=new Store();var gate=new Gate();var manifest=new Manifest();
            var service=new YocoCutoverService(store,null,null,manifest,Config(),gate);
            gate.Accepted=false;
            (await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"))).Message.ShouldBe("YOCO_EMPTY_PROSPECTIVE_ADMISSION_REQUIRED");
            store.State.Phase.ShouldBe("Replay");store.State.PhaseEvidence.ShouldBeEmpty();
            gate.Accepted=true;await service.SetPhaseAsync("Normal");
            var evidence=store.State.PhaseEvidence.ShouldHaveSingleItem();
            evidence.AdmissionKind.ShouldBe(YocoCutoverPhaseEvidence.EmptyProspectiveV2);
            evidence.AdmissionEvidenceDigest.ShouldBe(new string('b',64));evidence.ReconciliationDigest.ShouldBeNull();manifest.Reads.ShouldBe(0);
        }

        [Theory]
        [InlineData("ReconciliationPath")][InlineData("ReconciliationSha256")][InlineData("ReconciliationJsonBase64")]
        public async Task AnySelectedManifestInputCannotFallBackToEmptyAdmission(string selector)
        {
            var store=new Store();var gate=new Gate();var manifest=new Manifest();var config=Config();
            config[YocoCutoverOptions.Prefix+selector]="synthetic selected input";
            var service=new YocoCutoverService(store,null,null,manifest,config,gate);
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));
            gate.EmptyCalls.ShouldBe(0);manifest.Reads.ShouldBe(1);store.State.Phase.ShouldBe("Replay");
        }

        [Fact]
        public async Task EvenAppliedCapturedEventsCannotBeOmittedFromAReconciliationFreeLaunch()
        {
            var store=new Store();store.State.Events.Add(new YocoCutoverEvent{EventId="synthetic-old",Status="Applied"});
            var gate=new Gate();var service=new YocoCutoverService(store,null,null,new Manifest(),Config(),gate);
            (await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"))).Message.ShouldBe("YOCO_NONEMPTY_INBOX_REQUIRES_RECONCILIATION");
            gate.EmptyCalls.ShouldBe(0);store.State.Phase.ShouldBe("Replay");
        }
    }
}
