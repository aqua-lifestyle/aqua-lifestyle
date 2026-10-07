using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace AqualLifeStyle.Customer11Backfill.Tests;

[Collection("Customer11 PostgreSQL")]
public sealed class BackfillPostgreSqlTests(PostgreSqlFixture fixture)
{
    private static readonly string[] NewIds = {
        "c36485ad-65c2-c44e-54a8-abbba3b91cfa", "f5110269-2d6d-47a7-4e8a-dc03b9120cc6",
        "1f8f57f6-1b42-ee1e-1ddc-d1d92966a0c6", "08280e99-67e9-4e4d-4359-240f02eba052",
        "f003f647-9d57-b259-5656-67bf91231662", "1d251e09-d29a-d47f-afb6-950aa3ef6634" };
    private static readonly string[] OldIds = {
        "31599db2-bfd8-f661-a328-a66b987e20a7", "97c80850-dfec-5f9c-c309-aa266f426a94",
        "86988bb0-38b0-cffe-4075-dbce22fcde84", "5161a991-a5f1-50b4-45b8-c40222d14d73",
        "f7811ac4-256d-02ef-c20d-1c46bbda906c", "7e772bed-3c78-5caa-5521-809caa7e0113" };

    [Fact]
    public void AcceptedNewLineage_ExactIds_Unknowns_AndCustodyPreserved()
    {
        Assert.Equal(180006,fixture.ObservedServerVersionNumber);
        Assert.Equal("74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416",AQGreenCustomer11BackfillPlan.InputSha256);
        Assert.Equal("a1bcf510b41fa97e449e0dcb938d2b492ad08f215f6d4e5d75ff117f8b6af11d",AQGreenCustomer11BackfillPlan.OutputSha256);
        Assert.Equal("b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494",fixture.Plan.SourceSnapshotSha256);
        Assert.Equal("NEW RECOVERY-CLONE EVIDENCE CAPTURE",fixture.Plan.SourceProvenance.GetProperty("captureLabel").GetString());
        Assert.Equal("NOT CLAIMED",fixture.Plan.CustodyDisclosure.GetProperty("uninterruptedCustody").GetString());
        Assert.Equal(new[] {11,14,15,17,19,18},fixture.Plan.Members.Select(m=>m.CustomerId));
        for(int i=0;i<6;i++)
        {
            var m=fixture.Plan.Members[i];
            Assert.Equal(Guid.Parse(NewIds[i]),fixture.Plan.PlacementId(m.ParticipantId));
            Assert.DoesNotContain(fixture.Plan.PlacementId(m.ParticipantId),OldIds.Select(Guid.Parse));
            Assert.Null(m.EffectiveV1StructuralLevel);
            Assert.Equal(i==0?"UNKNOWN":"RETAINED_POSITIVE_DECISION",m.HistoricalAdministratorApproval);
            Assert.Equal("LegacyUnknownAcquisition",m.AcquisitionProvenance);
            Assert.Equal("UNKNOWN",m.HistoricalInvitation); Assert.Equal("UNKNOWN",m.HistoricalParticipantConfirmation);
        }
    }

