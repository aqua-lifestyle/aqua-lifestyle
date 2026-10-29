using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    // Reuses the existing clean-start PostgreSQL 18.4 fixture. All identities,
    // provider signatures and financial facts here are synthetic fixtures.
    public sealed partial class AQGreenCleanStartPostgreSqlTests
    {
        private const string CutoverSecret = "whsec_c3ludGhldGljLWN1dG92ZXItc2lnbmluZy1rZXktb25seQ==";
        private static IConfiguration CutoverConfig() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string>
        { [YocoCutoverOptions.Prefix+"Enabled"]="true", [YocoCutoverOptions.Prefix+"OperatorUserId"]="7",
          ["Yoco:WebhookSecret"]=CutoverSecret }).Build();
        private static byte[] CutoverBody(string mode="test") => JsonSerializer.SerializeToUtf8Bytes(new
        { type="payment.succeeded", payload=new { id="pay_synthetic", type="payment",status="succeeded",amount=1200,
            currency="ZAR", mode, createdDate=DateTimeOffset.UtcNow.AddDays(-2),
            metadata=new Dictionary<string,string>{{YocoCheckoutMetadata.ProviderCheckoutId,"checkout_synthetic"}} } });
        private static (string Timestamp,string Signature) CutoverSign(byte[] body,string id)
        {
            var ts=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var prefix=Encoding.UTF8.GetBytes(id+"."+ts+".");
            using var hmac=new HMACSHA256(Convert.FromBase64String(CutoverSecret.Substring(6)));
            return(ts,"v1,"+Convert.ToBase64String(hmac.ComputeHash(prefix.Concat(body).ToArray())));
        }
        private sealed class CutoverStore : IYocoCutoverStore
        {
            private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;
            private readonly string _cs;
            public readonly AsyncLocal<AqualLifeStyleDbContext> Current=new();
            public Func<Task> BeforeCommit;
            public CutoverStore(AQGreenPlacementAllocatorPostgreSqlFixture fixture,string cs) { _fixture=fixture;_cs=cs; }
            public async Task<T> TransactAsync<T>(Func<YocoCutoverSnapshot,Task<T>> action)
            {
                await using var c=new NpgsqlConnection(_cs);await c.OpenAsync();
                await using var context=_fixture.CreateDbContext(c);await using var tx=await context.Database.BeginTransactionAsync();
                Current.Value=context;
                try { var result=await YocoCutoverStore.ExecuteAsync(context,action);
                    if(BeforeCommit!=null) await BeforeCommit();await tx.CommitAsync();return result; }
                finally { Current.Value=null; }
            }
            public Task<IAsyncDisposable> EnterBusinessAsync()=>YocoCutoverStore.EnterBusinessAsync(_cs);
            public Task ReadOnlyAsync(Func<Task> read) => throw new NotSupportedException("Receipt test double does not admit HTTP reads.");
        }
        private sealed class CutoverDispatch : IYocoCutoverDispatcher
        {
            public int Calls; public Func<VerifiedYocoPaymentNotification,int?,Task> Action;
            public async Task DispatchAsync(VerifiedYocoPaymentNotification n,int? tenant)
            { YocoCutoverDispatch.IsAuthorized.ShouldBeTrue();Interlocked.Increment(ref Calls);if(Action!=null) await Action(n,tenant); }
        }
        private sealed class CutoverRelease : IYocoCutoverReleaseGate
        { public bool Accepted;public Task VerifyAsync(){if(!Accepted)throw new InvalidOperationException("synthetic D10 hold");return Task.CompletedTask;} }
        private sealed class CutoverReconciliation : IYocoCutoverReconciliationSource
        {
            public YocoCutoverReconciliation Manifest;
            public (YocoCutoverReconciliation,string) Read() => (Manifest,new string('a',64));
            public void Accept(YocoCutoverEvent e,string mode="test")
            {
                Manifest=new YocoCutoverReconciliation { Contract="Aqua.YocoCutover.ReconciliationV1",
                    Authority="OPERATOR_ATTESTED_AUTHORITATIVE_YOCO_RECORDS",OperatorUserId=7,
                    AcceptedAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1),ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),
                    AquaAcceptanceReference="SYNTHETIC-ACCEPTANCE-NOT-HUMAN-EVIDENCE",OwnerAuthorizationReference="SYNTHETIC-AUTHORIZATION",
                    OutstandingReferencesReconciled=true,UnresolvedObligationsRemain=false,
                    CapturedEventSetSha256=YocoCutoverService.EventSetHash(e==null?Array.Empty<YocoCutoverEvent>():new[]{e}),
                    Entries=e==null?new():new(){new(){EventId=e.EventId,PayloadHash=e.PayloadHash,TenantId=1,PaymentId="pay_synthetic",
                        ProviderCheckoutId="checkout_synthetic",AmountInCents=1200,Currency="ZAR",Mode=mode,EventType="payment.succeeded",
                        Disposition="MATCHED",AuthoritativeRecordReference="SYNTHETIC-MERCHANT-RECORD",EvidenceSha256=new string('b',64)}} };
            }
        }
        private static YocoCutoverService CutoverService(CutoverStore store,out CutoverDispatch dispatch,out CutoverReconciliation proof,
            out CutoverRelease release,IConfiguration configuration=null)
        {
            var config=configuration??CutoverConfig();dispatch=new();proof=new();release=new();
            return new(store,new YocoWebhookSignatureVerifier(config),dispatch,proof,config,release);
        }
        private static async Task Capture(YocoCutoverService service,string id="evt_synthetic",byte[] body=null)
        { body??=CutoverBody();var (ts,sig)=CutoverSign(body,id);await service.ReceiveAsync(body,id,ts,sig); }
        private static YocoPaymentsController CutoverController(YocoCutoverService service,byte[] body,string id="evt_synthetic",bool valid=true)
        {
            var config=CutoverConfig();var (ts,sig)=CutoverSign(body,id);
            var c=new YocoPaymentsController(new YocoWebhookSignatureVerifier(config),null,
                new OptionsWrapper<Microsoft.AspNetCore.Mvc.JsonOptions>(new()),NullLogger<YocoPaymentsController>.Instance)
                {Cutover=service,ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
            c.Request.Body=new MemoryStream(body);c.Request.Headers["webhook-id"]=id;c.Request.Headers["webhook-timestamp"]=ts;
            c.Request.Headers["webhook-signature"]=valid?sig:"v1,aW52YWxpZA==";return c;
        }
        private static async Task Apply55(NpgsqlConnection c)=>await SqlAsync(c,await File.ReadAllTextAsync(Path.Combine(Operations,"migration-55.reviewed.sql")));

        [Fact]
        public async Task YocoReceiveOnly_Schema54DurablyCapturesWithoutBusinessEffectsAndNeverRunsAsAnAutomaticJob()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var before=await ProtectedDigestAsync(c);var store=new CutoverStore(_fixture,db.ConnectionString());
            var service=CutoverService(store,out var dispatch,out _,out _);
            await service.SetPhaseAsync("ReceiveOnly");var body=CutoverBody();
            (await CutoverController(service,body).WebhookAsync()).ShouldBeOfType<OkObjectResult>();
            var state=await service.StatusAsync();state.HasMigration55.ShouldBeFalse();state.Events.Count.ShouldBe(1);
            var e=state.Events.Single();e.Status.ShouldBe("Pending");e.RequiresReconciliation.ShouldBeTrue();Convert.FromBase64String(e.BodyBase64).ShouldBe(body);
            dispatch.Calls.ShouldBe(0);(await ProtectedDigestAsync(c)).ShouldBe(before);
            Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"__EFMigrationsHistory\"")).ShouldBe(54);
            Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ManualHeldEventV1' AND \"NextTryTime\">now()+interval '100 years' AND NOT \"IsAbandoned\"" )).ShouldBe(1);
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Replay"));
            await Should.ThrowAsync<InvalidOperationException>(()=>service.ReplayAsync(e.Id,7));
        }
        [Fact]
        public async Task YocoReceiveOnly_RejectsInvalidSignatureAndConflictingDuplicate_ConcurrentDuplicatesCommitOneEvent()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var service=CutoverService(new(_fixture,db.ConnectionString()),out var dispatch,out _,out _);var body=CutoverBody();
            (await CutoverController(service,body,valid:false).WebhookAsync()).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(403);
            (await service.StatusAsync()).Events.ShouldBeEmpty();
            await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Capture(service,body:body)));
            (await service.StatusAsync()).Events.Count.ShouldBe(1);
            (await CutoverController(service,CutoverBody("live")).WebhookAsync()).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
            (await service.StatusAsync()).Events.Single().PayloadHash.ShouldBe(YocoCutoverService.Hash(body));dispatch.Calls.ShouldBe(0);
        }
        [Fact]
        public async Task YocoReceiveOnly_AcknowledgementWaitsForCommit_StorageFailureRollsBackAndRetryAfterCommittedLostResponseIsExact()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var store=new CutoverStore(_fixture,db.ConnectionString());var service=CutoverService(store,out _,out _,out _);
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var proceed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.BeforeCommit=async()=>{entered.TrySetResult(true);await proceed.Task;};var body=CutoverBody();
            var response=CutoverController(service,body).WebhookAsync();await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            response.IsCompleted.ShouldBeFalse();Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"AbpBackgroundJobs\" WHERE \"JobType\"='Aqua.YocoCutover.ManualHeldEventV1'")).ShouldBe(0);
            proceed.SetResult(true);(await response).ShouldBeOfType<OkObjectResult>();store.BeforeCommit=null;
            // Simulate response loss after commit: identical provider delivery is safe.
            (await CutoverController(service,body).WebhookAsync()).ShouldBeOfType<OkObjectResult>();
            (await service.StatusAsync()).Events.Count.ShouldBe(1);
            store.BeforeCommit=()=>throw new IOException("synthetic commit boundary failure; never print envelope");
            (await CutoverController(service,body,"evt_rolled_back").WebhookAsync()).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
            store.BeforeCommit=null;(await service.StatusAsync()).Events.Count.ShouldBe(1);
            // Actual PostgreSQL connection loss before commit, not just a mock.
            store.BeforeCommit=async()=>await SqlAsync(c,"SELECT pg_terminate_backend("+
                ((NpgsqlConnection)store.Current.Value.Database.GetDbConnection()).ProcessID+")");
            (await CutoverController(service,body,"evt_connection_lost").WebhookAsync()).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
            store.BeforeCommit=null;(await service.StatusAsync()).Events.Count.ShouldBe(1);
        }
        [Fact]
        public async Task YocoReplay_LateEventRequiresPinnedAuthoritativeReconciliation_ConcurrentReplayIsIdempotent()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var store=new CutoverStore(_fixture,db.ConnectionString());var service=CutoverService(store,out var dispatch,out var proof,out var release);
            var body=CutoverBody("live");await Capture(service,body:body);await Apply55(c);await service.SetPhaseAsync("Replay");
            var e=(await service.StatusAsync()).Events.Single();
            await Should.ThrowAsync<InvalidOperationException>(()=>service.ReplayAsync(e.Id,8));
            (await service.ReplayAsync(e.Id,7)).ShouldBe("Held");dispatch.Calls.ShouldBe(0);
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));
            proof.Accept(e,"live");dispatch.Action=async(n,tenant)=>{
                tenant.ShouldBe(1);n.ConfirmedAt.ShouldBeLessThan(DateTimeOffset.UtcNow.AddDays(-1));
                await store.Current.Value.Database.ExecuteSqlRawAsync("UPDATE \"Customers\" SET \"MembershipId\"=NULL WHERE \"TenantId\"=1 AND \"Id\"=1");};
            var results=await Task.WhenAll(service.ReplayAsync(e.Id,7),service.ReplayAsync(e.Id,7));results.ShouldAllBe(x=>x=="Applied");dispatch.Calls.ShouldBe(1);
            e=(await service.StatusAsync()).Events.Single();e.ReconciliationDigest.ShouldBe(new string('a',64));e.ReplayedByUserId.ShouldBe(7);Convert.FromBase64String(e.BodyBase64).ShouldBe(body);
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));release.Accepted=true;
            proof.Manifest.UnresolvedObligationsRemain=true;
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));proof.Manifest.UnresolvedObligationsRemain=false;
            proof.Manifest.CapturedEventSetSha256=new string('c',64);
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));proof.Manifest.CapturedEventSetSha256=YocoCutoverService.EventSetHash(new[]{e});
            await service.SetPhaseAsync("Normal");await using(var lease=await store.EnterBusinessAsync()) { }
        }
        [Fact]
        public async Task YocoReplay_BusinessFailureRollsBackTogetherWithReceiptAndHoldsOriginalEnvelope()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            await SeedMembershipAssignmentsAsync(c);var before=await ProtectedDigestAsync(c);
            var store=new CutoverStore(_fixture,db.ConnectionString());var service=CutoverService(store,out var dispatch,out var proof,out _);
            await Capture(service);await Apply55(c);await service.SetPhaseAsync("Replay");var e=(await service.StatusAsync()).Events.Single();proof.Accept(e);
            dispatch.Action=async(_,_)=>{await store.Current.Value.Database.ExecuteSqlRawAsync("UPDATE \"Customers\" SET \"MembershipId\"=NULL WHERE \"TenantId\"=1 AND \"Id\"=1");throw new IOException("synthetic crash after business write");};
            (await service.ReplayAsync(e.Id,7)).ShouldBe("Held");(await ProtectedDigestAsync(c)).ShouldBe(before);
            var held=(await service.StatusAsync()).Events.Single();held.BodyBase64.ShouldBe(e.BodyBase64);held.ReplayedAtUtc.ShouldBeNull();held.ReconciliationDigest.ShouldBeNull();
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));
        }
        [Theory]
        [InlineData("payload")][InlineData("amount")][InlineData("tenant")][InlineData("expired")][InlineData("mode")][InlineData("authority")]
        public async Task YocoReplay_MismatchedOrUnacceptedProviderEvidenceNeverDispatches(string mismatch)
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var service=CutoverService(new(_fixture,db.ConnectionString()),out var dispatch,out var proof,out _);await Capture(service);await Apply55(c);await service.SetPhaseAsync("Replay");
            var e=(await service.StatusAsync()).Events.Single();proof.Accept(e);var entry=proof.Manifest.Entries.Single();
            switch(mismatch){case "payload":entry.PayloadHash=new string('c',64);break;case "amount":entry.AmountInCents++;break;
                case "tenant":entry.TenantId=0;break;case "expired":proof.Manifest.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-2);break;
                case "mode":entry.Mode="live";break;case "authority":proof.Manifest.Authority="APPLICATION_TEST_FLAG";break;}
            (await service.ReplayAsync(e.Id,7)).ShouldBe("Held");dispatch.Calls.ShouldBe(0);
        }
        [Fact]
        public async Task YocoReceiveOnly_UnknownSignedEventIsRetainedAndCannotBeReplayedAsSuccess()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var service=CutoverService(new(_fixture,db.ConnectionString()),out var dispatch,out var proof,out _);
            (await CutoverController(service,Encoding.UTF8.GetBytes("{\"type\":\"unknown.future.event\"}")).WebhookAsync()).ShouldBeOfType<OkObjectResult>();
            await Apply55(c);await service.SetPhaseAsync("Replay");var e=(await service.StatusAsync()).Events.Single();proof.Accept(e);
            (await service.ReplayAsync(e.Id,7)).ShouldBe("Held");dispatch.Calls.ShouldBe(0);
        }
        [Fact]
        public async Task YocoWriteFence_DrainsExistingBusinessLeasesAndBlocksAllLaterBusiness_QueuedEventsCannotBeBypassed()
        {
            await using var db=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(db.ConnectionString());await c.OpenAsync();await PrepareAsync(c);await Apply55(c);
            var store=new CutoverStore(_fixture,db.ConnectionString());var service=CutoverService(store,out var dispatch,out var proof,out var release);
            await service.SetPhaseAsync("Replay");release.Accepted=true;
            await Should.ThrowAsync<InvalidOperationException>(()=>service.SetPhaseAsync("Normal"));proof.Accept(null);
            await service.SetPhaseAsync("Normal");
            var lease=await store.EnterBusinessAsync();var change=service.SetPhaseAsync("ReceiveOnly");
            await Task.Delay(150);change.IsCompleted.ShouldBeFalse();await lease.DisposeAsync();await change;
            await Should.ThrowAsync<InvalidOperationException>(()=>store.EnterBusinessAsync());
            // A rejected session lease must release its advisory lock back to the pool.
            await service.SetPhaseAsync("Replay");await service.SetPhaseAsync("Normal");
            var config=CutoverConfig();var reached=false;
            var middleware=new ApplicationRecoveryMiddleware(_=>{reached=true;YocoCutoverDispatch.IsBusinessAuthorized.ShouldBeTrue();return Task.CompletedTask;},config,store);
            var context=new DefaultHttpContext();context.Request.Method="POST";context.Request.Path="/api/join";context.Response.Body=new MemoryStream();
            await middleware.InvokeAsync(context);reached.ShouldBeTrue();YocoCutoverDispatch.IsBusinessAuthorized.ShouldBeFalse();
            await service.SetPhaseAsync("ReceiveOnly");await Capture(service);await service.SetPhaseAsync("Replay");
            reached=false;await middleware.InvokeAsync(context);reached.ShouldBeFalse();context.Response.StatusCode.ShouldBe(503);dispatch.Calls.ShouldBe(0);
        }
    }
}
