using System;
using System.Collections.Generic;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EntryNetworkQualificationEvaluatorTests
    {
        private static readonly DateTime EffectiveFrom = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly EntryProgrammeTerms Terms = EntryProgrammeTerms.Create(
            "entry-2026-07",
            EffectiveFrom,
            600m,
            600m,
            600m,
            7);
        private readonly EntryNetworkQualificationEvaluator _evaluator = new();

        [Theory]
        [InlineData(EntryNetworkLevel.Level1, 5)]
        [InlineData(EntryNetworkLevel.Level2, 25)]
        [InlineData(EntryNetworkLevel.Level3, 125)]
        [InlineData(EntryNetworkLevel.Level4, 625)]
        [InlineData(EntryNetworkLevel.Level5, 3125)]
        public void RequiredPopulation_UsesFivePersonStructuralDepth(
            EntryNetworkLevel level,
            int expectedPopulation)
        {
            Assert.Equal(
                expectedPopulation,
                EntryNetworkQualificationEvaluator.GetRequiredPopulation(level));
        }

        [Fact]
        public void Level1_RequiresFiveQualifiedDirectRecruits()
        {
            var fourRecruitNetwork = BuildNetwork(maxDepth: 1, incompleteRecruiterId: 1);
            var completeNetwork = BuildNetwork(maxDepth: 1);

            Assert.Equal(
                EntryNetworkLevel.None,
                _evaluator.Evaluate(customerId: 1, fourRecruitNetwork));
            Assert.Equal(
                EntryNetworkLevel.Level1,
                _evaluator.Evaluate(customerId: 1, completeNetwork));
        }

        [Fact]
        public void Level2_RequiresEveryLevel1BranchToHaveFiveQualifiedMembers()
        {
            var incompleteNetwork = BuildNetwork(maxDepth: 2, incompleteRecruiterId: 2);
            var completeNetwork = BuildNetwork(maxDepth: 2);

            Assert.Equal(
                EntryNetworkLevel.Level1,
                _evaluator.Evaluate(customerId: 1, incompleteNetwork));
            Assert.Equal(
                EntryNetworkLevel.Level2,
                _evaluator.Evaluate(customerId: 1, completeNetwork));
        }

        [Fact]
        public void Level3_RequiresEveryLevel2BranchToHaveFiveQualifiedMembers()
        {
            var incompleteNetwork = BuildNetwork(maxDepth: 3, incompleteRecruiterId: 7);
            var completeNetwork = BuildNetwork(maxDepth: 3);

            Assert.Equal(
                EntryNetworkLevel.Level2,
                _evaluator.Evaluate(customerId: 1, incompleteNetwork));
            Assert.Equal(
                EntryNetworkLevel.Level3,
                _evaluator.Evaluate(customerId: 1, completeNetwork));
        }

        [Fact]
        public void Level4_RequiresAllSixHundredAndTwentyFivePositions()
        {
            var incompleteNetwork = BuildNetwork(
                maxDepth: 4,
                incompleteRecruiterId: 32);
            var completeNetwork = BuildNetwork(maxDepth: 4);

            Assert.Equal(
                EntryNetworkLevel.Level3,
                _evaluator.Evaluate(customerId: 1, incompleteNetwork));
            Assert.Equal(
                EntryNetworkLevel.Level4,
                _evaluator.Evaluate(customerId: 1, completeNetwork));
        }

        [Fact]
        public void Level5_QualifiesDeterministicallyFromCutoffNetwork()
        {
            var completeNetwork = BuildNetwork(maxDepth: 5);
            var cutoffNetwork = EffectiveProgrammeNetwork.BuildAQGreen(
                expectedTenantId: 1,
                completeNetwork,
                EffectiveFrom.AddDays(7));

            Assert.Equal(3906, completeNetwork.Count);
            Assert.Equal(
                EntryNetworkLevel.Level5,
                _evaluator.Evaluate(customerId: 1, completeNetwork));
            Assert.Equal(
                EntryNetworkLevel.Level5,
                _evaluator.Evaluate(customerId: 1, cutoffNetwork));
        }

        [Fact]
        public void UnqualifiedRecruit_DoesNotCompleteABranch()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Find(participation => participation.CustomerId == 1);
            var unqualified = EntryParticipation.StartUnderRecruiter(
                tenantId: 1,
                customerId: 7,
                root,
                Terms,
                EffectiveFrom);
            network.RemoveAll(participation => participation.CustomerId == 6);
            network.Add(unqualified);

            Assert.Equal(EntryNetworkLevel.None, _evaluator.Evaluate(1, network));
        }

        [Fact]
        public void MoreThanFiveQualifiedDirectRecruits_UsesTheEarliestFive()
        {
            var network = BuildNetwork(maxDepth: 1);
            var root = network.Find(participation => participation.CustomerId == 1);
            network.Add(CreateQualifiedParticipation(7, root));

            Assert.Equal(EntryNetworkLevel.Level1, _evaluator.Evaluate(1, network));
        }

        [Fact]
        public void CutoffNetwork_UsesPlacementBeforeAPostCutoffCorrection()
        {
            var network = BuildNetwork(maxDepth: 1);
            var originalRecruiter = network.Find(participation => participation.CustomerId == 1);
            var correctedParticipation = network.Find(participation => participation.CustomerId == 2);
            var newRecruiter = CreateQualifiedIndependentParticipation(customerId: 100);
            var cutoff = EffectiveFrom.AddDays(7);
            network.Add(newRecruiter);
            correctedParticipation.CorrectRecruiter(
                newRecruiter,
                administratorUserId: 1,
                reason: "Correct placement",
                correctedAt: cutoff.AddMinutes(1));

            var cutoffNetwork = EffectiveProgrammeNetwork.BuildAQGreen(1, network, cutoff);

            Assert.Equal(
                EntryNetworkLevel.Level1,
                _evaluator.Evaluate(originalRecruiter.CustomerId, cutoffNetwork));
            Assert.Equal(
                EntryNetworkLevel.None,
                _evaluator.Evaluate(newRecruiter.CustomerId, cutoffNetwork));
        }

        [Fact]
        public void CutoffNetwork_RejectsActiveParticipationWithoutActivationEvidence()
        {
            var network = BuildNetwork(maxDepth: 1);
            var activatedAt = typeof(EntryParticipation)
                .GetProperty(nameof(EntryParticipation.ActivatedAt));
            Assert.NotNull(activatedAt);
            activatedAt.SetValue(network[0], null);

            Assert.Throws<InvalidOperationException>(() =>
                EffectiveProgrammeNetwork.BuildAQGreen(
                    1,
                    network,
                    EffectiveFrom.AddDays(7)));
        }

        [Fact]
        public void MixedTenantNetworkInput_FailsClosed()
        {
            var network = BuildNetwork(maxDepth: 1);
            network.Add(CreateQualifiedIndependentParticipation(
                customerId: 100,
                tenantId: 2));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                EffectiveProgrammeNetwork.BuildAQGreen(
                    expectedTenantId: 1,
                    network,
                    EffectiveFrom.AddDays(7)));

            Assert.Contains("outside Tenant 1", exception.Message);
        }

        private static List<EntryParticipation> BuildNetwork(
            int maxDepth,
            int? incompleteRecruiterId = null)
        {
            var participations = new List<EntryParticipation>
            {
                CreateQualifiedIndependentParticipation(customerId: 1)
            };
            var currentLevel = new List<EntryParticipation> { participations[0] };
            var nextCustomerId = 2;

            for (var depth = 1; depth <= maxDepth; depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiterParticipation in currentLevel)
                {
                    var recruitCount = recruiterParticipation.CustomerId == incompleteRecruiterId
                        ? EntryNetworkQualificationEvaluator.BranchSize - 1
                        : EntryNetworkQualificationEvaluator.BranchSize;
                    for (var index = 0; index < recruitCount; index++)
                    {
                        var recruit = CreateQualifiedParticipation(nextCustomerId, recruiterParticipation);
                        participations.Add(recruit);
                        nextLevel.Add(recruit);
                        nextCustomerId++;
                    }
                }

                currentLevel = nextLevel;
            }

            return participations;
        }

        private static EntryParticipation CreateQualifiedIndependentParticipation(
            int customerId,
            int tenantId = 1)
        {
            var participation = EntryParticipation.StartIndependently(
                tenantId,
                customerId,
                Terms,
                EffectiveFrom);
            Activate(participation);
            return participation;
        }

        private static EntryParticipation CreateQualifiedParticipation(
            int customerId,
            EntryParticipation recruiterParticipation)
        {
            var participation = EntryParticipation.StartUnderRecruiter(
                tenantId: 1,
                customerId,
                recruiterParticipation,
                Terms,
                EffectiveFrom);
            Activate(participation);
            return participation;
        }

        private static void Activate(EntryParticipation participation)
        {
            var registration = CreateConfirmedPayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                $"registration-{participation.CustomerId}");
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = CreateConfirmedPayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                $"activation-{participation.CustomerId}");
            participation.ApplyConfirmedActivationPayment(activation);
            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
        }

        private static MemberPayment CreateConfirmedPayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            string externalReference)
        {
            var payment = MemberPayment.CreatePending(
                tenantId,
                customerId,
                purpose,
                600m,
                "Yoco",
                externalReference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            return payment;
        }
    }
}
