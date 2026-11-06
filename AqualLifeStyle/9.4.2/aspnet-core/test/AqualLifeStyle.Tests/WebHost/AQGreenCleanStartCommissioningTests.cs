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

        private static void Prospective(AQGreenD10SyntheticEvidence x, AQGreenCleanStartEvidence c)
        {
            c.ContractVersion="AQGreenCleanStartV2";
            c.BoundaryPolicy=c.State.BoundaryPolicy=AQGreenCleanStartEvidence.ProspectiveBoundary;
            c.BoundaryAuthorizationDigest=c.State.BoundaryAuthorizationDigest=x.Authority.FinancialBoundaryEvidenceDigest;
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=x.Decision.V2EffectiveAtUtc.AddMinutes(17);
            c.State.BoundaryUtc=x.Decision.V2EffectiveAtUtc;
        }

        public static void PostReset(AQGreenD10SyntheticEvidence x, AQGreenCleanStartEvidence c)
        {
            var resetBoundary=DateTime.UtcNow.Date.AddDays(-4).AddHours(1);
            var authorization=DateTime.UtcNow.AddMinutes(-2);
            c.ContractVersion="AQGreenCleanStartV2";
            c.BoundaryPolicy=c.State.BoundaryPolicy=AQGreenCleanStartEvidence.ProspectiveBoundary;
            c.BoundaryAuthorizationDigest=c.State.BoundaryAuthorizationDigest=x.Authority.FinancialBoundaryEvidenceDigest;
            c.ResetAtUtc=resetBoundary.AddMinutes(-10);c.State.ResetAtUtc=c.ResetAtUtc;c.State.BoundaryUtc=resetBoundary;
            x.Decision.ContractVersion=AQGreenD10Decision.PostResetContract;
            x.Decision.CleanStartRootCustomerId=null;
            x.Decision.CleanStartRootSelectionPolicy=AQGreenD10Decision.HostSelectRootAfterRegistration;
            x.Decision.CleanStartResetBoundaryUtc=resetBoundary;
            x.Decision.AuthorizedAtUtc=authorization;
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=authorization.AddSeconds(10);
            x.Authority.ContractVersion=AQGreenD10VerifiedAuthority.PostResetContract;
            x.Authority.AuthorizedAtUtc=authorization.AddSeconds(30);
            x.Authority.ResetBoundaryAuthorizationEvidenceDigest=c.BoundaryAuthorizationDigest;
            x.Authority.FinancialBoundaryEvidenceDigest=AQGreenD10SyntheticEvidence.Hash("synthetic-distinct-current-live-financial-authority");
        }

        [Fact]
        public void CurrentLiveAuthorizationAndLaterVerificationBindWithoutRewritingTheHistoricalReset()
        {
            var (x,c)=Create();OwnerAttest(c);PostReset(x,c);Sign(x,c);
            var historicalBytes=AQGreenB6CanonicalEvidence.Bytes(c);
            var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            bound.EnsureCurrent(DateTime.UtcNow);
            bound.Decision.CleanStartResetBoundaryUtc.ShouldBe(c.State.BoundaryUtc.Value.UtcDateTime);
            bound.Decision.V2EffectiveAtUtc.ShouldBeGreaterThan(c.State.BoundaryUtc.Value.UtcDateTime);
            AQGreenB6CanonicalEvidence.Bytes(bound.CleanStartEvidence).ShouldBe(historicalBytes);
            bound.FinancialWorkersCommissioned.ShouldBeFalse();
            // Verification may occur after the policy effective time; it cannot
            // grant use before its actual timestamp or rewrite that policy epoch.
            x.Authority.AuthorizedAtUtc=DateTime.UtcNow.AddMinutes(5);Sign(x,c);
            var future=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            Should.Throw<InvalidOperationException>(()=>future.EnsureCurrent(DateTime.UtcNow))
                .Message.ShouldBe("D10_AUTHORITY_NOT_YET_AUTHORIZED");
        }

        [Theory]
        [InlineData("missing-reset-boundary")][InlineData("changed-reset-boundary")][InlineData("non-utc-reset-boundary")]
        [InlineData("backdated-owner")][InlineData("effective-before-owner")][InlineData("review-before-owner")]
        [InlineData("expired-review")][InlineData("reset-only-financial-authority")][InlineData("missing-original-boundary-authority")]
        [InlineData("legacy-authority")][InlineData("legacy-evidence")][InlineData("unknown-version")][InlineData("preselected-root")]
        public void PostResetCommissioningRejectsContradictoryChronologyScopeOrReusedRetirementPermission(string failure)
        {
            var (x,c)=Create();PostReset(x,c);
            switch(failure)
            {
                case "missing-reset-boundary":x.Decision.CleanStartResetBoundaryUtc=null;break;
                case "changed-reset-boundary":x.Decision.CleanStartResetBoundaryUtc=x.Decision.CleanStartResetBoundaryUtc.Value.AddMinutes(1);break;
                case "non-utc-reset-boundary":x.Decision.CleanStartResetBoundaryUtc=DateTime.SpecifyKind(x.Decision.CleanStartResetBoundaryUtc.Value,DateTimeKind.Unspecified);break;
                case "backdated-owner":x.Decision.AuthorizedAtUtc=c.State.BoundaryUtc.Value.UtcDateTime.AddSeconds(-1);break;
                case "effective-before-owner":x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=x.Decision.AuthorizedAtUtc.AddSeconds(-1);break;
                case "review-before-owner":x.Authority.AuthorizedAtUtc=x.Decision.AuthorizedAtUtc.AddSeconds(-1);break;
                case "expired-review":x.Authority.ExpiresAtUtc=x.Authority.AuthorizedAtUtc;break;
                case "reset-only-financial-authority":x.Authority.FinancialBoundaryEvidenceDigest=c.BoundaryAuthorizationDigest;break;
                case "missing-original-boundary-authority":x.Authority.ResetBoundaryAuthorizationEvidenceDigest=null;break;
                case "legacy-authority":x.Authority.ContractVersion="AQGreenD10VerifiedAuthorityV1";break;
                case "legacy-evidence":c.ContractVersion="AQGreenCleanStartV1";break;
                case "unknown-version":x.Decision.ContractVersion="AQGreenD10CutoverV9";break;
                case "preselected-root":x.Decision.CleanStartRootCustomerId=1;break;
            }
            Sign(x,c);Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
        }

        [Fact]
        public void PostResetHistoryClassificationDoesNotRetireTheUnapprovedIntervalOrChangeLegacyEncoding()
        {
            var (x,c)=Create();
            var legacy=AQGreenB6CanonicalEvidence.Bytes(x.Decision);
            System.Text.Json.Nodes.JsonNode.Parse(legacy).AsObject().ContainsKey("cleanStartResetBoundaryUtc").ShouldBeFalse();
            System.Text.Json.Nodes.JsonNode.Parse(AQGreenB6CanonicalEvidence.Bytes(x.Authority)).AsObject()
                .ContainsKey("resetBoundaryAuthorizationEvidenceDigest").ShouldBeFalse();
            PostReset(x,c);Sign(x,c);var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            var terms=AqualLifeStyle.Domain.Onyx.EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-history-classification",c.ResetAtUtc.AddDays(-1),1200,600,7);
            var old=AqualLifeStyle.Domain.Onyx.EntryParticipation.StartIndependently(1,1,terms,c.ResetAtUtc.AddMinutes(-1));old.IsDeleted=true;
            var gap=AqualLifeStyle.Domain.Onyx.EntryParticipation.StartIndependently(1,2,terms,c.State.BoundaryUtc.Value.UtcDateTime.AddMinutes(1));gap.IsDeleted=true;
            bound.IsRetiredCleanStartParticipation(old).ShouldBeTrue();bound.IsRetiredCleanStartParticipation(gap).ShouldBeFalse();
        }

        [Fact]
        public void ExplicitProspectiveCleanStartBindsWithoutMovingCommissionWeeksOrEnablingMoney()
        {
            var (x,c)=Create();OwnerAttest(c);Prospective(x,c);Sign(x,c);
            var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            bound.CleanStartEvidence.BoundaryPolicy.ShouldBe(AQGreenCleanStartEvidence.ProspectiveBoundary);
            bound.Decision.V2EffectiveAtUtc.ShouldBe(c.State.BoundaryUtc.Value.UtcDateTime);
            bound.FinancialWorkersCommissioned.ShouldBeFalse();
            Should.Throw<ArgumentException>(()=>AQGreenCommissionWeek.FromStartUtc(bound.Decision.V2EffectiveAtUtc));
            var config=AQGreenD10SyntheticEvidence.Configuration();config["App:AutomaticPayouts:Enabled"]="true";
            Should.Throw<InvalidOperationException>(()=>bound.EnsureConfiguration(config));
            // Historical admission keeps its existing canonical-week requirement.
            var historical=AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration());
            historical.Decision.V2EffectiveAtUtc=historical.Authority.V2EffectiveAtUtc=bound.Decision.V2EffectiveAtUtc;
            Should.Throw<ArgumentException>(()=>AQGreenD10Commissioning.BindVerified(historical.Decision,historical.Authority,historical.Final));
        }

        [Theory]
        [InlineData("unselected-version")][InlineData("unknown-version")][InlineData("unknown-policy")]
        [InlineData("missing-authority")][InlineData("zero-authority")][InlineData("authority-mismatch")]
        [InlineData("receipt-policy")][InlineData("receipt-authority")][InlineData("backdated")]
        [InlineData("non-utc")][InlineData("missing-owner")][InlineData("missing-independent")]
        public void ProspectiveBoundaryRequiresExplicitNativeAndIndependentBindingWithoutBackdating(string failure)
        {
            var (x,c)=Create();Prospective(x,c);
            switch(failure)
            {
                case "unselected-version":c.ContractVersion="AQGreenCleanStartV1";break;
                case "unknown-version":c.ContractVersion="AQGreenCleanStartV9";break;
                case "unknown-policy":c.BoundaryPolicy="INFERRED_IMMEDIATE";break;
                case "missing-authority":c.BoundaryAuthorizationDigest=null;break;
                case "zero-authority":c.BoundaryAuthorizationDigest=new string('0',64);break;
                case "authority-mismatch":c.BoundaryAuthorizationDigest=AQGreenD10SyntheticEvidence.Hash("different-boundary-decision");break;
                case "receipt-policy":c.State.BoundaryPolicy=null;break;
                case "receipt-authority":c.State.BoundaryAuthorizationDigest=null;break;
                case "backdated":x.Decision.V2EffectiveAtUtc=x.Decision.AuthorizedAtUtc.AddTicks(-1);break;
                case "non-utc":x.Decision.V2EffectiveAtUtc=DateTime.SpecifyKind(x.Decision.V2EffectiveAtUtc,DateTimeKind.Unspecified);break;
                case "missing-owner":c.OwnerResetAuthorizationDigest=null;break;
                case "missing-independent":c.IndependentAcceptanceDigest=null;break;
            }
            Sign(x,c);Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
        }

        [Theory]
        [InlineData(nameof(AQGreenCleanStartState.PlacementCount))]
        [InlineData(nameof(AQGreenCleanStartState.UnclassifiedMembershipCount))]
        [InlineData(nameof(AQGreenCleanStartState.RealCommissionCount))]
        [InlineData(nameof(AQGreenCleanStartState.LoanCount))]
        [InlineData(nameof(AQGreenCleanStartState.GraduationCount))]
        [InlineData(nameof(AQGreenCleanStartState.MonthlyObligationCount))]
        [InlineData(nameof(AQGreenCleanStartState.PayoutCount))]
        [InlineData(nameof(AQGreenCleanStartState.OrphanTopologyCount))]
        [InlineData(nameof(AQGreenCleanStartState.InvalidParticipationCount))]
        public void ProspectiveBoundaryPreservesEveryNonPaymentExposureGuard(string property)
        {
            var (x,c)=Create();OwnerAttest(c);Prospective(x,c);
            typeof(AQGreenCleanStartState).GetProperty(property).SetValue(c.State,(long?)1);
            Sign(x,c);Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
        }

        [Fact]
        public void LegacyCanonicalEvidenceKeepsItsEncodingAndRejectsAnImplicitImmediateBoundary()
        {
            var (x,c)=Create();
            var canonical=AQGreenB6CanonicalEvidence.Bytes(c);
            var json=System.Text.Json.Nodes.JsonNode.Parse(canonical).AsObject();
            json.ContainsKey("boundaryPolicy").ShouldBeFalse();json.ContainsKey("boundaryAuthorizationDigest").ShouldBeFalse();
            json["state"].AsObject().ContainsKey("boundaryPolicy").ShouldBeFalse();
            x.Decision.V2EffectiveAtUtc=x.Authority.V2EffectiveAtUtc=x.Decision.V2EffectiveAtUtc.AddMinutes(17);
            c.State.BoundaryUtc=x.Decision.V2EffectiveAtUtc;Sign(x,c);
            Should.Throw<ArgumentException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
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
        public void ExplicitRootUnboundLaunchBindsOnlyTheVerifiedEmptyActiveBaseline()
        {
            var (x,c)=Create();
            x.Decision.CleanStartRootCustomerId=null;
            x.Decision.CleanStartRootSelectionPolicy=AQGreenD10Decision.HostSelectRootAfterRegistration;
            Sign(x,c);
            var bound=AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c);
            bound.Decision.CleanStartRootCustomerId.ShouldBeNull();
            bound.FinancialWorkersCommissioned.ShouldBeFalse();
            bound.EnsureConfiguration(AQGreenD10SyntheticEvidence.Configuration());
            // An archived disposable cohort is allowed; it is not an active network.
            c.State.PopulationCount=c.State.ArchivedCount=3;Sign(x,c);
            AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c).ShouldNotBeNull();
        }

        [Theory]
        [InlineData("implicit")][InlineData("unknown-policy")][InlineData("mixed-pin")]
        [InlineData("zero-pin")][InlineData("negative-pin")][InlineData("live-population")]
        [InlineData("unknown-population")][InlineData("unknown-archive")][InlineData("placement")]
        [InlineData("legacy-membership")][InlineData("authority-not-rebound")]
        public void RootUnboundLaunchCannotInferAuthorityOrAdoptPopulatedOrUnknownState(string failure)
        {
            var (x,c)=Create();
            x.Decision.CleanStartRootCustomerId=null;
            x.Decision.CleanStartRootSelectionPolicy=AQGreenD10Decision.HostSelectRootAfterRegistration;
            switch(failure)
            {
                case "implicit":x.Decision.CleanStartRootSelectionPolicy=null;break;
                case "unknown-policy":x.Decision.CleanStartRootSelectionPolicy="AUTOMATIC_ROOT";break;
                case "mixed-pin":x.Decision.CleanStartRootCustomerId=1;break;
                case "zero-pin":x.Decision.CleanStartRootCustomerId=0;break;
                case "negative-pin":x.Decision.CleanStartRootCustomerId=-1;break;
                case "live-population":c.State.PopulationCount=1;break;
                case "unknown-population":c.State.PopulationCount=null;break;
                case "unknown-archive":c.State.ArchivedCount=null;break;
                case "placement":c.State.PlacementCount=1;break;
                case "legacy-membership":c.State.LegacyMembershipCount=1;break;
            }
            if(failure!="authority-not-rebound") Sign(x,c);
            Should.Throw<InvalidOperationException>(()=>AQGreenD10Commissioning.BindCleanStartVerified(x.Decision,x.Authority,c));
        }

        [Fact]
        public void DeferredRootSelectionDoesNotChangeLegacyEncodingOrAuthorizeHistoricalAdmission()
        {
            var (x,c)=Create();
            System.Text.Json.Nodes.JsonNode.Parse(AQGreenB6CanonicalEvidence.Bytes(x.Decision))
                .AsObject().ContainsKey("cleanStartRootSelectionPolicy").ShouldBeFalse();
            var historical=AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration());
            historical.Decision.CleanStartRootSelectionPolicy=AQGreenD10Decision.HostSelectRootAfterRegistration;
            Should.Throw<InvalidOperationException>(()=>historical.Bind()).Message.ShouldBe("D10_ADMISSION_MODE_MISMATCH");
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
