using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Tests.WebHost;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed partial class AQGreenV2ContinuousFreshNetworkE2ETests
    {
        // Synthetic independent inputs and a fake provider. This verifies the real
        // PostgreSQL/application flow, not human acceptance or Yoco delivery.
        [IsolatedPostgreSqlFact]
        public async Task CleanStart_EmptyTenantCommissions_ExplicitRootThenSixInvitedMembers_SpilloverAndReplay()
        {
            await AssertFreshPostgreSqlDatabaseAsync();LoginAsDefaultTenantAdmin();
            var area=await UsingDbContextAsync(async c=>await (from a in c.AreaAdminAssignments join r in c.Areas
                on a.AreaId equals r.Id where a.TenantId==1 && a.UserId==AbpSession.UserId && !a.RevokedAt.HasValue && r.IsActive select r.Id).FirstAsync());
            var root=await SeedLeanMemberIdentityAsync(area,"clean-start-root");
            var (x,clean)=AQGreenCleanStartCommissioningTests.Create();var config=AQGreenD10SyntheticEvidence.Configuration();
            config["App:FinancialOperations:Enabled"]="true";
            config["App:BackgroundJobs:Enabled"]="false";
            x.Decision.RequiredConfigurationDigest=AQGreenD10Commissioning.ConfigurationDigest(config);
            x.Decision.CleanStartRootCustomerId=root.CustomerId;x.Decision.AuthorizedAreaIds=new[]{area};
            // A test-only durable marker models the committed operator receipt.
            await UsingDbContextAsync(async c=>await c.Database.ExecuteSqlRawAsync("""
                WITH undo AS (SELECT jsonb_build_object() AS value), receipt AS
                (SELECT jsonb_build_object('resetId','SYNTHETIC-CLEAN-START','undoDigest',encode(sha256(convert_to(value::text,'UTF8')),'hex'),
                  'resetAt',{0}::timestamptz,'boundary',{1}::timestamptz,
                  'databaseIdentity',(SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
                    (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') FROM pg_control_system())) AS value FROM undo)
                INSERT INTO "AbpAuditLogs"("TenantId","ServiceName","MethodName","Parameters","ReturnValue","ExecutionTime","ExecutionDuration")
                SELECT 1,'AQGreen.CleanStart','ArchiveDisposableTests',jsonb_build_object('resetId','SYNTHETIC-CLEAN-START',
                  'receiptDigest',encode(sha256(convert_to(receipt.value::text,'UTF8')),'hex'))::text,
                  jsonb_build_object('receipt',receipt.value,'undo',undo.value)::text,{0},0 FROM undo,receipt
                """,clean.ResetAtUtc,x.Decision.V2EffectiveAtUtc));
            using(var uow=Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions{Scope=TransactionScopeOption.RequiresNew,
                IsTransactional=true,IsolationLevel=IsolationLevel.RepeatableRead}))
            {
                var reader=Resolve<IAQGreenReconciliationEvidenceSnapshot>();var boundary=await reader.CaptureAsync();
                clean.State=await reader.CaptureCleanStartAsync(1,x.Decision.V2EffectiveAtUtc,clean.ResetId);
                clean.State.PopulationCount.ShouldBe(0);clean.State.HasNoProtectedExposure().ShouldBeTrue();
                clean.DatabaseIdentityDigest=x.Decision.RequiredDatabaseIdentityDigest=boundary.SourceDatabaseIdentityEvidence;
                clean.SchemaDigest=x.Decision.RequiredSchemaDigest=boundary.SourceSchemaVersion;
                x.Decision.RequiredAssemblyDigest=boundary.SourceAssemblyDigest;
                x.Decision.RequiredRuntimeAssemblyDigest=AQGreenD10Runtime.RuntimeAssemblyDigest(boundary.SourceAssemblyDigest);
                await uow.CompleteAsync();
            }
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            var source=new CleanStartTestSource{Current=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,clean)};
            var key=Guid.NewGuid().ToString("N");
            IocManager.IocContainer.Register(Component.For<IConfiguration>().Instance(config).Named("clean-config-"+key).IsDefault(),
                Component.For<IAQGreenD10CommissioningSource>().Instance(source).Named("clean-source-"+key).IsDefault(),
                Component.For<IAQGreenPlacementV2JoiningGate>().ImplementedBy<AQGreenD10JoiningGate>().Named("clean-joining-"+key).LifestyleTransient().IsDefault(),
                Component.For<IAQGreenPlacementV2ApprovalGate>().ImplementedBy<AQGreenD10ApprovalGate>().Named("clean-approval-"+key).LifestyleTransient().IsDefault(),
                Component.For<IAQGreenPlacementV2ProgressGate>().ImplementedBy<AQGreenD10ProgressGate>().Named("clean-progress-"+key).LifestyleTransient().IsDefault(),
                Component.For<IYocoCheckoutGateway>().Instance(new LeanMvpCheckoutGateway()).Named("clean-gateway-"+key).IsDefault());
            var runtime=Resolve<AQGreenD10Runtime>();await runtime.VerifyStartupAsync();
            await Should.ThrowAsync<InvalidOperationException>(()=>runtime.EnsureFinancialWorkersAsync());
            // A Tenant administrator cannot nominate or create the root.
            await Should.ThrowAsync<Abp.Authorization.AbpAuthorizationException>(()=>InUnitOfWorkAsync(()=>
                Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(new CleanStartRootInput{TenantId=1,Reason="Synthetic owner-selected first root"})));
            LoginAsHostAdmin();var request=new CleanStartRootInput{TenantId=1,Reason="Synthetic owner-selected first root"};
            var rootId=await InUnitOfWorkAsync(()=>Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(request));
            (await InUnitOfWorkAsync(()=>Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(request))).ShouldBe(rootId);
            root=root with{ParticipationId=rootId};await AssertAwaitingApprovalOrPaymentAsync(rootId);
            await ConfirmJoiningPaymentAsync(root);await AssertAwaitingApprovalAsync(rootId);
            LoginAsHostAdmin();await ApproveAsync(rootId);await AssertActiveApprovalAsync(root,true);
            var children=new System.Collections.Generic.List<ParticipantFixture>();
            for(var i=0;i<6;i++)
            {
                string code;
                using(AbpSession.Use(1,root.UserId)) code=(await InUnitOfWorkAsync(()=>Resolve<IProgrammeInvitationAppService>().GetMyInvitationsAsync())).Invitations.Single().Code;
                var child=await SeedLeanMemberIdentityAsync(area,"clean-start-invite-"+i);
                using(AbpSession.Use(1,child.UserId))
                {
                    await InUnitOfWorkAsync(()=>StartLeanAsync(new StartEntryParticipationInput{InviteCode=code}));
                    var participantId=await UsingDbContextAsync(async c=>await c.EntryParticipations.Where(p=>p.TenantId==1 &&
                        p.CustomerId==child.CustomerId).Select(p=>p.Id).SingleAsync());
                    child=child with{ParticipationId=participantId,SponsorParticipationId=rootId};
                }
                await ConfirmJoiningPaymentAsync(child);await AssertAwaitingApprovalAsync(child.ParticipationId);
                LoginAsHostAdmin();await ApproveAsync(child.ParticipationId);await ApproveAsync(child.ParticipationId);
                children.Add(child);
            }
            await UsingDbContextAsync(async c=>
            {
                var rows=await c.AQGreenNetworkPlacements.Where(p=>p.TenantId==1).ToListAsync();rows.Count.ShouldBe(7);
                rows.Count(p=>p.PlacementParentParticipantId==rootId).ShouldBe(5);
                rows.Single(p=>p.ParticipantId==children[5].ParticipationId).PlacementParentParticipantId.ShouldBe(children[0].ParticipationId);
                (await c.EntryWeeklyCommissions.CountAsync()).ShouldBe(0);
            });
            await runtime.VerifyStartupAsync();source.Current.FinancialWorkersCommissioned.ShouldBeFalse();
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("REPRO_MARKER_DIR"),"clean-start-commissioning.ran"),
                "PostgreSQL: empty CLEAN_START, protected root authority, ordinary payment/approval, six invitations, spillover, retries; no real provider or automatic payouts.");
        }

        private async Task AssertAwaitingApprovalOrPaymentAsync(Guid id) => await UsingDbContextAsync(async c=>
        { var p=await c.EntryParticipations.SingleAsync(p=>p.Id==id);p.Status.ShouldNotBe(AqualLifeStyle.Domain.Onyx.EntryParticipationStatus.Active);
            (await c.AQGreenNetworkPlacements.CountAsync()).ShouldBe(0);(await c.MemberPayments.CountAsync()).ShouldBe(0); });
        private sealed class CleanStartTestSource : IAQGreenD10CommissioningSource
        { public AQGreenD10Commissioning Current;public AQGreenD10Commissioning Read()=>Current; }
    }
}
