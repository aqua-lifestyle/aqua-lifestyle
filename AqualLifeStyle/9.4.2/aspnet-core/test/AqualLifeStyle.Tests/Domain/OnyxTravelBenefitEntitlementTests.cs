using System;
using AqualLifeStyle.Domain.Onyx;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxTravelBenefitEntitlementTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly OnyxPlanTerms PlanTerms = OnyxPlanTerms.Create(
            "onyx-2026-07",
            EffectiveFrom,
            6120m);

        private static readonly OnyxTravelBenefitTerms TravelTerms =
            OnyxTravelBenefitTerms.Create(
                "onyx-travel-2026-07",
                EffectiveFrom,
                OnyxNetworkLevel.Level3,
                waitingPeriodMonths: 3,
                memberTripContributionPercent: 10m);

        [Fact]
        public void CompleteLevelTwo_DoesNotGrantTravelEligibility()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 2,
                PlanTerms,
                EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxTravelBenefitEntitlement.GrantForQualifiedParticipant(
                    network[0],
                    network,
                    new OnyxNetworkQualificationEvaluator(),
                    TravelTerms,
                    EffectiveFrom.AddDays(10)));
        }

        [Fact]
        public void CompleteLevelThree_GrantsEligibilityWithThreeMonthWaitingPeriod()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 3,
                PlanTerms,
                EffectiveFrom);
            var eligibleAt = EffectiveFrom.AddDays(10);

            var entitlement =
                OnyxTravelBenefitEntitlement.GrantForQualifiedParticipant(
                    network[0],
                    network,
                    new OnyxNetworkQualificationEvaluator(),
                    TravelTerms,
                    eligibleAt);

            Assert.Equal(OnyxNetworkLevel.Level3, entitlement.QualifiedNetworkLevel);
            Assert.Equal(OnyxNetworkLevel.Level3, entitlement.RequiredNetworkLevel);
            Assert.Equal(OnyxTravelBenefitStatus.WaitingPeriod, entitlement.Status);
            Assert.Equal(eligibleAt.AddMonths(3), entitlement.WaitingPeriodEndsAt);
            Assert.Equal(10m, entitlement.MemberTripContributionPercent);
            Assert.Null(entitlement.ActivatedAt);
        }

        [Fact]
        public void TravelBenefit_ActivatesOnlyAfterWaitingPeriodAndIsIdempotent()
        {
            var network = OnyxNetworkTestBuilder.BuildCompleteNetwork(
                maximumDepth: 3,
                PlanTerms,
                EffectiveFrom);
            var entitlement =
                OnyxTravelBenefitEntitlement.GrantForQualifiedParticipant(
                    network[0],
                    network,
                    new OnyxNetworkQualificationEvaluator(),
                    TravelTerms,
                    EffectiveFrom.AddDays(10));
            var activatedAt = entitlement.WaitingPeriodEndsAt;

            Assert.Throws<InvalidOperationException>(() =>
                entitlement.ActivateAfterWaitingPeriod(activatedAt.AddTicks(-1)));

            entitlement.ActivateAfterWaitingPeriod(activatedAt);
            entitlement.ActivateAfterWaitingPeriod(activatedAt);

            Assert.Equal(OnyxTravelBenefitStatus.Active, entitlement.Status);
            Assert.Equal(activatedAt, entitlement.ActivatedAt);
        }
    }
}
