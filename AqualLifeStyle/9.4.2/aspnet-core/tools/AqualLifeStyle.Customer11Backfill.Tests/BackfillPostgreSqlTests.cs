using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace AqualLifeStyle.Customer11Backfill.Tests;

[Collection("Customer11 PostgreSQL")]
public sealed class BackfillPostgreSqlTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task OperatorCommand_DryRun_Apply_Retry_AndProductionEndpointRefusal()
    {
        await using var db=await fixture.NewDatabase();
        var target=await GetTarget(db.Dsn);
        async Task<(int Exit, string Output)> Command(string mode,string dsn)
        {
            var start=new ProcessStartInfo("dotnet") { RedirectStandardOutput=true,RedirectStandardError=true };
            foreach(var arg in new[] { Path.Combine(AppContext.BaseDirectory,"AqualLifeStyle.Customer11Backfill.dll"),
                "--mode",mode,"--manifest",Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_INPUT")!,
                "--output",Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_OUTPUT")!,
                "--source-capture",Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_CAPTURE")!,
                "--target-kind","local-fixture","--expected-database",target.Database,"--expected-user",target.User,
                "--expected-server-address",target.ServerAddress,"--expected-server-port",target.ServerPort.ToString(),
                "--source-snapshot-sha256",target.SourceSnapshotSha256 }) start.ArgumentList.Add(arg);
            start.Environment["AQGREEN_CUSTOMER11_BACKFILL_DSN"]=dsn;
            using var process=Process.Start(start)!;
            var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode,(await output)+(await error));
        }
        var dry=await Command("dry-run",db.Dsn); Assert.Equal(0,dry.Exit);
        Assert.Contains("\"ScopeInserts\": 1",dry.Output); Assert.Contains("\"PlacementInserts\": 6",dry.Output);
        await AssertNoTopology(db.Dsn);
        var apply=await Command("apply",db.Dsn); Assert.Equal(0,apply.Exit); await AssertExactTopology(db.Dsn);
        var retry=await Command("apply",db.Dsn); Assert.Equal(0,retry.Exit); Assert.Contains("\"AlreadyApplied\": true",retry.Output);
        var production=new NpgsqlConnectionStringBuilder(db.Dsn) { Host=fixture.Plan.SourceServiceId+".example.invalid" };
        var blocked=await Command("apply",production.ConnectionString); Assert.Equal(1,blocked.Exit);
        Assert.Contains("loopback endpoint",blocked.Output); Assert.DoesNotContain("local-disposable-only",blocked.Output);
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
    [InlineData("placement")]
    [InlineData("partial")]
    [InlineData("extra")]
    public async Task ExistingConflictingState_RejectedWithoutOverwrite(string kind)
    {
        await using var db=await fixture.NewDatabase();
        await using(var context=fixture.Context(db.Dsn))
        {
            await using var tx=await context.Database.BeginTransactionAsync();
            var scope=AQGreenPlacementTreeScope.Create(1); scope.Id=kind=="scope"?Guid.NewGuid():fixture.Plan.ScopeId;
            context.AQGreenPlacementTreeScopes.Add(scope); await context.SaveChangesAsync();
            var root=AQGreenNetworkPlacement.CreateRoot(scope,fixture.Plan.RootParticipantId,fixture.Plan.EffectiveAtUtc,AQGreenPlacementRules.CurrentVersion);
            context.AQGreenNetworkPlacements.Add(root); await context.SaveChangesAsync();
            if(kind=="placement" || kind=="extra")
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
