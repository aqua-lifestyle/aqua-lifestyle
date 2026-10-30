using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [CollectionDefinition("AQGreen clean-start PostgreSQL", DisableParallelization = true)]
    public sealed class AQGreenCleanStartPostgreSqlCollection : ICollectionFixture<AQGreenCleanStartPostgreSqlFixture> { }
    public sealed class AQGreenCleanStartPostgreSqlFixture : IAsyncLifetime
    {
        public readonly AQGreenPlacementAllocatorPostgreSqlFixture Database = new("postgres:18.4-alpine");
        public Task InitializeAsync() => Database.InitializeAsync();
        public Task DisposeAsync() => Database.DisposeAsync();
    }

    [Collection("AQGreen clean-start PostgreSQL")]
    public sealed partial class AQGreenCleanStartPostgreSqlTests
    {
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;
        private static readonly DateTime Boundary = AqualLifeStyle.Tests.WebHost.AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(14);
        private const string ResetId="SYNTHETIC-RESET-ONLY";
        private static readonly string Hash=AQGreenB6CanonicalEvidence.Hash(System.Text.Encoding.UTF8.GetBytes("synthetic-authority-not-production"));
        public AQGreenCleanStartPostgreSqlTests(AQGreenCleanStartPostgreSqlFixture fixture) => _fixture=fixture.Database;
        private static string Operations
        {
            get
            {
                for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
                {
                    var candidate=Path.Combine(d.FullName,"src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart");
                    if(File.Exists(Path.Combine(candidate,"reset.sql"))) return candidate;
                }
                throw new InvalidOperationException("Clean-start operator SQL unavailable.");
            }
        }
        private async Task PrepareAsync(NpgsqlConnection connection)
        {
            await using var context=_fixture.CreateDbContext(connection);
            await context.GetService<IMigrator>().MigrateAsync("20261005194901_AddIsolatedAQGreenPilotEvidence");
        }
        private static async Task<object> SqlAsync(NpgsqlConnection c,string sql)
        { await using var command=new NpgsqlCommand(sql,c); return await command.ExecuteScalarAsync(); }
        private static async Task<AQGreenCleanStartState> StateAsync(NpgsqlConnection c,DateTime? boundaryUtc=null)
        {
            await using var tx=await c.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            await using(var command=new NpgsqlCommand("SET TRANSACTION READ ONLY; SELECT set_config('aqua.clean_start.tenant','1',true),"+
                "set_config('aqua.clean_start.boundary',@boundary,true),set_config('aqua.clean_start.reset_id',@reset,true)",c,tx))
            { command.Parameters.AddWithValue("boundary",(boundaryUtc??Boundary).ToString("O"));command.Parameters.AddWithValue("reset",ResetId);await command.ExecuteNonQueryAsync(); }
            await using var read=new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(Operations,"state.sql")),c,tx);
            var state=JsonSerializer.Deserialize<AQGreenCleanStartState>((string)await read.ExecuteScalarAsync(),AQGreenB6CanonicalEvidence.Options);
            await tx.RollbackAsync();return state;
        }
        private static async Task<(int Code,string Output)> ResetAsync(NpgsqlConnection c,AQGreenCleanStartState state,string owner=null,
            string operation="reset.sql",bool ownerAttested=false,string attestation=null,
            DateTime? boundaryUtc=null,string boundaryPolicy=null,string boundaryAuthorization=null)
        {
            var identity=(string)await SqlAsync(c,"SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||"+
                "(SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') FROM pg_control_system()");
            var info=new NpgsqlConnectionStringBuilder(c.ConnectionString);
            // Only the randomly generated loopback fixture is eligible for this helper.
            (info.Host=="localhost"||info.Host=="127.0.0.1").ShouldBeTrue();info.Database.ShouldStartWith("aqgreen_allocator_test_");
            var start=new ProcessStartInfo("psql") { RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
            foreach(var key in new[]{"PGHOSTADDR","PGSERVICE","PGSERVICEFILE","PGOPTIONS","PGSSLMODE","PGSSLROOTCERT"})
                start.Environment.Remove(key);
            start.Environment["PGHOST"]=info.Host;start.Environment["PGPORT"]=info.Port.ToString();
            start.Environment["PGDATABASE"]=info.Database;start.Environment["PGUSER"]=info.Username;
            // Npgsql strips the password from an opened connection string. This
            // is the fixed synthetic fixture password, never a production secret.
            start.Environment["PGPASSWORD"]="aqualifestyle";start.Environment["PSQL_HISTORY"]="/dev/null";
            foreach(var arg in new[]{"-X","-qAt","--no-password","--set=ON_ERROR_STOP=1","--file="+Path.Combine(Operations,operation)}) start.ArgumentList.Add(arg);
            foreach(var pair in new[]{("boundary_utc",(boundaryUtc??Boundary).ToString("O")),("reset_id",ResetId),
                ("expected_source_digest",state.SourceDigest),("expected_database_identity_digest",identity),("source_database",info.Database),
                ("classification_digest",Hash),("independent_acceptance_digest",Hash),("owner_reset_authorization_digest",owner??Hash),
                ("recovery_evidence_digest",Hash),("reset_sql_digest",Hash),("no_genuine_memberships","true"),("write_fence_evidence_digest",Hash),
                ("owner_undo_authorization_digest",Hash),("expected_archive_digest",state.ArchiveDigest),
                ("payment_admission_mode",ownerAttested?AQGreenCleanStartEvidence.OwnerAttestedTestOnly:AQGreenCleanStartEvidence.ZeroPaymentExposure),
                ("owner_test_only_attestation_digest",ownerAttested?attestation??Hash:""),
                ("valid_from_utc",DateTime.UtcNow.AddMinutes(-5).ToString("O")),("valid_until_utc",DateTime.UtcNow.AddMinutes(5).ToString("O"))})
                start.ArgumentList.Add("--set="+pair.Item1+"="+pair.Item2);
            if(boundaryPolicy!=null) start.ArgumentList.Add("--set=boundary_policy="+boundaryPolicy);
            if(boundaryAuthorization!=null) start.ArgumentList.Add("--set=boundary_authorization_digest="+boundaryAuthorization);
            using var process=Process.Start(start);var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();return(process.ExitCode,await stdout+await stderr);
        }
        private static async Task<string> ProtectedDigestAsync(NpgsqlConnection c) => (string)await SqlAsync(c,"""
            SELECT md5(jsonb_build_object(
              'users',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AbpUsers" t),
              'customerFields',(SELECT jsonb_agg(to_jsonb(t)-'MembershipId' ORDER BY "Id") FROM "Customers" t),
              'otherCustomerMemberships',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "Customers" t WHERE "TenantId"<>1),
              'payments',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "MemberPayments" t),
              'receipts',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "YocoWebhookReceipts" t),
              'joining',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AQGreenJoiningCheckouts" t),
              'monthly',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AQGreenMonthlyObligationCheckouts" t),
              'otherTenant',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "EntryParticipations" t WHERE "TenantId"=2)
            )::text)
            """);

        [Fact]
        public async Task ProspectiveRetirementRequiresOwnerBoundaryAuthorityPreservesMoneyAndBindsNativeD10()
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();
            await PrepareAsync(c);await SeedMembershipAssignmentsAsync(c);await SeedRetainedPaymentGraphAsync(c);
            var boundary=Boundary.AddMinutes(17);var before=await StateAsync(c,boundary);var protectedBefore=await ProtectedDigestAsync(c);
            var implicitMode=await ResetAsync(c,before,ownerAttested:true,boundaryUtc:boundary);
            implicitMode.Code.ShouldNotBe(0);implicitMode.Output.ShouldContain("CLEAN_START_BOUNDARY_OR_POPULATION_CHANGED");
            foreach(var authority in new[]{"",new string('0',64),"not-owner-authority"})
            {
                var denied=await ResetAsync(c,before,ownerAttested:true,boundaryUtc:boundary,
                    boundaryPolicy:AQGreenCleanStartEvidence.ProspectiveBoundary,boundaryAuthorization:authority);
                denied.Code.ShouldNotBe(0);denied.Output.ShouldContain("CLEAN_START_EXPLICIT_BOUNDARY_AUTHORITY_REQUIRED");
                (await ProtectedDigestAsync(c)).ShouldBe(protectedBefore);(await StateAsync(c,boundary)).SourceDigest.ShouldBe(before.SourceDigest);
            }
            var past=await ResetAsync(c,before,ownerAttested:true,boundaryUtc:DateTime.UtcNow.AddMinutes(-1),
                boundaryPolicy:AQGreenCleanStartEvidence.ProspectiveBoundary,boundaryAuthorization:Hash);
            past.Code.ShouldNotBe(0);past.Output.ShouldContain("CLEAN_START_BOUNDARY_OR_POPULATION_CHANGED");
            var unknown=await ResetAsync(c,before,ownerAttested:true,boundaryUtc:boundary,
                boundaryPolicy:"UNKNOWN_BOUNDARY",boundaryAuthorization:Hash);
            unknown.Code.ShouldNotBe(0);unknown.Output.ShouldContain("CLEAN_START_EXPLICIT_BOUNDARY_AUTHORITY_REQUIRED");
            var reset=await ResetAsync(c,before,ownerAttested:true,boundaryUtc:boundary,
                boundaryPolicy:AQGreenCleanStartEvidence.ProspectiveBoundary,boundaryAuthorization:Hash);
            reset.Code.ShouldBe(0,reset.Output);var after=await StateAsync(c,boundary);
            after.BoundaryPolicy.ShouldBe(AQGreenCleanStartEvidence.ProspectiveBoundary);after.BoundaryAuthorizationDigest.ShouldBe(Hash);
            after.BoundaryUtc.Value.UtcDateTime.ShouldBe(boundary);after.ArchivedCount.ShouldBe(after.PopulationCount);
            after.PaymentEvidenceDigest.ShouldBe(before.PaymentEvidenceDigest);after.TenantFinancialRecordsDigest.ShouldBe(before.TenantFinancialRecordsDigest);
            (await ProtectedDigestAsync(c)).ShouldBe(protectedBefore);
            var (x,e)=AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.Create();
            AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.OwnerAttest(e);
            e.ContractVersion="AQGreenCleanStartV2";e.BoundaryPolicy=after.BoundaryPolicy;e.BoundaryAuthorizationDigest=Hash;
            e.State=after;e.ResetAtUtc=after.ResetAtUtc.Value.UtcDateTime;e.OwnerTestOnlyAttestationDigest=Hash;
            e.HistoricalFinancialRecordsDigest=after.ResetTenantFinancialRecordsDigest;
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=boundary;
            x.Decision.AuthorizedAtUtc=x.Authority.AuthorizedAtUtc=DateTime.UtcNow;
            x.Authority.ExpiresAtUtc=boundary.AddDays(1);x.Authority.FinancialBoundaryEvidenceDigest=Hash;
            AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.Sign(x,e);
            var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,e);
            bound.FinancialWorkersCommissioned.ShouldBeFalse();bound.CleanStartEvidence.ProviderModeVerification.ShouldBe("NOT_INDEPENDENTLY_VERIFIED");
            var restore=await ResetAsync(c,after,operation:"restore.sql",boundaryUtc:boundary);
            restore.Code.ShouldBe(0,restore.Output);(await ProtectedDigestAsync(c)).ShouldBe(protectedBefore);
        }

        private async Task SeedMembershipAssignmentsAsync(NpgsqlConnection c)
        {
            await using var context=_fixture.CreateDbContext(c);
            var test=AqualLifeStyle.Domain.Memberships.Membership.Create(1,"Synthetic AQGreen catalog","Test fixture",AqualLifeStyle.Domain.Enums.MembershipType.AQGreen);
            var other=AqualLifeStyle.Domain.Memberships.Membership.Create(2,"Synthetic other programme","Protected fixture",AqualLifeStyle.Domain.Enums.MembershipType.Onyx);
            context.Memberships.AddRange(test,other);await context.SaveChangesAsync();
            await SqlAsync(c,$"UPDATE \"Customers\" SET \"MembershipId\"={test.Id} WHERE \"Id\"=1 AND \"TenantId\"=1");
            await SqlAsync(c,$"UPDATE \"Customers\" SET \"MembershipId\"={other.Id} WHERE \"Id\"=101 AND \"TenantId\"=2");
        }

        [Fact]
        public async Task ClassifiedArchivePreservesProtectedRecordsAndMigration55AppliesOnlyOnce()
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();
            await PrepareAsync(c);await SeedMembershipAssignmentsAsync(c);var protectedBefore=await ProtectedDigestAsync(c);var state=await StateAsync(c);
            state.PopulationCount.ShouldBe(64);state.HasNoProtectedExposure().ShouldBeTrue();
            var result=await ResetAsync(c,state);result.Code.ShouldBe(0,result.Output);
            var after=await StateAsync(c);after.ArchivedCount.ShouldBe(64);after.PlacementCount.ShouldBe(0);
            after.ResetReceiptDigest.ShouldNotBeNull();after.HasNoProtectedExposure().ShouldBeTrue();
            // Couple the operator SQL's real receipt to the domain calendar and
            // admission, instead of mirroring its date predicate in this test.
            AQGreenCommissionWeek.FromStartUtc(after.BoundaryUtc.Value.UtcDateTime).StartUtc.ShouldBe(Boundary);
            var (admission,clean)=AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.Create();
            admission.Decision.V2EffectiveAtUtc=admission.Authority.V2EffectiveAtUtc=Boundary;
            admission.Decision.AuthorizedAtUtc=admission.Authority.AuthorizedAtUtc=DateTime.UtcNow;
            admission.Authority.ExpiresAtUtc=Boundary.AddDays(1);
            clean.ResetAtUtc=after.ResetAtUtc.Value.UtcDateTime;clean.State=after;
            AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.Sign(admission,clean);
            AQGreenD10Commissioning.BindCleanStartVerified(admission.Decision,admission.Authority,clean).Decision.HistoricalMembers.ShouldBeEmpty();
            (await ProtectedDigestAsync(c)).ShouldBe(protectedBefore);
            var replay=await ResetAsync(c,after);replay.Code.ShouldNotBe(0);replay.Output.ShouldContain("CLEAN_START_RESET_REPLAY");
            // Execute the retained reviewed SQL bytes, not a regenerated script.
            var migration=await File.ReadAllTextAsync(Path.Combine(Operations,"migration-55.reviewed.sql"));
            await SqlAsync(c,migration);
            await Should.ThrowAsync<PostgresException>(()=>SqlAsync(c,migration));await SqlAsync(c,"ROLLBACK");
            await using var context=_fixture.CreateDbContext(c);await context.GetService<IMigrator>().MigrateAsync();
            context.Database.HasPendingModelChanges().ShouldBeFalse(); // Uses pinned EF 8.0.8, not a global CLI.
            Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"__EFMigrationsHistory\"")).ShouldBe(55);
            Convert.ToInt64(await SqlAsync(c,"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='20261021051549_AddAQGreenSalesEvidenceInvalidation'")).ShouldBe(1);
            ((bool)await SqlAsync(c,"SELECT to_regclass('public.\"AQGreenSalesEvidenceInvalidations\"') IS NOT NULL")).ShouldBeTrue();
        }

        [Fact]
        public async Task ActualPaymentExposureAbortsWithoutArchivingOrChangingProtectedRows()
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            await SqlAsync(c,"""
              INSERT INTO "MemberPayments"("Id","TenantId","CustomerId","Purpose","Amount","Currency","Provider","ExternalReference",
                "Status","InitiatedAt","CreationTime","IsDeleted") VALUES
                ('f0000000-0000-0000-0000-000000000001',1,1,7,1200,'ZAR','YOCO','synthetic-payment-only',1,now(),now(),false)
              """);
            var before=await ProtectedDigestAsync(c);var state=await StateAsync(c);state.ProtectedPaymentCount.ShouldBe(1);
            var result=await ResetAsync(c,state);result.Code.ShouldNotBe(0);result.Output.ShouldContain("CLEAN_START_PROTECTED_OR_UNCLASSIFIED_EXPOSURE");
            (await StateAsync(c)).ArchivedCount.ShouldBe(0);(await ProtectedDigestAsync(c)).ShouldBe(before);
        }

        private static async Task SeedRetainedPaymentGraphAsync(NpgsqlConnection c)
        {
            await SqlAsync(c,"""
              INSERT INTO "MemberPayments"("Id","TenantId","CustomerId","Purpose","Amount","Currency","Provider","ExternalReference",
                "Status","InitiatedAt","CreationTime","IsDeleted") VALUES
                ('f0000000-0000-0000-0000-000000000002',1,1,7,1200,'ZAR','YOCO','SYNTHETIC-OWNER-ATTESTED-PAYMENT',1,now()-interval '1 hour',now(),false);
              UPDATE "EntryParticipations" SET "JoiningPaymentId"='f0000000-0000-0000-0000-000000000002' WHERE "TenantId"=1 AND "CustomerId"=1;
              """);
            await using var context=new AqualLifeStyle.EntityFrameworkCore.AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyle.EntityFrameworkCore.AqualLifeStyleDbContext>().UseNpgsql(c).Options);
            var p=await context.EntryParticipations.SingleAsync(p=>p.TenantId==1 && p.CustomerId==1);
            var checkout=AqualLifeStyle.Domain.Payments.AQGreenJoiningCheckout.Create(1,p.Id,1,
                AqualLifeStyle.Domain.Onyx.AQGreenJoiningPaymentSchedule.Full,AqualLifeStyle.Domain.Onyx.AQGreenJoiningPaymentStage.Full,1200,"ZAR",DateTime.UtcNow.AddHours(-2));
            checkout.RecordCheckout("SYNTHETIC-OWNER-CHECKOUT","https://payments.example.test/owner-checkout",DateTime.UtcNow.AddHours(-1));
            checkout.Complete(Guid.Parse("f0000000-0000-0000-0000-000000000002"),DateTime.UtcNow);
            context.AQGreenJoiningCheckouts.Add(checkout);
            context.YocoWebhookReceipts.Add(AqualLifeStyle.Domain.Payments.YocoWebhookReceipt.Record(1,"SYNTHETIC-OWNER-EVENT",
                "SYNTHETIC-OWNER-ATTESTED-PAYMENT",checkout.ProviderCheckoutId,Hash,AqualLifeStyle.Domain.Payments.YocoCheckoutProgramme.AQGreen,checkout.Id,DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task OwnerAttestedRetirementPreservesAllPaymentRelationshipsAndBindsActualReceipt()
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();
            await PrepareAsync(c);await SeedMembershipAssignmentsAsync(c);await SeedRetainedPaymentGraphAsync(c);
            var before=await StateAsync(c);before.ProtectedPaymentCount.ShouldBe(4);var protectedBefore=await ProtectedDigestAsync(c);
            var result=await ResetAsync(c,before,ownerAttested:true);result.Code.ShouldBe(0,result.Output);
            var after=await StateAsync(c);after.ProtectedPaymentCount.ShouldBe(4);after.ArchivedCount.ShouldBe(after.PopulationCount);
            after.PaymentEvidenceDigest.ShouldBe(before.PaymentEvidenceDigest);after.TenantFinancialRecordsDigest.ShouldBe(before.TenantFinancialRecordsDigest);
            (await ProtectedDigestAsync(c)).ShouldBe(protectedBefore);after.OwnerTestOnlyAttestationDigest.ShouldBe(Hash);
            var (x,e)=AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.Create();
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=Boundary;
            x.Decision.AuthorizedAtUtc=x.Authority.AuthorizedAtUtc=DateTime.UtcNow;x.Authority.ExpiresAtUtc=Boundary.AddDays(1);
            AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.OwnerAttest(e);
            e.State=after;e.ResetAtUtc=after.ResetAtUtc.Value.UtcDateTime;e.OwnerTestOnlyAttestationDigest=Hash;
            e.HistoricalFinancialRecordsDigest=after.ResetTenantFinancialRecordsDigest;
            AqualLifeStyle.Tests.WebHost.AQGreenCleanStartCommissioningTests.Sign(x,e);
            AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,e).CleanStartEvidence.State.ProtectedPaymentCount.ShouldBe(4);
            ((bool)await SqlAsync(c,"SELECT EXISTS(SELECT 1 FROM \"EntryParticipations\" p JOIN \"MemberPayments\" m ON m.\"Id\"=p.\"JoiningPaymentId\" WHERE p.\"TenantId\"=1 AND p.\"IsDeleted\" AND m.\"TenantId\"=1)")).ShouldBeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OwnerAttestedRetirementRejectsMissingAuthorityAndFinancialMutationAndRecoveryDrift(bool participationMutation)
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();
            await PrepareAsync(c);await SeedRetainedPaymentGraphAsync(c);var before=await StateAsync(c);var protectedBefore=await ProtectedDigestAsync(c);
            var missing=await ResetAsync(c,before,ownerAttested:true,attestation:"");missing.Code.ShouldNotBe(0);missing.Output.ShouldContain("CLEAN_START_EXPLICIT_PAYMENT_CLASSIFICATION_REQUIRED");
            await SqlAsync(c,"""
              CREATE FUNCTION synthetic_financial_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
              BEGIN
              """+"\n"+(participationMutation?"NEW.\"GracePeriodDays\":=8;":"UPDATE \"MemberPayments\" SET \"ExternalReference\"='SYNTHETIC-UNAUTHORIZED-MUTATION' WHERE \"TenantId\"=1;")+"\n"+"""
               RETURN NEW; END $$;
              CREATE TRIGGER synthetic_financial_mutation BEFORE UPDATE ON "EntryParticipations" FOR EACH ROW EXECUTE FUNCTION synthetic_financial_mutation();
              """);
            var failed=await ResetAsync(c,before,ownerAttested:true);failed.Code.ShouldNotBe(0);failed.Output.ShouldContain("CLEAN_START_PROTECTED_FINANCIAL_OR_AUDIT_RECORD_CHANGED");
            (await ProtectedDigestAsync(c)).ShouldBe(protectedBefore);(await StateAsync(c)).ArchivedCount.ShouldBe(0);
            (await StateAsync(c)).SourceDigest.ShouldBe(before.SourceDigest);
            await SqlAsync(c,"DROP TRIGGER synthetic_financial_mutation ON \"EntryParticipations\";");
            var reset=await ResetAsync(c,before,ownerAttested:true);reset.Code.ShouldBe(0,reset.Output);var archived=await StateAsync(c);
            await SqlAsync(c,"UPDATE \"MemberPayments\" SET \"ExternalReference\"='SYNTHETIC-LATE-FINANCIAL-FACT' WHERE \"TenantId\"=1");
            var recovery=await ResetAsync(c,archived,operation:"restore.sql");recovery.Code.ShouldNotBe(0);recovery.Output.ShouldContain("CLEAN_START_RECOVERY_FINANCIAL_STATE_CHANGED");
            (await StateAsync(c)).ArchivedCount.ShouldBe(archived.ArchivedCount);
        }

        [Fact]
        public async Task ImmutablePlacementHistoryIsRejectedAndNeverDeleted()
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            await using(var context=_fixture.CreateDbContext(c))
            {
                var scope=AQGreenPlacementTreeScope.Create(1);
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateRoot(scope,
                    AQGreenPlacementAllocatorPostgreSqlFixture.Participant(1,1),DateTime.UtcNow.AddDays(-1),AQGreenPlacementRules.CurrentVersion));
                await context.SaveChangesAsync();
            }
            var state=await StateAsync(c);state.PlacementCount.ShouldBe(1);
            var result=await ResetAsync(c,state);result.Code.ShouldNotBe(0);result.Output.ShouldContain("CLEAN_START_IMMUTABLE_V2_PLACEMENT_EXISTS");
            (await StateAsync(c)).SourceDigest.ShouldBe(state.SourceDigest);
        }

        [Fact]
        public async Task ServerRetainedJournalRestoresExactOriginalStateWithoutASeparateResource()
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            await SeedMembershipAssignmentsAsync(c);
            var before=await StateAsync(c);before.LegacyMembershipCount.ShouldBe(1);
            var reset=await ResetAsync(c,before);reset.Code.ShouldBe(0,reset.Output);
            var after=await StateAsync(c);var restore=await ResetAsync(c,after,operation:"restore.sql");restore.Code.ShouldBe(0,restore.Output);
            (await StateAsync(c)).SourceDigest.ShouldBe(before.SourceDigest);
            var replay=await ResetAsync(c,after,operation:"restore.sql");replay.Code.ShouldNotBe(0);
        }

        [Theory]
        [InlineData("missing-authorization")][InlineData("source-drift")][InlineData("customer-drift")][InlineData("late-failure")]
        public async Task MissingApprovalSourceDriftAndLateFailureCannotPartiallyReset(string failure)
        {
            await using var lease=await _fixture.CreateDatabaseAsync();await using var c=new NpgsqlConnection(lease.ConnectionString());await c.OpenAsync();await PrepareAsync(c);
            var state=await StateAsync(c);var before=await ProtectedDigestAsync(c);
            if(failure=="source-drift") await SqlAsync(c,"UPDATE \"EntryParticipations\" SET \"GracePeriodDays\"=8 WHERE \"TenantId\"=1");
            if(failure=="customer-drift") await SqlAsync(c,"UPDATE \"Customers\" SET \"Name\"='Synthetic changed identity' WHERE \"TenantId\"=1 AND \"Id\"=1");
            // Drift is an independently committed change; the reset must preserve it.
            if(failure=="customer-drift") before=await ProtectedDigestAsync(c);
            if(failure=="late-failure") await SqlAsync(c,"""
              CREATE FUNCTION synthetic_reject_reset() RETURNS trigger LANGUAGE plpgsql AS $$
              BEGIN RAISE EXCEPTION 'SYNTHETIC_LATE_FAILURE'; END $$;
              CREATE TRIGGER synthetic_reject_reset BEFORE INSERT ON "AbpAuditLogs" FOR EACH ROW EXECUTE FUNCTION synthetic_reject_reset()
              """);
            var result=await ResetAsync(c,state,failure=="missing-authorization"?"":null);result.Code.ShouldNotBe(0);
            result.Output.ShouldContain(failure=="source-drift"||failure=="customer-drift"?"CLEAN_START_APPROVED_DATASET_CHANGED":
                failure=="late-failure"?"SYNTHETIC_LATE_FAILURE":"CLEAN_START_AUTHORITY_REQUIRED");
            (await StateAsync(c)).ArchivedCount.ShouldBe(0);(await ProtectedDigestAsync(c)).ShouldBe(before);
        }
    }
}
