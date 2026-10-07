using System;
using AqualLifeStyle.Domain.Onyx;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public sealed class EffectiveRecruiterOrderingTests
    {
        private static readonly DateTime Start = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Activation = Start.AddHours(4);
        private static readonly DateTime Cutoff = Start.AddDays(5);

        [Fact]
        public void StartedBeforeActivation_OrdersAtActivation()
        {
            var result = Derive(currentRecruiter: 10);

            result.EffectiveRecruiterCustomerId.ShouldBe(10);
            result.QualifiedUnderRecruiterAt.ShouldBe(Activation);
        }

        [Fact]
        public void StartedAtActivation_IsALegalBoundary()
        {
            var result = EffectiveRecruiterOrdering.Derive(20, 10, Activation, Activation,
                Array.Empty<EffectiveRecruiterCorrectionEvidence>(), Cutoff, "AQGreen");

            result.QualifiedUnderRecruiterAt.ShouldBe(Activation);
        }

        [Fact]
        public void CutoffEffectiveCorrectionAfterActivation_OrdersAtCorrection()
        {
            var correctedAt = Activation.AddHours(2);
            var result = Derive(11, new EffectiveRecruiterCorrectionEvidence(10, 11, correctedAt));

            result.EffectiveRecruiterCustomerId.ShouldBe(11);
            result.QualifiedUnderRecruiterAt.ShouldBe(correctedAt);
        }

        [Fact]
        public void CutoffEffectiveCorrectionBeforeActivation_StillOrdersAtActivation()
        {
            var result = Derive(11,
                new EffectiveRecruiterCorrectionEvidence(10, 11, Start.AddHours(2)));

            result.EffectiveRecruiterCustomerId.ShouldBe(11);
            result.QualifiedUnderRecruiterAt.ShouldBe(Activation);
        }

        [Fact]
        public void PostCutoffCorrection_UsesPreviousRecruiterAndActivation()
        {
            var result = Derive(11,
                new EffectiveRecruiterCorrectionEvidence(10, 11, Cutoff.AddSeconds(1)));

            result.EffectiveRecruiterCustomerId.ShouldBe(10);
            result.QualifiedUnderRecruiterAt.ShouldBe(Activation);
        }

        [Fact]
        public void ActivationEqualToEffectiveCorrection_IsStableBoundary()
        {
            var result = Derive(11,
                new EffectiveRecruiterCorrectionEvidence(10, 11, Activation));

            result.EffectiveRecruiterCustomerId.ShouldBe(11);
            result.QualifiedUnderRecruiterAt.ShouldBe(Activation);
        }

        private static EffectiveRecruiterOrderingEvidence Derive(
            int? currentRecruiter,
            params EffectiveRecruiterCorrectionEvidence[] corrections) =>
            EffectiveRecruiterOrdering.Derive(20, currentRecruiter, Start, Activation,
                corrections, Cutoff, "AQGreen");
    }
}