    [Theory]
    [InlineData("input")][InlineData("output")][InlineData("capture")][InlineData("mixed")]
    public void HistoricalOrMixedLineage_Rejected(string kind)
    {
        var input=fixture.Input.ToArray(); var output=fixture.Output.ToArray(); var capture=fixture.Capture.ToArray();
        if(kind=="input" || kind=="mixed") input=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(input).Replace(
            AQGreenCustomer11BackfillPlan.CaptureSha256,"ac89f43cbe89959b7da690c836c33f6365593476cb687db1f29c059f04b25c8e",StringComparison.Ordinal));
        if(kind=="output" || kind=="mixed") output=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(output).Replace(
            AQGreenCustomer11BackfillPlan.InputSha256,"57529d67d4320595c90e2dedaf492d97c42fede1febd22caea014d0fc16f257c",StringComparison.Ordinal));
        if(kind=="capture") capture=Encoding.UTF8.GetBytes("{}\n{}\n");
        Assert.Throws<InvalidOperationException>(()=>AQGreenCustomer11BackfillPlan.Load(input,output,capture));
    }

    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("failed")][InlineData("misordered")]
    [InlineData("database")][InlineData("user")][InlineData("version")][InlineData("readonly")]
    [InlineData("origin")][InlineData("label")][InlineData("time")][InlineData("supplement")]
    [InlineData("identity-database")][InlineData("identity-user")][InlineData("extra")]
    [InlineData("failed-second")][InlineData("failed-third")][InlineData("failed-fourth")]
    public void SevenRecordCapture_RejectsInvalidTransportIndependentlyOfHash(string kind)
    {
        var r=Encoding.UTF8.GetString(fixture.Capture).Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>JsonNode.Parse(s)!).ToList();
        switch(kind)
        {
            case "missing": r.RemoveAt(0); break;
            case "extra": r.Add(r[6].DeepClone()); break;
            case "duplicate": r[1]=r[0].DeepClone(); break;
            case "misordered": (r[0],r[1])=(r[1],r[0]); break;
            case "failed": r[0]["result"]="FAIL"; break;
            case "failed-second": r[1]["result"]="FAIL"; break;
            case "failed-third": r[2]["result"]="FAIL"; break;
            case "failed-fourth": r[3]["result"]="FAIL"; break;
            case "database": r[0]["observed"]="aqualifestyle"; break;
            case "user": r[1]["observed"]="wrong"; break;
            case "version": r[2]["observed"]=180004; break;
            case "readonly": r[3]["observed"]="off"; break;
            case "origin": r[4]["clone_service_id"]=AQGreenCustomer11TargetGuard.ProductionServiceId; break;
            case "label": r[4]["capture_label"]="ORIGINAL PRODUCTION CAPTURE"; break;
            case "time": r[4]["historical_pitr_cutoff"]="2026-10-03T00:00:00Z"; break;
            case "identity-database": r[4]["database"]="wrong"; break;
            case "identity-user": r[4]["user"]="wrong"; break;
            case "supplement": r[6]["placement_count"]=1; break;
        }
        var bytes=Encoding.UTF8.GetBytes(string.Join('\n',r.Select(n=>n.ToJsonString()))+"\n");
        Assert.Throws<InvalidOperationException>(()=>AQGreenCustomer11BackfillPlan.VerifyCaptureStructure(bytes));
        Assert.Throws<InvalidOperationException>(()=>AQGreenCustomer11BackfillPlan.Load(fixture.Input,fixture.Output,bytes));
    }

    [Fact]
    public void ExactSevenRecordCapture_PresentationVersionIsObservationOnly()
    {
        AQGreenCustomer11BackfillPlan.VerifyCaptureStructure(fixture.Capture);
        var r=Encoding.UTF8.GetString(fixture.Capture).Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>JsonNode.Parse(s)!).ToArray();
        r[2]["server_version"]="18.6 distribution observation";
        r[4]["version"]="PostgreSQL 18.6 distribution observation";
        AQGreenCustomer11BackfillPlan.VerifyCaptureStructure(Encoding.UTF8.GetBytes(string.Join('\n',r.Select(n=>n.ToJsonString()))+"\n"));
    }

    // No real provider endpoint is used. This structurally shaped host is synthetic and
    // is only passed to the pure guard; never resolved or connected.
    private static (NpgsqlConnectionStringBuilder Connection,AQGreenBackfillTargetIdentity Target,JsonObject Receipt) CloneContext()
    {
        var service=AQGreenCustomer11BackfillPlan.RecoveryServiceId;
        var endpoint=service+".fixture-postgres.render.com";
        var c=new NpgsqlConnectionStringBuilder { Host=endpoint,Database=AQGreenCustomer11BackfillPlan.RecoveryDatabase,
            Username=AQGreenCustomer11BackfillPlan.RecoveryUser,Port=5432,SslMode=SslMode.VerifyFull };
        var r=new JsonObject {
            ["verification"]="OWNER_AUTHORISED_INDEPENDENT_TARGET_VERIFICATION",["targetKind"]="verified-recovery-clone",
            ["serviceId"]=service,["database"]=c.Database,["user"]=c.Username,["endpoint"]=endpoint,["externalPort"]=5432,
            ["serverAddress"]="192.0.2.1",["serverPort"]=5432,["serverVersionNumber"]=180006,
            ["inputSha256"]=AQGreenCustomer11BackfillPlan.InputSha256,["outputSha256"]=AQGreenCustomer11BackfillPlan.OutputSha256,
            ["captureSha256"]=AQGreenCustomer11BackfillPlan.CaptureSha256,
            ["scopeId"]="06a452ea-9958-5911-8c5b-5d350e516095",
            ["pitrSourceServiceId"]=AQGreenCustomer11TargetGuard.ProductionServiceId,["pitrCutoffUtc"]="2026-10-02T05:37:10Z",
            ["productionServiceId"]=AQGreenCustomer11TargetGuard.ProductionServiceId,
            ["productionDatabase"]=AQGreenCustomer11TargetGuard.ProductionDatabase,["isolatedFromProduction"]=true };
        var t=new AQGreenBackfillTargetIdentity(c.Database!,c.Username!,"192.0.2.1",5432,AQGreenCustomer11BackfillPlan.CaptureSha256,
            "verified-recovery-clone",service,180006);
        return(c,BindReceipt(t,r),r);
    }
    private static AQGreenBackfillTargetIdentity BindReceipt(AQGreenBackfillTargetIdentity t,JsonObject r)
    {
        var bytes=Encoding.UTF8.GetBytes(r.ToJsonString());
        return t with { VerifiedTargetReceipt=bytes,VerifiedTargetReceiptSha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
    }

    [Fact]
    public void RecoveryGuard_DoesNotDenyRecoveryEvidenceOrigin_NoNetworkExecution()
    {
        var (c,t,_)=CloneContext(); AQGreenCustomer11TargetGuard.Verify(c,t);
    }

    [Theory]
    [InlineData("production-host")][InlineData("production-database")][InlineData("production-service")]
    [InlineData("database")][InlineData("user")][InlineData("service")][InlineData("version")]
    [InlineData("alias")][InlineData("multi-host")][InlineData("port")][InlineData("ssl")]
    [InlineData("missing-receipt")][InlineData("forged-reference")][InlineData("forged-endpoint")]
    [InlineData("forged-service")][InlineData("forged-isolation")][InlineData("old-input")][InlineData("old-output")]
    [InlineData("old-capture")][InlineData("options")][InlineData("trust-certificate")]
    [InlineData("wrong-pitr-source")][InlineData("wrong-pitr-cutoff")][InlineData("wrong-scope")]
    public async Task CloneContextOrEndpointMismatch_DirectWriterRejectsBeforeConnection(string kind)
    {
        var(c,t,r)=CloneContext();
        switch(kind)
        {
            case "production-host": c.Host=AQGreenCustomer11TargetGuard.ProductionServiceId+".fixture-postgres.render.com"; break;
            case "production-database": c.Database=AQGreenCustomer11TargetGuard.ProductionDatabase; t=t with {Database=c.Database}; break;
            case "production-service": t=t with {ServiceId=AQGreenCustomer11TargetGuard.ProductionServiceId}; break;
            case "database": c.Database="wrong"; t=t with {Database="wrong"}; break;
            case "user": c.Username="wrong"; t=t with {User="wrong"}; break;
            case "service": t=t with {ServiceId="wrong"}; break;
            case "version": t=t with {ServerVersionNumber=180004}; break;
            case "alias": c.Host="alias.example.invalid"; break;
            case "multi-host": c.Host+=",127.0.0.1"; break;
            case "port": c.Port=1; break;
            case "ssl": c.SslMode=SslMode.Require; break;
            case "missing-receipt": t=t with {VerifiedTargetReceipt=null}; break;
            case "forged-reference": t=t with {VerifiedTargetReceiptSha256=new string('0',64)}; break;
            case "forged-endpoint": r["endpoint"]="alias.example.invalid"; t=BindReceipt(t,r); break;
            case "forged-service": r["serviceId"]=AQGreenCustomer11TargetGuard.ProductionServiceId; t=BindReceipt(t,r); break;
            case "forged-isolation": r["isolatedFromProduction"]=false; t=BindReceipt(t,r); break;
            case "old-input": r["inputSha256"]="57529d67d4320595c90e2dedaf492d97c42fede1febd22caea014d0fc16f257c"; t=BindReceipt(t,r); break;
            case "old-output": r["outputSha256"]="46a7ae8a01b28b4c8896f1f778f704e6673256164b3825c1a052f1d113430978"; t=BindReceipt(t,r); break;
            case "old-capture": r["captureSha256"]="ac89f43cbe89959b7da690c836c33f6365593476cb687db1f29c059f04b25c8e"; t=BindReceipt(t,r); break;
            case "options": c.Options="-c search_path=pg_catalog"; break;
            case "trust-certificate": c["Trust Server Certificate"]=true; break;
            case "wrong-pitr-source": r["pitrSourceServiceId"]="wrong"; t=BindReceipt(t,r); break;
            case "wrong-pitr-cutoff": r["pitrCutoffUtc"]="2026-10-03T00:00:00Z"; t=BindReceipt(t,r); break;
            case "wrong-scope": r["scopeId"]=Guid.NewGuid().ToString(); t=BindReceipt(t,r); break;
        }
        var interceptor=new NoConnection();
        await using var guarded=new AqualLifeStyleDbContext(new DbContextOptionsBuilder<AqualLifeStyleDbContext>().UseNpgsql(c.ConnectionString).AddInterceptors(interceptor).Options);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>new AQGreenCustomer11BackfillWriter(guarded).ExecuteAsync(fixture.Plan,t,true));
        Assert.StartsWith("TargetIdentity:",error.Message); Assert.Equal(0,interceptor.Attempts);
    }

    private sealed class NoConnection : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData eventData,InterceptionResult result,CancellationToken cancellationToken=default)
        { Attempts++; throw new InvalidOperationException("Unexpected network connection"); }
    }

    [Fact]
    public async Task CloneModeCli_ProductionEndpointRejectedBeforeAnyNetworkConnection()
    {
        var(c,t,_)=CloneContext();
        c.Host=AQGreenCustomer11TargetGuard.ProductionServiceId+".fixture-postgres.render.com";
        var result=await Command("unused","dry-run",c.ConnectionString,t);
        Assert.Equal(1,result.Exit); Assert.Contains("TargetIdentity:ProductionDenied",result.Output);
        Assert.DoesNotContain("POSTGRESQL_FAILURE",result.Output);
    }

    [Theory]
    [InlineData("old")][InlineData("arbitrary")][InlineData("mixed")]
    public async Task SameTopologyDifferentStorageIdentity_FailsClosed(string kind)
    {
        await using var db=await fixture.NewDatabase();
        await using(var context=fixture.Context(db.Dsn))
        {
            await using var tx=await context.Database.BeginTransactionAsync();
            var scope=AQGreenPlacementTreeScope.Create(1); scope.Id=fixture.Plan.ScopeId;
            context.AQGreenPlacementTreeScopes.Add(scope); await context.SaveChangesAsync();
            AQGreenNetworkPlacement? root=null;
            for(int i=0;i<6;i++)
            {
                var m=fixture.Plan.Members[i];
                var p=i==0?AQGreenNetworkPlacement.CreateRoot(scope,m.ParticipantId,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion):
                    AQGreenNetworkPlacement.CreateChild(root!,m.ParticipantId,m.Slot!.Value,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion);
                p.Id=kind=="arbitrary"?Guid.NewGuid():Guid.Parse(kind=="mixed" && i%2==0?NewIds[i]:OldIds[i]);
                context.AQGreenNetworkPlacements.Add(p); await context.SaveChangesAsync(); if(i==0) root=p;
            }
            await tx.CommitAsync();
        }
        var before=await TopologyDigest(db.Dsn); var other=await NonTopologyDigest(db.Dsn);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,true));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false));
        Assert.Equal(before,await TopologyDigest(db.Dsn)); Assert.Equal(other,await NonTopologyDigest(db.Dsn));
    }

    [Fact]
    public async Task ProspectivePrimaryKeyCollisionElsewhere_RejectedByDryRunBeforeInsert()
    {
        await using var db=await fixture.NewDatabase(); var id=Guid.NewGuid();
        await PostgreSqlFixture.Sql(db.Dsn,"""
            INSERT INTO "Customers" ("Id","TenantId","Name","Email","AreaId","IsActive","CreationTime","ClubMemberNumber","UserId","IsDeleted")
            VALUES (999,1,'Fixture','collision@example.test','cccccccc-0000-0000-0000-000000000001',true,NOW(),'COLLISION',9000,false);
            INSERT INTO "EntryParticipations" ("Id","TenantId","CustomerId","Status","StartedAt","TermsVersion","TermsEffectiveFrom","JoiningPaymentAmount","JoiningInstallmentAmount","RegistrationPaymentAmount","ActivationPaymentAmount","MonthlyCommitmentAmount","GracePeriodDays","Currency","CreationTime","IsDeleted")
            VALUES (@id,1,999,0,TIMESTAMPTZ '2026-01-01','fixture',TIMESTAMPTZ '2026-01-01',1200,0,0,0,600,7,'ZAR',NOW(),false);
            """,new NpgsqlParameter("id",id));
        await using(var context=fixture.Context(db.Dsn))
        {
            await using var tx=await context.Database.BeginTransactionAsync();
            var scope=AQGreenPlacementTreeScope.Create(1); context.AQGreenPlacementTreeScopes.Add(scope); await context.SaveChangesAsync();
            var p=AQGreenNetworkPlacement.CreateRoot(scope,id,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion);
            p.Id=Guid.Parse(NewIds[0]); context.AQGreenNetworkPlacements.Add(p); await context.SaveChangesAsync(); await tx.CommitAsync();
        }
        var before=await TopologyDigest(db.Dsn);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,true));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false));
        Assert.Equal(before,await TopologyDigest(db.Dsn));
    }

    [Fact]
    public async Task OperatorCommand_DryRun_Apply_Retry_AndProductionEndpointRefusal()
    {
        await using var db=await fixture.NewDatabase();
        var dry=await Command(db.Dsn,"dry-run",db.Dsn); Assert.Equal(0,dry.Exit);
        Assert.Contains("\"ScopeInserts\": 1",dry.Output); Assert.Contains("\"PlacementInserts\": 6",dry.Output);
        await AssertNoTopology(db.Dsn);
        var apply=await Command(db.Dsn,"apply",db.Dsn); Assert.Equal(0,apply.Exit); await AssertExactTopology(db.Dsn);
        var retry=await Command(db.Dsn,"apply",db.Dsn); Assert.Equal(0,retry.Exit); Assert.Contains("\"AlreadyApplied\": true",retry.Output);
        var production=new NpgsqlConnectionStringBuilder(db.Dsn) { Host=fixture.Plan.SourceServiceId+".example.invalid" };
        var blocked=await Command(db.Dsn,"apply",production.ConnectionString); Assert.Equal(1,blocked.Exit);
        Assert.Contains("loopback endpoint",blocked.Output); Assert.DoesNotContain("local-disposable-only",blocked.Output);
    }

    [Theory]
    [InlineData("domain-uppercase")]
    [InlineData("lowercase")]
    [InlineData("mixed-case")]
    public async Task AcceptedAsciiHexRepresentations_ApplyExactlyAndRetryWithoutWrites(string representation)
    {
        await using var db=await fixture.NewDatabase();
        var root=fixture.Plan.Members[0];
        var hash=representation switch {
            "domain-uppercase"=>YocoWebhookReceipt.Record(1,"fixture-event","fixture-payment","fixture-checkout",
                new string('a',64),YocoCheckoutProgramme.AQGreen,root.CapturedCheckoutId,root.CapturedReceiptProcessedAtUtc).PayloadHash,
            "lowercase"=>string.Concat(Enumerable.Repeat("0123456789abcdef",4)),
            "mixed-case"=>string.Concat(Enumerable.Repeat("0123456789aBcDeF",4)),
            _=>throw new InvalidOperationException() };
        if(representation=="domain-uppercase")
        {
            Assert.Equal(new string('A',64),hash);
            Assert.Equal(hash,await PostgreSqlFixture.Scalar<string>(db.Dsn,
                "SELECT \"PayloadHash\" FROM \"YocoWebhookReceipts\" WHERE \"CheckoutReferenceId\"='"+root.CapturedCheckoutId+"'"));
        }
        else await PostgreSqlFixture.Sql(db.Dsn,"UPDATE \"YocoWebhookReceipts\" SET \"PayloadHash\"=@hash WHERE \"Id\"=@receipt",
            new NpgsqlParameter("hash",hash),new NpgsqlParameter("receipt",root.CapturedReceiptId));
        var before=await NonTopologyDigest(db.Dsn);
        Assert.Equal(new AQGreenBackfillResult(true,false,1,6),await Run(db.Dsn,true));
        await AssertNoTopology(db.Dsn);
        Assert.Equal(new AQGreenBackfillResult(false,false,1,6),await Run(db.Dsn,false));
        await AssertExactTopology(db.Dsn);
        var placed=await TopologyDigest(db.Dsn);
        Assert.Equal(new AQGreenBackfillResult(false,true,0,0),await Run(db.Dsn,false));
        Assert.Equal(placed,await TopologyDigest(db.Dsn));
        Assert.Equal(before,await NonTopologyDigest(db.Dsn));
    }

    [Theory]
    [InlineData("null","PayloadHashNull")]
    [InlineData("empty","PayloadHashLength")]
    [InlineData("short","PayloadHashLength")]
    [InlineData("long","PayloadHashLength")]
    [InlineData("space","PayloadHashCharacters")]
    [InlineData("tab","PayloadHashCharacters")]
    [InlineData("non-hex","PayloadHashCharacters")]
    [InlineData("fullwidth","PayloadHashCharacters")]
    [InlineData("unicode-digit","PayloadHashCharacters")]
    public async Task InvalidHash_ReportsExactCategoryAndLeavesAllTablesUnchanged(string kind,string category)
    {
        await using var db=await fixture.NewDatabase();
        var value=kind switch {
            "null"=>null,"empty"=>"","short"=>new string('a',63),"long"=>new string('a',65),
            "space"=>new string('a',63)+" ","tab"=>new string('a',63)+"\t","non-hex"=>new string('g',64),
            "fullwidth"=>new string('\uFF21',64),"unicode-digit"=>new string('\u0661',64),
            _=>throw new InvalidOperationException() };
        var interceptors=Array.Empty<IInterceptor>();
        if(kind=="null")
        {
            // PostgreSQL forbids persisted NULL here. Exercise the defensive null branch
            // after materialization without weakening that constraint or changing stored rows.
            interceptors=new IInterceptor[] { new NullReceiptHash() };
        }
        else
        {
            // Fixture-only text permits malformed lengths without char(64) padding/truncation.
            // The writer's source validation remains enabled; topology constraints stay intact.
            await PostgreSqlFixture.Sql(db.Dsn,"ALTER TABLE \"YocoWebhookReceipts\" ALTER COLUMN \"PayloadHash\" TYPE text;");
            await PostgreSqlFixture.Sql(db.Dsn,"UPDATE \"YocoWebhookReceipts\" SET \"PayloadHash\"=@hash WHERE \"Id\"=@receipt",
                new NpgsqlParameter("hash",value!),new NpgsqlParameter("receipt",fixture.Plan.Members[0].CapturedReceiptId));
        }
        var before=await NonTopologyDigest(db.Dsn);
        var target=await GetTarget(db.Dsn);
        await using var context=fixture.Context(db.Dsn,interceptors);
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>
            new AQGreenCustomer11BackfillWriter(context).ExecuteAsync(fixture.Plan,target,false));
        Assert.Equal("Retained joining-receipt binding differs. Predicate: "+category+".",failure.Message);
        if(!string.IsNullOrEmpty(value)) Assert.DoesNotContain(value,failure.Message);
        await AssertNoTopology(db.Dsn);
        Assert.Equal(before,await NonTopologyDigest(db.Dsn));
    }

    [Theory]
    [InlineData("missing","ReceiptCount")]
    [InlineData("duplicate","ReceiptCount")]
    [InlineData("receipt-id","ReceiptId")]
    [InlineData("processed-at","ProcessedAt")]
    [InlineData("tenant","Tenant")]
    [InlineData("programme","Programme")]
    [InlineData("provider-checkout","ProviderCheckoutBinding")]
    [InlineData("payment","PaymentBinding")]
    [InlineData("checkout-reference","ReceiptCount")]
    [InlineData("event-empty","EventPresence")]
    [InlineData("event-whitespace","EventPresence")]
    public async Task ReceiptBindingConflict_ReportsExactCategoryAndRollsBack(string kind,string category)
    {
        await using var db=await fixture.NewDatabase();
        var sql=kind switch {
            "missing"=>"DELETE FROM \"YocoWebhookReceipts\" WHERE \"Id\"=@receipt",
            "duplicate"=>"INSERT INTO \"YocoWebhookReceipts\" (\"Id\",\"TenantId\",\"EventId\",\"PaymentId\",\"ProviderCheckoutId\",\"PayloadHash\",\"Programme\",\"CheckoutReferenceId\",\"ProcessedAt\",\"CreationTime\") SELECT @other,\"TenantId\",'fixture-duplicate-event',\"PaymentId\",\"ProviderCheckoutId\",\"PayloadHash\",\"Programme\",\"CheckoutReferenceId\",\"ProcessedAt\",\"CreationTime\" FROM \"YocoWebhookReceipts\" WHERE \"Id\"=@receipt",
            "receipt-id"=>"UPDATE \"YocoWebhookReceipts\" SET \"Id\"=@other WHERE \"Id\"=@receipt",
            "processed-at"=>"UPDATE \"YocoWebhookReceipts\" SET \"ProcessedAt\"=\"ProcessedAt\"+interval '1 microsecond' WHERE \"Id\"=@receipt",
            "tenant"=>"UPDATE \"YocoWebhookReceipts\" SET \"TenantId\"=2 WHERE \"Id\"=@receipt",
            "programme"=>"UPDATE \"YocoWebhookReceipts\" SET \"Programme\"=1 WHERE \"Id\"=@receipt",
            "provider-checkout"=>"UPDATE \"YocoWebhookReceipts\" SET \"ProviderCheckoutId\"='private-fixture-checkout' WHERE \"Id\"=@receipt",
            "payment"=>"UPDATE \"YocoWebhookReceipts\" SET \"PaymentId\"='private-fixture-payment' WHERE \"Id\"=@receipt",
            "checkout-reference"=>"UPDATE \"YocoWebhookReceipts\" SET \"CheckoutReferenceId\"=@other WHERE \"Id\"=@receipt",
            "event-empty"=>"UPDATE \"YocoWebhookReceipts\" SET \"EventId\"='' WHERE \"Id\"=@receipt",
            "event-whitespace"=>"UPDATE \"YocoWebhookReceipts\" SET \"EventId\"=E' \\t\\r\\n' WHERE \"Id\"=@receipt",
            _=>throw new InvalidOperationException() };
        // Mutations construct negative disposable fixture inputs, never writer/clone repairs.
        await PostgreSqlFixture.Sql(db.Dsn,sql,new NpgsqlParameter("receipt",fixture.Plan.Members[0].CapturedReceiptId),new NpgsqlParameter("other",Guid.NewGuid()));
        var before=await NonTopologyDigest(db.Dsn);
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false));
        Assert.Equal("Retained joining-receipt binding differs. Predicate: "+category+".",failure.Message);
        Assert.DoesNotContain("private-fixture",failure.Message);
        await AssertNoTopology(db.Dsn); Assert.Equal(before,await NonTopologyDigest(db.Dsn));
    }

    [Fact]
    public async Task OperatorReceiptFailure_ExposesOnlyCategoryAndNoSensitiveValues()
    {
        await using var db=await fixture.NewDatabase();
        await PostgreSqlFixture.Sql(db.Dsn,"UPDATE \"YocoWebhookReceipts\" SET \"PaymentId\"='private-fixture-payment' WHERE \"Id\"=@receipt",
            new NpgsqlParameter("receipt",fixture.Plan.Members[0].CapturedReceiptId));
        var before=await NonTopologyDigest(db.Dsn);
        var result=await Command(db.Dsn,"apply",db.Dsn);
        Assert.Equal(1,result.Exit);
        using var output=JsonDocument.Parse(result.Output);
        Assert.Equal("REJECTED",output.RootElement.GetProperty("result").GetString());
        Assert.Equal("CONTRACT_CONFLICT",output.RootElement.GetProperty("category").GetString());
        Assert.Equal("Retained joining-receipt binding differs. Predicate: PaymentBinding.",output.RootElement.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null,output.RootElement.GetProperty("delta").ValueKind);
        foreach(var sensitive in new[] {"private-fixture-payment","fixture-checkout-","fixture-event-",new string('A',64),"local-disposable-only"})
            Assert.DoesNotContain(sensitive,result.Output);
        await AssertNoTopology(db.Dsn); Assert.Equal(before,await NonTopologyDigest(db.Dsn));
    }

    [Fact]
    public async Task ExistingForeignTenantScope_RejectsWithoutReplacingItsRoot()
    {
        await using var db=await fixture.NewDatabase(); var foreign=Guid.NewGuid();
        await PostgreSqlFixture.Sql(db.Dsn,"""
            INSERT INTO "Areas" ("Id","TenantId","Code","Name","IsActive","CreationTime","IsDeleted")
            VALUES ('cccccccc-0000-0000-0000-000000000002',2,'OTHER','Other Fixture',true,NOW(),false);
            INSERT INTO "Customers" ("Id","TenantId","Name","Email","AreaId","IsActive","CreationTime","ClubMemberNumber","UserId","IsDeleted")
            VALUES (999,2,'Other Fixture','other@example.test','cccccccc-0000-0000-0000-000000000002',true,NOW(),'OTHER-FIXTURE',9000,false);
            INSERT INTO "EntryParticipations" ("Id","TenantId","CustomerId","Status","StartedAt","TermsVersion","TermsEffectiveFrom","JoiningPaymentAmount","JoiningInstallmentAmount","RegistrationPaymentAmount","ActivationPaymentAmount","MonthlyCommitmentAmount","GracePeriodDays","Currency","CreationTime","IsDeleted")
            VALUES (@id,2,999,0,TIMESTAMPTZ '2026-01-01','fixture',TIMESTAMPTZ '2026-01-01',1200,0,0,0,600,7,'ZAR',NOW(),false);
            """,new NpgsqlParameter("id",foreign));
        await using(var context=fixture.Context(db.Dsn))
        {
            await using var tx=await context.Database.BeginTransactionAsync();
            var scope=AQGreenPlacementTreeScope.Create(2); scope.Id=fixture.Plan.ScopeId;
            context.AQGreenPlacementTreeScopes.Add(scope); await context.SaveChangesAsync();
            context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateRoot(scope,foreign,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion));
            await context.SaveChangesAsync(); await tx.CommitAsync();
        }
        var before=await TopologyDigest(db.Dsn);
        var rejected=await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false));
        Assert.Contains("Existing scope",rejected.Message); Assert.Equal(before,await TopologyDigest(db.Dsn));
    }

    [Fact]
    public async Task ExistingMigrationModelMatches_NoSchemaMigrationNeeded()
    {
        await using var db=await fixture.NewDatabase(); await using var context=fixture.Context(db.Dsn);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task FirstApplyExact_SecondApplyZero_DryRunNeverWrites_ProhibitedTablesUnchanged()
    {
        await using var db=await fixture.NewDatabase();
        var before=await NonTopologyDigest(db.Dsn);
        var dry=await Run(db.Dsn,true);
        Assert.Equal(new AQGreenBackfillResult(true,false,1,6),dry);
        await AssertNoTopology(db.Dsn);
        var first=await Run(db.Dsn,false);
        Assert.Equal(new AQGreenBackfillResult(false,false,1,6),first);
        await AssertExactTopology(db.Dsn);
        var topologyBeforeRetry=await TopologyDigest(db.Dsn);
        var second=await Run(db.Dsn,false);
        Assert.Equal(new AQGreenBackfillResult(false,true,0,0),second);
        Assert.Equal(new AQGreenBackfillResult(true,true,0,0),await Run(db.Dsn,true));
        Assert.Equal(topologyBeforeRetry,await TopologyDigest(db.Dsn));
        Assert.Equal(before,await NonTopologyDigest(db.Dsn));
        Assert.Equal(0L,await PostgreSqlFixture.Scalar<long>(db.Dsn,"SELECT count(*) FROM \"AQGreenRecruitmentAttributions\""));
        Assert.Equal(0L,await PostgreSqlFixture.Scalar<long>(db.Dsn,"SELECT count(*) FROM \"AQGreenRecruitmentAttributionConfirmations\""));
    }

    [Fact]
    public async Task PartialFailureAfterScopeAndRoot_FullyRollsBack()
    {
        await using var db=await fixture.NewDatabase(); var before=await NonTopologyDigest(db.Dsn);
        var fault=new FailAfterRoot();
        var target=await GetTarget(db.Dsn);
        await using var context=fixture.Context(db.Dsn,fault);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new AQGreenCustomer11BackfillWriter(context).ExecuteAsync(fixture.Plan,target,false));
        Assert.Equal(3,fault.SaveCalls); // scope and root INSERTs actually completed inside PostgreSQL before interruption
        await AssertNoTopology(db.Dsn); Assert.Equal(before,await NonTopologyDigest(db.Dsn));
        Assert.Equal(new AQGreenBackfillResult(false,false,1,6),await Run(db.Dsn,false));
    }

    [Fact]
    public async Task ConcurrentApplies_SerializeAndOnlyOneWrites()
    {
        await using var db=await fixture.NewDatabase();
        var results=await Task.WhenAll(Run(db.Dsn,false),Run(db.Dsn,false));
        Assert.Single(results.Where(r=>!r.AlreadyApplied)); Assert.Single(results.Where(r=>r.AlreadyApplied));
        Assert.Equal(1,results.Sum(r=>r.ScopeInserts)); Assert.Equal(6,results.Sum(r=>r.PlacementInserts));
        await AssertExactTopology(db.Dsn);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("participant-elsewhere")]
    [InlineData("placement")]
    [InlineData("partial")]
    [InlineData("extra")]
    public async Task ExistingConflictingState_RejectedWithoutOverwrite(string kind)
    {
        await using var db=await fixture.NewDatabase();
        await using(var context=fixture.Context(db.Dsn))
        {
            await using var tx=await context.Database.BeginTransactionAsync();
            var scope=AQGreenPlacementTreeScope.Create(1); scope.Id=kind=="scope" || kind=="participant-elsewhere"?Guid.NewGuid():fixture.Plan.ScopeId;
            context.AQGreenPlacementTreeScopes.Add(scope); await context.SaveChangesAsync();
            var root=AQGreenNetworkPlacement.CreateRoot(scope,fixture.Plan.RootParticipantId,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion);
            context.AQGreenNetworkPlacements.Add(root); await context.SaveChangesAsync();
            if(kind=="placement" || kind=="extra" || kind=="participant-elsewhere")
            {
                foreach(var member in fixture.Plan.Members.Skip(1))
                {
                    var slot=member.Slot!.Value;
                    if(kind=="placement" && slot<=2) slot=3-slot;
                    var placement=AQGreenNetworkPlacement.CreateChild(root,member.ParticipantId,slot,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion);
                    context.AQGreenNetworkPlacements.Add(placement); await context.SaveChangesAsync();
                }
            }
            await tx.CommitAsync();
        }
        if(kind=="extra")
        {
            // An unrelated extra participant in the same scope is not silently accepted as an exact retry.
            await AddExtra(db.Dsn,fixture.Plan.ScopeId);
        }
        var before=await TopologyDigest(db.Dsn);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,true));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false));
        Assert.Equal(before,await TopologyDigest(db.Dsn));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("programme")]
    [InlineData("missing")]
    [InlineData("recruiter")]
    [InlineData("timestamp")]
    [InlineData("approval")]
    [InlineData("payment")]
    [InlineData("receipt")]
    [InlineData("approving-actor")]
    [InlineData("approval-identity")]
    [InlineData("receipt-shape")]
    public async Task WrongOrChangedSource_RejectsWithoutWrites(string kind)
    {
        await using var db=await fixture.NewDatabase(); var member=fixture.Plan.Members[1];
        var sql=kind switch {
            "tenant"=>"UPDATE \"EntryParticipations\" SET \"TenantId\"=2 WHERE \"Id\"=@id",
            "programme"=>"UPDATE \"MemberPayments\" SET \"Purpose\"=2 WHERE \"Id\"=(SELECT \"JoiningPaymentId\" FROM \"EntryParticipations\" WHERE \"Id\"=@id)",
            "missing"=>"UPDATE \"EntryParticipations\" SET \"Id\"=@replacement WHERE \"Id\"=@id",
            "recruiter"=>"UPDATE \"EntryParticipations\" SET \"RecruiterCustomerId\"=NULL WHERE \"Id\"=@id",
            "timestamp"=>"UPDATE \"EntryParticipations\" SET \"StartedAt\"=\"StartedAt\"+interval '1 microsecond' WHERE \"Id\"=@id",
            "approval"=>"UPDATE \"EntryParticipationApprovalDecisions\" SET \"Approved\"=false WHERE \"EntryParticipationId\"=@id",
            "payment"=>"UPDATE \"MemberPayments\" SET \"Amount\"=1 WHERE \"Id\"=(SELECT \"JoiningPaymentId\" FROM \"EntryParticipations\" WHERE \"Id\"=@id)",
            "receipt"=>"UPDATE \"YocoWebhookReceipts\" SET \"PaymentId\"='wrong-fixture-reference' WHERE \"CheckoutReferenceId\"=(SELECT \"Id\" FROM \"AQGreenJoiningCheckouts\" WHERE \"ParticipationId\"=@id)",
            "approving-actor"=>"UPDATE \"EntryParticipationApprovalDecisions\" SET \"AdministratorUserId\"=9000 WHERE \"EntryParticipationId\"=@id",
            "approval-identity"=>"UPDATE \"EntryParticipationApprovalDecisions\" SET \"Id\"=@replacement WHERE \"EntryParticipationId\"=@id",
            "receipt-shape"=>"UPDATE \"YocoWebhookReceipts\" SET \"PayloadHash\"=repeat('z',64) WHERE \"CheckoutReferenceId\"=(SELECT \"Id\" FROM \"AQGreenJoiningCheckouts\" WHERE \"ParticipationId\"=@id)",
            _=>throw new InvalidOperationException() };
        if(kind=="missing")
        {
            // Remove dependent fixture evidence first, then substitute identity. No writer writes are involved.
            await PostgreSqlFixture.Sql(db.Dsn,"DELETE FROM \"YocoWebhookReceipts\" WHERE \"CheckoutReferenceId\" IN (SELECT \"Id\" FROM \"AQGreenJoiningCheckouts\" WHERE \"ParticipationId\"=@id); DELETE FROM \"AQGreenJoiningCheckouts\" WHERE \"ParticipationId\"=@id; DELETE FROM \"EntryParticipationApprovalDecisions\" WHERE \"EntryParticipationId\"=@id;",new NpgsqlParameter("id",member.ParticipantId));
        }
        await PostgreSqlFixture.Sql(db.Dsn,sql,new NpgsqlParameter("id",member.ParticipantId),new NpgsqlParameter("replacement",Guid.NewGuid()));
        var before=await NonTopologyDigest(db.Dsn);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false));
        await AssertNoTopology(db.Dsn); Assert.Equal(before,await NonTopologyDigest(db.Dsn));
    }

    [Theory]
    [InlineData("disabled-trigger")]
    [InlineData("missing-index")]
    [InlineData("changed-check")]
    [InlineData("changed-trigger-body")]
    [InlineData("source-rls")]
    [InlineData("changed-literal")]
    public async Task MissingOrWeakenedInvariant_Rejects(string kind)
    {
        await using var db=await fixture.NewDatabase();
        await PostgreSqlFixture.Sql(db.Dsn,kind switch {
            "disabled-trigger"=>"ALTER TABLE \"AQGreenNetworkPlacements\" DISABLE TRIGGER \"TR_AQGreenNetworkPlacements_AppendOnly\"",
            "missing-index"=>"DROP INDEX \"IX_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId_Plac~\"",
            "changed-check"=>"ALTER TABLE \"AQGreenNetworkPlacements\" DROP CONSTRAINT \"CK_AQGreenNetworkPlacements_PlacementSlot_Range\", ADD CONSTRAINT \"CK_AQGreenNetworkPlacements_PlacementSlot_Range\" CHECK (true)",
            "changed-trigger-body"=>"CREATE OR REPLACE FUNCTION \"ValidateAQGreenNetworkPlacementInsert\"() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog AS $b$ BEGIN RETURN NEW; END; $b$",
            "source-rls"=>"ALTER TABLE \"EntryParticipations\" ENABLE ROW LEVEL SECURITY",
            "changed-literal"=>"ALTER TABLE \"AQGreenNetworkPlacements\" DROP CONSTRAINT \"CK_AQGreenNetworkPlacements_RootOrNonRootShape\", ADD CONSTRAINT \"CK_AQGreenNetworkPlacements_RootOrNonRootShape\" CHECK ((\"PlacementParentParticipantId\" IS NULL AND \"PlacementSlot\" IS NULL AND \"CanonicalPath\" = ' ') OR (\"PlacementParentParticipantId\" IS NOT NULL AND \"PlacementSlot\" IS NOT NULL AND \"CanonicalPath\" <> ' '))",
            _=>throw new InvalidOperationException() });
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(db.Dsn,false)); await AssertNoTopology(db.Dsn);
    }

    [Theory]
    [InlineData("database")]
    [InlineData("user")]
    [InlineData("address")]
    [InlineData("port")]
    [InlineData("snapshot")]
    public async Task WrongOperatorTargetIdentity_Rejects(string kind)
    {
        await using var db=await fixture.NewDatabase(); var target=await GetTarget(db.Dsn);
        target=kind switch {"database"=>target with {Database="wrong"},"user"=>target with {User="wrong"},"address"=>target with {ServerAddress="192.0.2.1"},"port"=>target with {ServerPort=1},"snapshot"=>target with {SourceSnapshotSha256=new string('0',64)},_=>target};
        await using var context=fixture.Context(db.Dsn);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new AQGreenCustomer11BackfillWriter(context).ExecuteAsync(fixture.Plan,target,false));
        await AssertNoTopology(db.Dsn);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WrongManifestOrOutputHash_RejectsBeforeConnection(bool input)
    {
        var m=fixture.Input.ToArray(); var o=fixture.Output.ToArray();
        if(input) m[^2]^=1; else o[^2]^=1;
        Assert.Throws<InvalidOperationException>(()=>AQGreenCustomer11BackfillPlan.Load(m,o,fixture.Capture));
    }

    [Fact]
    public void ReplacedProtectedSnapshotBytes_RejectsBeforeConnection()
    {
        var capture=fixture.Capture.ToArray(); capture[^2]^=1;
        Assert.Throws<InvalidOperationException>(()=>AQGreenCustomer11BackfillPlan.Load(fixture.Input,fixture.Output,capture));
    }

    private async Task<(int Exit,string Output)> Command(string identityDsn,string mode,string dsn,AQGreenBackfillTargetIdentity? suppliedTarget=null)
    {
        var target=suppliedTarget??await GetTarget(identityDsn);
        var start=new ProcessStartInfo("dotnet") { RedirectStandardOutput=true,RedirectStandardError=true };
        foreach(var arg in new[] { Path.Combine(AppContext.BaseDirectory,"AqualLifeStyle.Customer11Backfill.dll"),
            "--mode",mode,"--manifest",Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_INPUT")!,
            "--output",Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_OUTPUT")!,
            "--source-capture",Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_CAPTURE")!,
            "--target-kind",target.TargetKind,"--expected-database",target.Database,"--expected-user",target.User,
            "--expected-server-address",target.ServerAddress,"--expected-server-port",target.ServerPort.ToString(),
            "--source-snapshot-sha256",target.SourceSnapshotSha256 }) start.ArgumentList.Add(arg);
        string? receiptFile=null;
        try
        {
            if(target.TargetKind=="verified-recovery-clone")
            {
                receiptFile=Path.GetTempFileName(); await File.WriteAllBytesAsync(receiptFile,target.VerifiedTargetReceipt!);
                foreach(var arg in new[] {"--expected-service-id",target.ServiceId!,"--verified-target-receipt",receiptFile,"--verified-target-reference",target.VerifiedTargetReceiptSha256!}) start.ArgumentList.Add(arg);
            }
            start.Environment["AQGREEN_CUSTOMER11_BACKFILL_DSN"]=dsn;
            using var process=Process.Start(start)!;
            var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode,(await output)+(await error));
        }
        finally { if(receiptFile!=null) File.Delete(receiptFile); }
    }

    private sealed class NullReceiptHash : IMaterializationInterceptor
    {
        public object InitializedInstance(MaterializationInterceptionData materializationData,object entity)
        {
            if(entity is YocoWebhookReceipt)
                typeof(YocoWebhookReceipt).GetProperty(nameof(YocoWebhookReceipt.PayloadHash))!.SetValue(entity,null);
            return entity;
        }
    }

    private Task<AQGreenBackfillTargetIdentity> GetTarget(string dsn)=>fixture.Target(dsn);
    private async Task<AQGreenBackfillResult> Run(string dsn,bool dry)
    {
        var target=await GetTarget(dsn); await using var context=fixture.Context(dsn);
        return await new AQGreenCustomer11BackfillWriter(context).ExecuteAsync(fixture.Plan,target,dry);
    }
    private async Task AssertExactTopology(string dsn)
    {
        await using var context=fixture.Context(dsn);
        var scopes=await context.AQGreenPlacementTreeScopes.ToListAsync(); Assert.Single(scopes); Assert.Equal(fixture.Plan.ScopeId,scopes[0].Id); Assert.Equal(1,scopes[0].TenantId);
        var rows=await context.AQGreenNetworkPlacements.ToListAsync(); Assert.Equal(6,rows.Count);
        foreach(var m in fixture.Plan.Members)
        {
            var p=Assert.Single(rows.Where(p=>p.ParticipantId==m.ParticipantId));
            Assert.Equal(fixture.Plan.PlacementId(m.ParticipantId),p.Id); Assert.Equal(fixture.Plan.ScopeId,p.PlacementTreeScopeId); Assert.Equal(1,p.TenantId);
            Assert.Equal(m.ParentId,p.PlacementParentParticipantId); Assert.Equal(m.Slot,p.PlacementSlot); Assert.Equal(m.CanonicalPath,p.CanonicalPath);
            Assert.Equal(fixture.Plan.EffectiveAtUtc,p.PlacedAt); Assert.Equal(AQGreenPlacementRules.CurrentVersion,p.RulesVersion);
        }
    }
    private static async Task AssertNoTopology(string dsn)
    {
        Assert.Equal(0L,await PostgreSqlFixture.Scalar<long>(dsn,"SELECT count(*) FROM \"AQGreenPlacementTreeScopes\""));
        Assert.Equal(0L,await PostgreSqlFixture.Scalar<long>(dsn,"SELECT count(*) FROM \"AQGreenNetworkPlacements\""));
    }
    private static async Task<string> TopologyDigest(string dsn)=>await Digest(dsn,true);
    private static async Task<string> NonTopologyDigest(string dsn)=>await Digest(dsn,false);
    private static async Task<string> Digest(string dsn,bool topology)
    {
        await using var c=new NpgsqlConnection(dsn); await c.OpenAsync();
        var tables=new List<string>();
        await using(var cmd=new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename",c))
        await using(var reader=await cmd.ExecuteReaderAsync()) while(await reader.ReadAsync()) tables.Add(reader.GetString(0));
        var values=new SortedDictionary<string,string>();
        foreach(var table in tables.Where(t=>t.StartsWith("AQGreenPlacementTreeScopes",StringComparison.Ordinal)||t=="AQGreenNetworkPlacements" ? topology:!topology))
        {
            var quoted="\""+table.Replace("\"","\"\"")+"\"";
            await using var cmd=new NpgsqlCommand($"SELECT md5(coalesce(string_agg(row_to_json(t)::text, ',' ORDER BY row_to_json(t)::text),'')) FROM public.{quoted} t",c);
            values[table]=(string)(await cmd.ExecuteScalarAsync())!;
        }
        return JsonSerializer.Serialize(values);
    }
    private async Task AddExtra(string dsn,Guid scopeId)
    {
        // Fixture-only unexpected scope row: reused legitimate fixture customer in another Tenant's participation is not used.
        // Instead add an independent Tenant-1 participant/customer and a grandchild under the first accepted child.
        var id=Guid.NewGuid();
        await PostgreSqlFixture.Sql(dsn,"""
            INSERT INTO "Customers" ("Id","TenantId","Name","Email","AreaId","IsActive","CreationTime","ClubMemberNumber","UserId","IsDeleted")
            VALUES (999,1,'Extra Fixture','extra@example.test','cccccccc-0000-0000-0000-000000000001',true,NOW(),'FIXTURE-EXTRA',9000,false);
            INSERT INTO "EntryParticipations" ("Id","TenantId","CustomerId","Status","StartedAt","TermsVersion","TermsEffectiveFrom","JoiningPaymentAmount","JoiningInstallmentAmount","RegistrationPaymentAmount","ActivationPaymentAmount","MonthlyCommitmentAmount","GracePeriodDays","Currency","CreationTime","IsDeleted")
            VALUES (@id,1,999,2,TIMESTAMPTZ '2026-01-01','fixture',TIMESTAMPTZ '2026-01-01',1200,0,0,0,600,7,'ZAR',NOW(),false);
            """,new NpgsqlParameter("id",id));
        await using var context=fixture.Context(dsn); await using var tx=await context.Database.BeginTransactionAsync();
        var first=fixture.Plan.Members[1];
        var parent=await context.AQGreenNetworkPlacements.SingleAsync(p=>p.ParticipantId==first.ParticipantId);
        context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateChild(parent,id,1,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion));
        await context.SaveChangesAsync(); await tx.CommitAsync();
    }
    private sealed class FailAfterRoot : SaveChangesInterceptor
    {
        public int SaveCalls { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)
        {
            if(++SaveCalls==3) throw new InvalidOperationException("Fixture interruption after scope/root database writes");
            return new(result);
        }
    }
}
