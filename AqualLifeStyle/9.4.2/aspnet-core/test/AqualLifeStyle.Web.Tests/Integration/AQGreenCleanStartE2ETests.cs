using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Domain.Uow;
using Abp.Configuration;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
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
            var (x,clean)=AQGreenCleanStartCommissioningTests.Create();var config=AQGreenD10SyntheticEvidence.Configuration();
            AQGreenCleanStartCommissioningTests.OwnerAttest(clean);
            AQGreenCleanStartCommissioningTests.PostReset(x,clean);
            var resetBoundary=x.Decision.CleanStartResetBoundaryUtc.Value;
            config["App:FinancialOperations:Enabled"]="true";
            config["App:BackgroundJobs:Enabled"]="false";
            config["App:AutomaticPayouts:Enabled"]="false";
            config["Bird:Enabled"]="true";
            config["Yoco:Mode"]="live";
            x.Decision.RequiredConfigurationDigest=AQGreenD10Commissioning.ConfigurationDigest(config);
            x.Decision.CleanStartRootCustomerId=null;
            x.Decision.CleanStartRootSelectionPolicy=AQGreenD10Decision.HostSelectRootAfterRegistration;
            x.Decision.AuthorizedAreaIds=new[]{area};
            var historical=await SeedLeanMemberIdentityAsync(area,"owner-attested-retired");
            var oldPaymentId=await UsingDbContextAsync(async c=>
            {
                var started=resetBoundary.AddDays(-3);
                var terms=EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-owner-retirement",started.AddDays(-1),1200,600,7);
                var p=EntryParticipation.StartIndependently(1,historical.CustomerId,terms,started);
                var payment=MemberPayment.CreatePending(1,historical.CustomerId,MemberPaymentPurpose.AQGreenJoining,1200,"YOCO","SYNTHETIC-RETIRED-PAYMENT",started);
                payment.Confirm(started.AddMinutes(1));p.ApplyConfirmedJoiningPayment(payment);p.ApproveByAdministrator(AbpSession.UserId.Value,started.AddMinutes(2));
                p.IsDeleted=true;p.DeletionTime=clean.ResetAtUtc;
                var checkout=AQGreenJoiningCheckout.Create(1,p.Id,historical.CustomerId,AQGreenJoiningPaymentSchedule.Full,AQGreenJoiningPaymentStage.Full,1200,"ZAR",started);
                checkout.RecordCheckout("SYNTHETIC-RETIRED-CHECKOUT","https://payments.example.test/retired",started);checkout.Complete(payment.Id,started.AddMinutes(1));
                c.EntryParticipations.Add(p);c.MemberPayments.Add(payment);c.AQGreenJoiningCheckouts.Add(checkout);
                c.YocoWebhookReceipts.Add(YocoWebhookReceipt.Record(1,"SYNTHETIC-RETIRED-EVENT",payment.ExternalReference,checkout.ProviderCheckoutId,
                    AQGreenD10SyntheticEvidence.Hash("synthetic-payload"),YocoCheckoutProgramme.AQGreen,checkout.Id,started.AddMinutes(1)));
                await c.SaveChangesAsync();return payment.Id;
            });
            AQGreenCleanStartState retained;
            using(var uow=Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions{Scope=TransactionScopeOption.RequiresNew,IsTransactional=true,IsolationLevel=IsolationLevel.RepeatableRead}))
            {var reader=Resolve<IAQGreenReconciliationEvidenceSnapshot>();await reader.CaptureAsync();retained=await reader.CaptureCleanStartAsync(1,resetBoundary,clean.ResetId);await uow.CompleteAsync();}
            retained.ProtectedPaymentCount.ShouldBe(4);
            // A test-only durable marker models the committed operator receipt.
            // The actual reset SQL and conservation checks execute separately on PG18.4.
            await UsingDbContextAsync(async c=>await c.Database.ExecuteSqlRawAsync("""
                WITH undo AS (SELECT jsonb_build_object() AS value), receipt AS
                (SELECT jsonb_build_object('resetId','SYNTHETIC-CLEAN-START','undoDigest',encode(sha256(convert_to(value::text,'UTF8')),'hex'),
                  'resetAt',{0}::timestamptz,'boundary',{1}::timestamptz,
                  'boundaryPolicy','OWNER_AUTHORIZED_PROSPECTIVE_UTC_V1','boundaryAuthorizationDigest',{6}::text,
                  'paymentAdmissionMode','OWNER_ATTESTED_TEST_ONLY_V1','ownerTestOnlyAttestationDigest',{2}::text,
                  'providerModeVerification','NOT_INDEPENDENTLY_VERIFIED','paymentEvidenceDigest',{3}::text,
                  'tenantFinancialRecordsDigest',{4}::text,'protectedPaymentCount',{5}::bigint,
                  'databaseIdentity',(SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
                    (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') FROM pg_control_system())) AS value FROM undo)
                INSERT INTO "AbpAuditLogs"("TenantId","ServiceName","MethodName","Parameters","ReturnValue","ExecutionTime","ExecutionDuration")
                SELECT 1,'AQGreen.CleanStart','ArchiveDisposableTests',jsonb_build_object('resetId','SYNTHETIC-CLEAN-START',
                  'receiptDigest',encode(sha256(convert_to(receipt.value::text,'UTF8')),'hex'))::text,
                  jsonb_build_object('receipt',receipt.value,'undo',undo.value)::text,{0},0 FROM undo,receipt
                """,clean.ResetAtUtc,resetBoundary,clean.OwnerTestOnlyAttestationDigest,retained.PaymentEvidenceDigest,retained.TenantFinancialRecordsDigest,retained.ProtectedPaymentCount.Value,clean.BoundaryAuthorizationDigest));
            using(var uow=Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions{Scope=TransactionScopeOption.RequiresNew,
                IsTransactional=true,IsolationLevel=IsolationLevel.RepeatableRead}))
            {
                var reader=Resolve<IAQGreenReconciliationEvidenceSnapshot>();var boundary=await reader.CaptureAsync();
                clean.State=await reader.CaptureCleanStartAsync(1,resetBoundary,clean.ResetId);
                clean.State.PopulationCount.ShouldBe(1);clean.State.ArchivedCount.ShouldBe(1);clean.State.ProtectedPaymentCount.ShouldBe(4);
                clean.HistoricalFinancialRecordsDigest=clean.State.ResetTenantFinancialRecordsDigest;
                clean.AdmitsPreservedState(clean.State).ShouldBeTrue();
                clean.DatabaseIdentityDigest=x.Decision.RequiredDatabaseIdentityDigest=boundary.SourceDatabaseIdentityEvidence;
                clean.SchemaDigest=x.Decision.RequiredSchemaDigest=boundary.SourceSchemaVersion;
                x.Decision.RequiredAssemblyDigest=boundary.SourceAssemblyDigest;
                x.Decision.RequiredRuntimeAssemblyDigest=AQGreenD10Runtime.RuntimeAssemblyDigest(boundary.SourceAssemblyDigest);
                await uow.CompleteAsync();
            }
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            PostResetOperatorTransportTests.Install(config,x.Decision,x.Authority,clean);
            var source=new CleanStartTestSource{Current=new AqualLifeStyle.Web.Host.Startup.AQGreenD10OperatorEvidenceSource(config).Read()};
            var key=Guid.NewGuid().ToString("N");
            IocManager.IocContainer.Register(Component.For<IConfiguration>().Instance(config).Named("clean-config-"+key).IsDefault(),
                Component.For<IAQGreenD10CommissioningSource>().Instance(source).Named("clean-source-"+key).IsDefault(),
                Component.For<IAQGreenPlacementV2JoiningGate>().ImplementedBy<AQGreenD10JoiningGate>().Named("clean-joining-"+key).LifestyleTransient().IsDefault(),
                Component.For<IAQGreenPlacementV2ApprovalGate>().ImplementedBy<AQGreenD10ApprovalGate>().Named("clean-approval-"+key).LifestyleTransient().IsDefault(),
                Component.For<IAQGreenPlacementV2ProgressGate>().ImplementedBy<AQGreenD10ProgressGate>().Named("clean-progress-"+key).LifestyleTransient().IsDefault(),
                Component.For<IYocoCheckoutGateway>().Instance(new LeanMvpCheckoutGateway()).Named("clean-gateway-"+key).IsDefault());
            var runtime=Resolve<AQGreenD10Runtime>();await runtime.VerifyStartupAsync();
            var gapCustomer=await SeedLeanMemberIdentityAsync(area,"forbidden-retirement-gap");
            var gapId=await UsingDbContextAsync(async c=>
            {
                var p=EntryParticipation.StartIndependently(1,gapCustomer.CustomerId,
                    EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-gap",clean.ResetAtUtc.AddDays(-1),1200,600,7),resetBoundary.AddMinutes(1));
                p.IsDeleted=true;c.EntryParticipations.Add(p);await c.SaveChangesAsync();return p.Id;
            });
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyStartupAsync()))
                .Message.ShouldBe("D10_UNAUTHORIZED_RESET_TO_COMMISSIONING_POPULATION");
            await UsingDbContextAsync(async c=>{c.EntryParticipations.Remove(await c.EntryParticipations.IgnoreQueryFilters().SingleAsync(p=>p.Id==gapId));});
            // Root-unbound release must not adopt a populated prospective network.
            var unauthorized=await SeedLeanMemberIdentityAsync(area,"not-authorized-root");
            var unauthorizedId=await UsingDbContextAsync(async c=>
            {
                var p=EntryParticipation.StartIndependently(1,unauthorized.CustomerId,
                    EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-invalid-root",DateTime.UtcNow.AddDays(-1),1200,600,7),DateTime.UtcNow);
                c.EntryParticipations.Add(p);await c.SaveChangesAsync();return p.Id;
            });
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyStartupAsync()))
                .Message.ShouldBe("D10_CLEAN_START_AUTHORIZED_ROOT_REQUIRED");
            await UsingDbContextAsync(async c=>{c.EntryParticipations.Remove(await c.EntryParticipations.SingleAsync(p=>p.Id==unauthorizedId));});
            await runtime.VerifyStartupAsync();
            await Should.ThrowAsync<InvalidOperationException>(()=>runtime.EnsureFinancialWorkersAsync());
            LoginAsHostAdmin();
            config[YocoCutoverOptions.Prefix+"Enabled"]="true";
            config[YocoCutoverOptions.Prefix+"OperatorUserId"]=AbpSession.UserId.Value.ToString();
            var store=Resolve<IYocoCutoverStore>();
            var merchant=new SyntheticEmptyReconciliation(AbpSession.UserId.Value);
            void InstallMerchant()
            {
                var bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(merchant.Read().Item1);
                var p=YocoCutoverOptions.Prefix;
                config[p+"ReconciliationPath"]="environment:"+p+"ReconciliationJsonBase64";
                config[p+"ReconciliationJsonBase64"]=Convert.ToBase64String(bytes);
                config[p+"ReconciliationSha256"]=YocoCutoverService.Hash(bytes);
            }
            InstallMerchant();
            var cutover=new YocoCutoverService(store,null,null,new YocoCutoverReconciliationSource(config),config,
                new YocoCutoverReleaseGate(runtime));
            await cutover.SetPhaseAsync("Replay");
            var originalExpiry=x.Authority.ExpiresAtUtc;
            x.Authority.ExpiresAtUtc=DateTime.UtcNow.AddMilliseconds(-1);
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            source.Current=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,clean);
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyStartupAsync())).Message.ShouldBe("D10_AUTHORITY_EXPIRED");
            (await Should.ThrowAsync<InvalidOperationException>(()=>cutover.SetPhaseAsync("Normal"))).Message.ShouldBe("D10_AUTHORITY_EXPIRED");
            // Even a complete-looking in-flight Normal cannot waive expiry. The
            // separate read sees only Replay and the owning transaction rolls back.
            (await Should.ThrowAsync<InvalidOperationException>(()=>store.TransactAsync(async pending=>
            {
                var at=x.Authority.ExpiresAtUtc.AddTicks(-1);
                pending.Phase="Normal";pending.PhaseEvidence.Add(new(){Phase="Normal",OperatorUserId=AbpSession.UserId.Value,
                    CommittedAtUtc=new DateTimeOffset(at),AdmissionKind=YocoCutoverPhaseEvidence.EmptyProspectiveV2,
                    AdmissionEvidenceDigest=new string('a',64),D10Activation=source.Current.CreateActivationReceipt(at)});
                await runtime.VerifyStartupAsync();return true;
            }))).Message.ShouldBe("D10_AUTHORITY_EXPIRED");
            (await store.ReadCommittedControlAsync()).Phase.ShouldBe("Replay");
            x.Authority.ExpiresAtUtc=originalExpiry;AQGreenCleanStartCommissioningTests.Sign(x,clean);
            source.Current=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,clean);
            var current=source.Current;var effective=x.Decision.V2EffectiveAtUtc;
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=DateTime.UtcNow.AddHours(1);
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            source.Current=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,clean);
            (await Should.ThrowAsync<InvalidOperationException>(()=>cutover.SetPhaseAsync("Normal"))).Message.ShouldBe("D10_CUTOVER_WRITE_FENCE");
            (await cutover.StatusAsync()).Phase.ShouldBe("Replay");
            source.Current=current;
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=effective;
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            merchant.Reviewed=false;InstallMerchant();
            (await Should.ThrowAsync<InvalidOperationException>(()=>cutover.SetPhaseAsync("Normal"))).Message.ShouldBe("YOCO_RELEASE_RECONCILIATION_REQUIRED");
            (await cutover.StatusAsync()).Phase.ShouldBe("Replay");
            merchant.Reviewed=true;InstallMerchant();
            var serverRoot=config["App:ServerRootAddress"];config["App:ServerRootAddress"]="https://changed.example.test";
            (await Should.ThrowAsync<InvalidOperationException>(()=>cutover.SetPhaseAsync("Normal"))).Message.ShouldBe("D10_CONFIGURATION_MISMATCH");
            config["App:ServerRootAddress"]=serverRoot;
            // An explicitly selected invalid manifest must not fall back to the
            // empty-launch path, even though this fixture has no captured work.
            var selectedPin=config[YocoCutoverOptions.Prefix+"ReconciliationSha256"];
            config[YocoCutoverOptions.Prefix+"ReconciliationSha256"]=new string('a',64);
            (await Should.ThrowAsync<InvalidOperationException>(()=>cutover.SetPhaseAsync("Normal"))).Message.ShouldBe("YOCO_RECONCILIATION_PIN_CHANGED");
            config[YocoCutoverOptions.Prefix+"ReconciliationSha256"]=selectedPin;
            // Owner-approved first-launch scope is explicit: no selected merchant
            // manifest, atomically empty inbox and no prospective V2 work.
            config[YocoCutoverOptions.Prefix+"ReconciliationPath"]=null;
            config[YocoCutoverOptions.Prefix+"ReconciliationSha256"]=null;
            config[YocoCutoverOptions.Prefix+"ReconciliationJsonBase64"]=null;
            // A bounded test-only authorization really expires on the wall clock;
            // every remaining member/payment/approval/progress operation is later.
            x.Authority.ExpiresAtUtc=DateTime.UtcNow.AddSeconds(10);
            AQGreenCleanStartCommissioningTests.Sign(x,clean);
            PostResetOperatorTransportTests.Install(config,x.Decision,x.Authority,clean);
            source.Current=new AqualLifeStyle.Web.Host.Startup.AQGreenD10OperatorEvidenceSource(config).Read();
            await cutover.SetPhaseAsync("Normal");
            var normal=await cutover.StatusAsync();normal.Phase.ShouldBe("Normal");
            var admission=normal.PhaseEvidence.Last();admission.AdmissionKind.ShouldBe(YocoCutoverPhaseEvidence.EmptyProspectiveV2);
            admission.AdmissionEvidenceDigest.Length.ShouldBe(64);admission.ReconciliationDigest.ShouldBeNull();
            admission.D10Activation.ShouldNotBeNull();
            admission.D10Activation.ActivatedAtUtc.ShouldBeLessThan(x.Authority.ExpiresAtUtc);
            var wait=x.Authority.ExpiresAtUtc-DateTime.UtcNow;
            if(wait>TimeSpan.Zero)await Task.Delay(wait+TimeSpan.FromMilliseconds(50));
            Should.Throw<InvalidOperationException>(()=>source.Current.EnsureCurrent(DateTime.UtcNow)).Message.ShouldBe("D10_AUTHORITY_EXPIRED");
            // Rehydrate the pinned authority and a new transient runtime, exactly
            // as startup does; no cached in-memory success is carried forward.
            source.Current=new AqualLifeStyle.Web.Host.Startup.AQGreenD10OperatorEvidenceSource(config).Read();
            runtime=Resolve<AQGreenD10Runtime>();await runtime.VerifyStartupAsync();
            var rootAddress=config["App:ServerRootAddress"];config["App:ServerRootAddress"]="https://drift.example.test";
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyStartupAsync())).Message.ShouldBe("D10_CONFIGURATION_MISMATCH");
            config["App:ServerRootAddress"]=rootAddress;
            config["App:AutomaticPayouts:Enabled"]="true";
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyStartupAsync())).Message.ShouldBe("YOCO_CUTOVER_AUTOMATION_MUST_BE_HELD");
            config["App:AutomaticPayouts:Enabled"]="false";
            await runtime.VerifyStartupAsync();
            var activatedSource=source.Current;source.Current=null;
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyStartupAsync()))
                .Message.ShouldBe("D10_AUTHORITY_MISSING_FOR_COMMITTED_ACTIVATION");
            source.Current=activatedSource;
            await AqualLifeStyle.Tests.EntityFrameworkCore.D10ActivationRestartProof.VerifyAsync(
                await UsingDbContextAsync(c=>Task.FromResult(c.Database.GetConnectionString())),config);
            await using var businessLease=await store.EnterBusinessAsync();
            using var businessScope=YocoCutoverDispatch.EnterBusiness();
            // New V1 financial activity on an old shared Customer is not a change
            // to the accepted historical reset facts and must not freeze V2.
            await UsingDbContextAsync(async c=>
            {
                var now=DateTime.UtcNow;
                var v1=MemberPayment.CreatePending(1,historical.CustomerId,MemberPaymentPurpose.SavingsContribution,250,"SYNTHETIC-V1","SYNTHETIC-V1-CONTINUING",now);
                v1.Confirm(now);c.MemberPayments.Add(v1);
            });
            using(var uow=Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions{Scope=TransactionScopeOption.RequiresNew,IsTransactional=true,IsolationLevel=IsolationLevel.RepeatableRead}))
            {
                var reader=Resolve<IAQGreenReconciliationEvidenceSnapshot>();await reader.CaptureAsync();
                var afterV1=await reader.CaptureCleanStartAsync(1,resetBoundary,clean.ResetId);
                afterV1.PaymentEvidenceDigest.ShouldNotBe(clean.State.PaymentEvidenceDigest);
                await uow.CompleteAsync();
            }
            await runtime.VerifyStartupAsync();
            // This uses actual registration/verification after valid commissioning,
            // not a pre-created root Customer. Synthetic provider/source authority
            // remains test-only; this is not merchant acceptance or live delivery.
            var root=await RegisterCleanStartCustomerAsync(area,"after-launch-root");
            var other=await RegisterCleanStartCustomerAsync(area,"other-registered-customer");
            // A Tenant administrator cannot nominate or create the root.
            LoginAsDefaultTenantAdmin();
            await Should.ThrowAsync<Abp.Authorization.AbpAuthorizationException>(()=>InUnitOfWorkAsync(()=>
                Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(new CleanStartRootInput{TenantId=1,CustomerId=root.CustomerId,Reason="Synthetic Host-selected first root"})));
            LoginAsHostAdmin();var request=new CleanStartRootInput{TenantId=1,CustomerId=root.CustomerId,Reason="Synthetic Host-selected first root"};
            (await Should.ThrowAsync<InvalidOperationException>(()=>InUnitOfWorkAsync(()=>
                Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(new CleanStartRootInput{TenantId=1,Reason=request.Reason}))))
                .Message.ShouldBe("D10_CLEAN_START_ROOT_CUSTOMER_REQUIRED_OR_MISMATCHED");
            var attempts=await Task.WhenAll(new[]{root,other}.Select(candidate=>Task.Run(async ()=>
            {
                try
                {
                    var id=await InUnitOfWorkAsync(()=>Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(
                        new CleanStartRootInput{TenantId=1,CustomerId=candidate.CustomerId,Reason=request.Reason}));
                    return (Customer:candidate,Id:(Guid?)id);
                }
                catch(InvalidOperationException e)
                {
                    e.Message.ShouldBe("D10_CLEAN_START_ROOT_POPULATION_NOT_EMPTY");
                    return (Customer:candidate,Id:(Guid?)null);
                }
            })));
            var winner=attempts.Where(a=>a.Id.HasValue).ShouldHaveSingleItem();
            root=winner.Customer;var rootId=winner.Id.Value;request.CustomerId=root.CustomerId;
            (await InUnitOfWorkAsync(()=>Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(request))).ShouldBe(rootId);
            root=root with{ParticipationId=rootId};await AssertAwaitingApprovalOrPaymentAsync(rootId);
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.VerifyEmptyProspectiveLaunchAsync()))
                .Message.ShouldBe("D10_PROSPECTIVE_WORK_REQUIRES_RECONCILIATION");
            (await Should.ThrowAsync<AQGreenPlacementConflictException>(()=>ApproveAsync(rootId)))
                .Message.ShouldBe("AQGreen approval requires a fully satisfied joining obligation.");
            await ConfirmJoiningPaymentAsync(root);await AssertAwaitingApprovalAsync(rootId);
            await UsingDbContextAsync(async c=>
            {
                var cover=await c.AQGreenFuneralCoverEntitlements.SingleAsync(e=>e.CustomerId==root.CustomerId);
                cover.FuneralCoverAmount.ShouldBe(30000m);
            });
            (await Should.ThrowAsync<InvalidOperationException>(()=>InUnitOfWorkAsync(()=>
                Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(new CleanStartRootInput{TenantId=1,CustomerId=unauthorized.CustomerId,Reason="Synthetic attempted replacement"}))))
                .Message.ShouldBe("D10_CLEAN_START_ROOT_POPULATION_NOT_EMPTY");
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
            source.Current.CleanStartEvidence.State.BoundaryUtc.Value.UtcDateTime.ShouldBe(resetBoundary);
            (await Should.ThrowAsync<InvalidOperationException>(()=>runtime.ForTenantAsync(1,false,x.Decision.V2EffectiveAtUtc.AddTicks(-1))))
                .Message.ShouldBe("D10_CLEAN_START_HISTORICAL_FINANCIAL_REPLAY_HELD");
            await UsingDbContextAsync(async c=>
            {
                var old=await c.EntryParticipations.IgnoreQueryFilters().SingleAsync(p=>p.CustomerId==historical.CustomerId && p.TenantId==1);
                old.IsDeleted.ShouldBeTrue();old.JoiningPaymentId.ShouldBe(oldPaymentId);
                (await c.MemberPayments.SingleAsync(p=>p.Id==oldPaymentId)).ExternalReference.ShouldBe("SYNTHETIC-RETIRED-PAYMENT");
                (await c.YocoWebhookReceipts.CountAsync(r=>r.EventId=="SYNTHETIC-RETIRED-EVENT")).ShouldBe(1);
                (await c.AQGreenJoiningCheckouts.SingleAsync(p=>p.ProviderCheckoutId=="SYNTHETIC-RETIRED-CHECKOUT")).PaymentId.ShouldBe(oldPaymentId);
            });
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("REPRO_MARKER_DIR"),"clean-start-commissioning.ran"),
                "PostgreSQL: immutable historical reset, later owner/independent LIVE authority, future-effective Normal held, post-reset gap denied, root-unbound registration/verification, protected root, payment/cover/approval/placement, retries and retained history; no real provider or automatic payouts.");
        }

        private async Task<ParticipantFixture> RegisterCleanStartCustomerAsync(Guid area, string label)
        {
            await Resolve<ISettingManager>().ChangeSettingForTenantAsync(1,"Abp.Account.IsSelfRegistrationEnabled","true");
            var name="clean_register_"+Guid.NewGuid().ToString("N");
            using(AbpSession.Use(1,null))
                await InUnitOfWorkAsync(()=>Resolve<IAccountAppService>().Register(new RegisterInput
                {
                    Name="Synthetic",Surname="Post-launch",UserName=name,EmailAddress=name+"@example.test",
                    Password="Synthetic!101",ContactNumber="+27 72 111 2233",HomeAddress="Synthetic fixture address",AreaId=area
                }));
            var identity=await UsingDbContextAsync(async c=>await c.Customers.SingleAsync(p=>p.User.UserName==name));
            await UsingDbContextAsync(async c=>
            {
                (await c.EntryParticipations.AnyAsync(p=>p.CustomerId==identity.Id)).ShouldBeFalse();
                (await c.AQGreenNetworkPlacements.AnyAsync()).ShouldBeFalse();
                (await c.MemberPayments.AnyAsync(p=>p.CustomerId==identity.Id)).ShouldBeFalse();
                (await c.AQGreenFuneralCoverEntitlements.AnyAsync(p=>p.CustomerId==identity.Id)).ShouldBeFalse();
            });
            using(AbpSession.Use(1,null))
            {
                var manager=Resolve<UserManager>();await manager.InitializeOptionsAsync(1);
                var user=await manager.FindByIdAsync(identity.UserId.ToString());
                user.IsEmailConfirmed.ShouldBeFalse();
                using(AbpSession.Use(null,await UsingDbContextAsync(async c=>await c.Users.Where(u=>u.TenantId==null && u.UserName=="admin").Select(u=>u.Id).SingleAsync())))
                    (await Should.ThrowAsync<InvalidOperationException>(()=>InUnitOfWorkAsync(()=>
                        Resolve<IAQGreenCleanStartRootAppService>().AuthorizeRootAsync(new CleanStartRootInput
                        {TenantId=1,CustomerId=identity.Id,Reason="Synthetic unverified selection rejected"}))))
                        .Message.ShouldBe("D10_CLEAN_START_ROOT_VERIFIED_REGISTRATION_REQUIRED");
                var token=await manager.GenerateEmailConfirmationTokenAsync(user);
                await InUnitOfWorkAsync(()=>Resolve<IAccountAppService>().ConfirmEmail(new ConfirmEmailInput{TenantId=1,UserId=user.Id,Token=token}));
            }
            return new ParticipantFixture(identity.UserId,name,identity.Id,Guid.Empty,null,area,label);
        }

        private async Task AssertAwaitingApprovalOrPaymentAsync(Guid id) => await UsingDbContextAsync(async c=>
        { var p=await c.EntryParticipations.SingleAsync(p=>p.Id==id);p.Status.ShouldNotBe(AqualLifeStyle.Domain.Onyx.EntryParticipationStatus.Active);
            (await c.AQGreenNetworkPlacements.CountAsync()).ShouldBe(0);(await c.MemberPayments.CountAsync(m=>m.CustomerId==p.CustomerId)).ShouldBe(0); });
        private sealed class CleanStartTestSource : IAQGreenD10CommissioningSource
        { public AQGreenD10Commissioning Current;public AQGreenD10Commissioning Read()=>Current; }

        private sealed class SyntheticEmptyReconciliation : IYocoCutoverReconciliationSource
        {
            public bool Reviewed=true;
            private readonly YocoCutoverReconciliation _manifest;
            public SyntheticEmptyReconciliation(long userId) => _manifest=new YocoCutoverReconciliation
            {
                Contract="Aqua.YocoCutover.ReconciliationV1",Authority="OPERATOR_ATTESTED_AUTHORITATIVE_YOCO_RECORDS",
                OperatorUserId=userId,AcceptedAtUtc=DateTimeOffset.UtcNow,ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),
                AquaAcceptanceReference="SYNTHETIC-TEST-ONLY",OwnerAuthorizationReference="SYNTHETIC-TEST-ONLY",
                CapturedEventSetSha256=YocoCutoverService.EventSetHash(Array.Empty<YocoCutoverEvent>()),
                OutstandingReferencesReconciled=true,UnresolvedObligationsRemain=false,Entries=new()
            };
            public (YocoCutoverReconciliation,string) Read()
            {
                _manifest.OutstandingReferencesReconciled=Reviewed;
                return (_manifest,YocoCutoverService.Hash(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(_manifest)));
            }
        }
    }
}
