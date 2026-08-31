using System;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public sealed class AQGreenWeeklySalesEligibilityTests
    {
        private static readonly DateTime WeekStartUtc =
            new(2026, 8, 27, 22, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime WeekEndUtc = WeekStartUtc.AddDays(7);

        [Theory]
        [InlineData(5, 5, 5, AQGreenWeeklySalesThresholdResult.Met)]
        [InlineData(6, 9, 100, AQGreenWeeklySalesThresholdResult.Met)]
        [InlineData(4, 5, 5, AQGreenWeeklySalesThresholdResult.NotMet)]
        [InlineData(5, 4, 5, AQGreenWeeklySalesThresholdResult.NotMet)]
        [InlineData(5, 5, 4, AQGreenWeeklySalesThresholdResult.NotMet)]
        [InlineData(20, 0, 0, AQGreenWeeklySalesThresholdResult.NotMet)]
        public void Evaluator_RequiresEachCategoryIndependently(
            int spray,
            int oneLitre,
            int fiveLitre,
            AQGreenWeeklySalesThresholdResult expected)
        {
            AQGreenWeeklySalesEligibilityEvaluator.Evaluate(
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion,
                    new AQGreenWeeklySalesQuantities(
                        spray,
                        oneLitre,
                        fiveLitre))
                .ShouldBe(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("AQGreenWeeklySalesEligibilityV2")]
        public void Evaluator_FailsClosedForUnsupportedVersion(string version)
        {
            Should.Throw<AQGreenWeeklySalesEligibilityVersionNotSupportedException>(() =>
                AQGreenWeeklySalesEligibilityEvaluator.Evaluate(
                    version,
                    new AQGreenWeeklySalesQuantities(5, 5, 5)));
        }

        [Fact]
        public void CommissionWeek_IsJohannesburgFridayAndEndExclusive()
        {
            var week = AQGreenCommissionWeek.FromStartUtc(WeekStartUtc);

            week.EndExclusiveUtc.ShouldBe(WeekEndUtc);
            week.Contains(WeekStartUtc).ShouldBeTrue();
            week.Contains(WeekEndUtc.AddTicks(-1)).ShouldBeTrue();
            week.Contains(WeekEndUtc).ShouldBeFalse();
            Should.Throw<ArgumentException>(() =>
                AQGreenCommissionWeek.FromStartUtc(WeekStartUtc.AddHours(1)));
        }

        [Fact]
        public void ConfirmedMet_StoresReviewerFactsAndEvaluatorResult()
        {
            var decision = Held();
            decision.AddManualEvidence("ticket:123", WeekEndUtc);

            decision.Confirm(
                new AQGreenWeeklySalesQuantities(5, 5, 5),
                42,
                WeekEndUtc);

            decision.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Confirmed);
            decision.ThresholdResult.ShouldBe(AQGreenWeeklySalesThresholdResult.Met);
            decision.ReviewedSprayQuantity.ShouldBe(5);
            decision.ReviewedByUserId.ShouldBe(42);
            decision.ReviewedAt.ShouldBe(WeekEndUtc);
        }

        [Fact]
        public void ConfirmedNotMet_IsAValidFinalDecision()
        {
            var decision = Held();
            decision.AddManualEvidence("ticket:not-met", WeekEndUtc);

            decision.Confirm(
                new AQGreenWeeklySalesQuantities(5, 4, 5),
                42,
                WeekEndUtc);

            decision.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Confirmed);
            decision.ThresholdResult.ShouldBe(AQGreenWeeklySalesThresholdResult.NotMet);
        }

        [Fact]
        public void Rejected_HasNoAuthoritativeQuantitiesOrThresholdResult()
        {
            var decision = Held();
            decision.AddManualEvidence("ticket:rejected", WeekEndUtc);

            decision.Reject("insufficient provenance", 42, WeekEndUtc);

            decision.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Rejected);
            decision.ReviewedSprayQuantity.ShouldBeNull();
            decision.ReviewedOneLitreQuantity.ShouldBeNull();
            decision.ReviewedFiveLitreQuantity.ShouldBeNull();
            decision.ThresholdResult.ShouldBeNull();
            decision.RejectionReason.ShouldBe("insufficient provenance");
        }

        [Fact]
        public void Finalization_RequiresEvidenceAndClosedWeek()
        {
            Should.Throw<InvalidOperationException>(() =>
                Held().Confirm(
                    new AQGreenWeeklySalesQuantities(5, 5, 5),
                    42,
                    WeekEndUtc));

            var decision = Held();
            decision.AddManualEvidence("ticket:early", WeekStartUtc);
            Should.Throw<InvalidOperationException>(() =>
                decision.Reject("reviewed too early", 42, WeekEndUtc.AddTicks(-1)));
        }

        [Fact]
        public void Evidence_IsNormalizedDeduplicatedAndCannotFollowFinalization()
        {
            var decision = Held();
            decision.AddManualEvidence("  ticket:one  ", WeekEndUtc);
            decision.AddManualEvidence("ticket:one", WeekEndUtc);
            decision.EvidenceReferences.Count.ShouldBe(1);
            decision.EvidenceReferences.ShouldContain(item =>
                item.TechnicalReference == "ticket:one");

            decision.Reject("not reliable", 42, WeekEndUtc);
            Should.Throw<InvalidOperationException>(() =>
                decision.AddManualEvidence("ticket:late", WeekEndUtc));
            Should.Throw<InvalidOperationException>(() =>
                decision.Reject("retry mutation", 42, WeekEndUtc));
        }

        private static AQGreenWeeklySalesEligibilityDecision Held() =>
            AQGreenWeeklySalesEligibilityDecision.Begin(
                1,
                Guid.NewGuid(),
                AQGreenCommissionWeek.FromStartUtc(WeekStartUtc),
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
    }
}
