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
        public void UnqualifiedRecruit_DoesNotCompleteABranch()
        {
            var network = BuildNetwork(maxDepth: 1);
            var unqualified = EntryParticipation.Start(
                tenantId: 2,
                customerId: 7,
                recruiterCustomerId: 1,
                Terms,
                EffectiveFrom);
            network.RemoveAll(participation => participation.CustomerId == 6);
            network.Add(unqualified);

            Assert.Equal(EntryNetworkLevel.None, _evaluator.Evaluate(1, network));
        }

        private static List<EntryParticipation> BuildNetwork(
            int maxDepth,
            int? incompleteRecruiterId = null)
        {
            var participations = new List<EntryParticipation>
            {
                CreateQualifiedParticipation(customerId: 1, recruiterCustomerId: 9000)
            };
            var currentLevel = new List<int> { 1 };
            var nextCustomerId = 2;

            for (var depth = 1; depth <= maxDepth; depth++)
            {
                var nextLevel = new List<int>();
                foreach (var recruiterCustomerId in currentLevel)
                {
                    var recruitCount = recruiterCustomerId == incompleteRecruiterId
                        ? EntryNetworkQualificationEvaluator.BranchSize - 1
                        : EntryNetworkQualificationEvaluator.BranchSize;
                    for (var index = 0; index < recruitCount; index++)
                    {
                        participations.Add(CreateQualifiedParticipation(nextCustomerId, recruiterCustomerId));
                        nextLevel.Add(nextCustomerId);
                        nextCustomerId++;
                    }
                }

                currentLevel = nextLevel;
            }

            return participations;
        }

        private static EntryParticipation CreateQualifiedParticipation(
            int customerId,
            int recruiterCustomerId)
        {
            var tenantId = customerId % 2 == 0 ? 1 : 2;
            var participation = EntryParticipation.Start(
                tenantId,
                customerId,
                recruiterCustomerId,
                Terms,
                EffectiveFrom);
            var registration = CreateConfirmedPayment(
                tenantId,
                customerId,
                MemberPaymentPurpose.EntryRegistration,
                $"registration-{customerId}");
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = CreateConfirmedPayment(
                tenantId,
                customerId,
                MemberPaymentPurpose.EntryActivation,
                $"activation-{customerId}");
            participation.ApplyConfirmedActivationPayment(activation);
            return participation;
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
