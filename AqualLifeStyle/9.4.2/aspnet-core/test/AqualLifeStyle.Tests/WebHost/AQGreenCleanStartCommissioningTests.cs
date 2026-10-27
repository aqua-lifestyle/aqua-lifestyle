using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.WebHost
{
    public sealed class AQGreenCleanStartCommissioningTests
    {
        public static (AQGreenD10SyntheticEvidence Existing, AQGreenCleanStartEvidence Clean) Create()
        {
            var x = AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration());
            var d = x.Decision; var a = x.Authority;
            d.AdmissionMode = a.AdmissionMode = "CLEAN_START"; d.CleanStartResetId = "SYNTHETIC-CLEAN-START";
            d.CleanStartRootCustomerId = 1;
            d.HistoricalMembers = new(); d.ReconciledPlacements = new(); d.PopulationDigests = null;
            d.PopulationParticipantKeys = null; d.AcceptedComponentDigest = null;
            d.SourceSnapshotDigest = d.BaseManifestDigest = d.FinalManifestDigest = a.FinalManifestDigest = null;
            a.B6FinalVerificationEvidenceDigest = a.B6ReconciliationAuthorityEvidenceDigest = a.B6AppliedReconciliationEvidenceDigest = null;
            a.MembershipDigest = d.MembershipDigest = AQGreenD10Commissioning.MembershipDigest(d.HistoricalMembers);
            a.ReconciledPlacementDigest = d.ReconciledPlacementDigest = AQGreenD10Commissioning.PlacementDigest(d.ReconciledPlacements);
            var hash = AQGreenD10SyntheticEvidence.Hash("synthetic-clean-start-evidence-only");
            var clean = new AQGreenCleanStartEvidence { ContractVersion = "AQGreenCleanStartV1", EvidenceClassification = "OPERATIONAL_EVIDENCE",
                TenantId = 1, ResetId = d.CleanStartResetId, ResetAtUtc = d.AuthorizedAtUtc.AddHours(-2),
                ClassificationDigest = hash, IndependentAcceptanceDigest = hash, OwnerResetAuthorizationDigest = hash,
                ResetSqlDigest = hash, BeforeStateDigest = hash, DatabaseIdentityDigest = d.RequiredDatabaseIdentityDigest,
                SchemaDigest = d.RequiredSchemaDigest, NoGenuineMembershipsInResetScope = true, AutomaticPayoutsDisabled = true,
                State = new AQGreenCleanStartState { PopulationCount = 0, ArchivedCount = 0, PlacementCount = 0, LegacyMembershipCount = 0,
                    UnclassifiedMembershipCount = 0, ProtectedPaymentCount = 0, RealCommissionCount = 0, LoanCount = 0,
                    GraduationCount = 0, MonthlyObligationCount = 0, PayoutCount = 0, OrphanTopologyCount = 0, InvalidParticipationCount = 0,
                    SourceDigest = hash, ArchiveDigest = hash, ResetReceiptDigest = hash,
                    ResetAtUtc = d.AuthorizedAtUtc.AddHours(-2), BoundaryUtc = d.V2EffectiveAtUtc } };
            Sign(x, clean); return (x, clean);
        }

        public static void Sign(AQGreenD10SyntheticEvidence x, AQGreenCleanStartEvidence c)
        { x.Authority.CleanStartEvidenceDigest = AQGreenB6CanonicalEvidence.Digest(c);
            x.Authority.DecisionDigest = AQGreenB6CanonicalEvidence.Digest(x.Decision); }

        public static void OwnerAttest(AQGreenCleanStartEvidence c)
        {
            var hash=AQGreenD10SyntheticEvidence.Hash("owner-attested-test-only-not-provider-verification");
            c.PaymentAdmissionMode=c.State.PaymentAdmissionMode=AQGreenCleanStartEvidence.OwnerAttestedTestOnly;
            c.OwnerTestOnlyAttestationDigest=c.State.OwnerTestOnlyAttestationDigest=hash;
            c.ProviderModeVerification="NOT_INDEPENDENTLY_VERIFIED";
            c.ProtectedFinancialRecordsPreserved=true;
            c.HistoricalFinancialRecordsDigest=c.State.ResetTenantFinancialRecordsDigest=hash;
            c.State.PaymentEvidenceDigest=c.State.ResetPaymentEvidenceDigest=hash;
            c.State.ProtectedPaymentCount=c.State.ResetProtectedPaymentCount=4;
        }

        [Fact]
        public void OwnerAttestationAdmitsRetainedPaymentsWithoutClaimingProviderVerification()
        {
            var (x,c)=Create();OwnerAttest(c);Sign(x,c);
            var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            bound.CleanStartEvidence.ProviderModeVerification.ShouldBe("NOT_INDEPENDENTLY_VERIFIED");
            bound.CleanStartEvidence.State.ProtectedPaymentCount.ShouldBe(4);
            bound.CleanStartEvidence.State.HasNoProtectedExposure().ShouldBeFalse();
            bound.CleanStartEvidence.AdmitsPreservedState(c.State).ShouldBeTrue();
            bound.EnsureConfiguration(AQGreenD10SyntheticEvidence.Configuration());
        }

        [Theory]
        [InlineData("mode")][InlineData("attestation")][InlineData("provider-claim")][InlineData("preservation")]
        [InlineData("current-payment-drift")][InlineData("reset-payment-drift")][InlineData("count-drift")]
        [InlineData("receipt-mode")][InlineData("receipt-attestation")][InlineData("financial-digest")]
        [InlineData("placements")][InlineData("unclassified")][InlineData("commission")][InlineData("loan")]
        [InlineData("graduation")][InlineData("obligation")][InlineData("payout")][InlineData("orphan")][InlineData("invalid")]
        public void OwnerAttestationCannotBypassPreservationUnknownEvidenceOrAnyOtherGuard(string failure)
        {
            var (x,c)=Create();OwnerAttest(c);
            switch(failure)
            {
                case "mode": c.PaymentAdmissionMode="INFERRED_TEST";break;
                case "attestation": c.OwnerTestOnlyAttestationDigest=null;break;
                case "provider-claim": c.ProviderModeVerification="INDEPENDENTLY_VERIFIED";break;
                case "preservation": c.ProtectedFinancialRecordsPreserved=false;break;
                case "current-payment-drift": c.State.PaymentEvidenceDigest=AQGreenD10SyntheticEvidence.Hash("changed-payment");break;
                case "reset-payment-drift": c.State.ResetPaymentEvidenceDigest=null;break;
                case "count-drift": c.State.ProtectedPaymentCount=5;break;
                case "receipt-mode": c.State.PaymentAdmissionMode=null;break;
                case "receipt-attestation": c.State.OwnerTestOnlyAttestationDigest=null;break;
                case "financial-digest": c.HistoricalFinancialRecordsDigest=AQGreenD10SyntheticEvidence.Hash("changed-financial-records");break;
                case "placements": c.State.PlacementCount=1;break;
                case "unclassified": c.State.UnclassifiedMembershipCount=1;break;
                case "commission": c.State.RealCommissionCount=1;break;
                case "loan": c.State.LoanCount=1;break;
                case "graduation": c.State.GraduationCount=1;break;
                case "obligation": c.State.MonthlyObligationCount=1;break;
                case "payout": c.State.PayoutCount=1;break;
                case "orphan": c.State.OrphanTopologyCount=1;break;
                case "invalid": c.State.InvalidParticipationCount=1;break;
            }
            Sign(x,c);Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
        }

        [Fact]
        public void EmptyApprovedTenantBindsWithNoFabricatedB6HistoryAndWorkersHeld()
        {
            var (x,c) = Create(); var bound = AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            bound.Decision.HistoricalMembers.ShouldBeEmpty(); bound.FinancialWorkersCommissioned.ShouldBeFalse();
            bound.Decision.FinalManifestDigest.ShouldBeNull(); bound.CleanStartEvidence.ResetId.ShouldBe(c.ResetId);
            bound.EnsureConfiguration(AQGreenD10SyntheticEvidence.Configuration());
            c.State.PopulationCount = 99; bound.CleanStartEvidence.State.PopulationCount.ShouldBe(0);
        }

        [Theory]
        [InlineData("payments")][InlineData("commission")][InlineData("loan")][InlineData("payout")]
        [InlineData("funding")][InlineData("obligation")][InlineData("membership")][InlineData("missing-count")]
        [InlineData("not-archived")][InlineData("classification")][InlineData("worker")][InlineData("B6")]
        [InlineData("mode")][InlineData("independence")][InlineData("receipt")][InlineData("foreign-tenant")][InlineData("root")]
        [InlineData("reset-time")][InlineData("recorded-boundary")]
        public void ExposureAndMissingOrMixedEvidenceFailClosed(string failure)
        {
            var (x,c)=Create();
            switch(failure)
            {
                case "payments": c.State.ProtectedPaymentCount=1; break;
                case "commission": c.State.RealCommissionCount=1; break;
                case "loan": c.State.LoanCount=1; break;
                case "payout": c.State.PayoutCount=1; break;
                case "funding": c.State.GraduationCount=1; break;
                case "obligation": c.State.MonthlyObligationCount=1; break;
                case "membership": c.State.UnclassifiedMembershipCount=1; break;
                case "missing-count": c.State.ProtectedPaymentCount=null; break;
                case "not-archived": c.State.PopulationCount=1; break;
                case "classification": c.NoGenuineMembershipsInResetScope=null; break;
                case "worker": x.Authority.FinancialWorkersCommissioningEvidenceDigest="unverified-worker-claim"; break;
                case "B6": x.Decision.FinalManifestDigest=AQGreenD10SyntheticEvidence.Hash("not-real-B6"); break;
                case "mode": x.Decision.AdmissionMode="HISTORICAL_B6"; break;
                case "independence": c.IndependentAcceptanceDigest=null; break;
                case "receipt": c.State.ResetReceiptDigest=null; break;
                case "foreign-tenant": c.TenantId=2; break;
                case "root": x.Decision.CleanStartRootCustomerId=null; break;
                case "reset-time": c.State.ResetAtUtc=null; break;
                case "recorded-boundary": c.State.BoundaryUtc=c.State.BoundaryUtc.Value.AddDays(7); break;
            }
            Sign(x,c);
            Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
        }

        [Fact]
        public void AutomaticPayoutsAndUnpinnedChangesRemainDenied()
        {
            var (x,c)=Create(); var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            var config=AQGreenD10SyntheticEvidence.Configuration(); config["App:AutomaticPayouts:Enabled"]="true";
            Should.Throw<InvalidOperationException>(()=>bound.EnsureConfiguration(config));
            config=AQGreenD10SyntheticEvidence.Configuration();config["App:FinancialOperations:Enabled"]="true";
            Should.Throw<InvalidOperationException>(()=>bound.EnsureConfiguration(config)).Message.ShouldBe("D10_CONFIGURATION_MISMATCH");
            c.ResetId="changed";
            Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
            Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindVerified(x.Decision,x.Authority,x.Final));
        }

        [Fact]
        public void WorkersRequireSeparateVerifiedCommissioningAndDoNotAuthorizeAutomaticPayouts()
        {
            var (x,c)=Create();
            var config=AQGreenD10SyntheticEvidence.Configuration();config["App:WeeklyCommissions:Enabled"]="true";
            x.Decision.RequiredConfigurationDigest=AQGreenD10Commissioning.ConfigurationDigest(config);Sign(x,c);
            var held=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            Should.Throw<InvalidOperationException>(()=>held.EnsureConfiguration(config)).Message.ShouldBe("D10_FINANCIAL_WORKER_AUTHORITY_MISSING");
            x.Authority.FinancialWorkersCommissioningEvidenceDigest=AQGreenD10SyntheticEvidence.Hash("separate-synthetic-worker-commissioning-only");
            var commissioned=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            commissioned.FinancialWorkersCommissioned.ShouldBeTrue();commissioned.EnsureConfiguration(config);
            config["App:AutomaticPayouts:Enabled"]="true";
            Should.Throw<InvalidOperationException>(()=>commissioned.EnsureConfiguration(config)).Message.ShouldBe("D10_CLEAN_START_AUTOMATIC_PAYOUTS_HELD");
        }

        [Fact]
        public async Task PendingCleanStartHoldsBusinessAndCallbacksButAllowsProtectedReadinessAndCannotReloadAway()
        {
            var config=AQGreenD10SyntheticEvidence.Configuration();
            config[AQGreenD10OperatorEvidenceSource.Prefix+"AdmissionMode"]="CLEAN_START";
            config[AQGreenD10OperatorEvidenceSource.Prefix+"Enabled"]="false";
            var reached=false;var middleware=new ApplicationRecoveryMiddleware(_=>{reached=true;return Task.CompletedTask;},config);
            foreach(var path in new[]{"/api/services/app/ClubMemberProgrammeParticipation/StartEntry","/api/payments/yoco/webhook"})
            {
                var c=new DefaultHttpContext();c.Request.Method="POST";c.Request.Path=path;c.Response.Body=new MemoryStream();
                await middleware.InvokeAsync(c);c.Response.StatusCode.ShouldBe(503);reached.ShouldBeFalse();
            }
            config[AQGreenD10OperatorEvidenceSource.Prefix+"Enabled"]="true";
            var late=new DefaultHttpContext();late.Request.Method="POST";late.Request.Path="/api/anything";late.Response.Body=new MemoryStream();
            await middleware.InvokeAsync(late);late.Response.StatusCode.ShouldBe(503);reached.ShouldBeFalse();
            var ready=new DefaultHttpContext();ready.Request.Method="GET";ready.Request.Path="/api/deployment-readiness";
            await middleware.InvokeAsync(ready);reached.ShouldBeTrue(); // Its controller still authenticates OIDC.
        }
    }
}
