using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class OnyxTravelBenefitEligibilityProcessorTests
    {
        private static readonly OnyxPlanTerms ParticipationTerms =
            OnyxPlanTerms.Create(
                "2026-07",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                6120m);

        [Fact]
        public async Task Synchronize_GrantsLevelThreeBenefitAndActivatesItAfterWaitingPeriod()
        {
            var repository = Substitute.For<
                IRepository<OnyxTravelBenefitEntitlement, Guid>>();
            var persisted = new List<OnyxTravelBenefitEntitlement>();
            repository.GetAllListAsync(
                    Arg.Any<Expression<
                        Func<OnyxTravelBenefitEntitlement, bool>>>()!)
                .Returns(_ => Task.FromResult(persisted));
            repository.InsertAsync(
                    Arg.Any<OnyxTravelBenefitEntitlement>())
                .Returns(call =>
                {
                    var entitlement =
                        call.Arg<OnyxTravelBenefitEntitlement>();
                    persisted.Add(entitlement);
                    return Task.FromResult(entitlement);
                });

            var processor = new OnyxTravelBenefitEligibilityProcessor(
                repository,
                new CurrentOnyxTravelBenefitTermsProvider());
            var network = CreateCompleteLevelThreeNetwork();
            var eligibleAt =
                new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);
            var effectiveNetwork = EffectiveProgrammeNetwork.BuildOnyx(
                1,
                network,
                eligibleAt);

            var grant = await processor.SynchronizeAsync(
                1,
                network,
                effectiveNetwork,
                eligibleAt,
                eligibleAt);
            var repeatGrant = await processor.SynchronizeAsync(
                1,
                network,
                effectiveNetwork,
                eligibleAt,
                eligibleAt.AddDays(1));
            var activation = await processor.SynchronizeAsync(
                1,
                network,
                effectiveNetwork,
                eligibleAt,
                eligibleAt.AddMonths(3));
            var repeatActivation = await processor.SynchronizeAsync(
                1,
                network,
                effectiveNetwork,
                eligibleAt,
                eligibleAt.AddMonths(3).AddDays(1));

            grant.GrantedCount.ShouldBe(1);
            grant.ActivatedCount.ShouldBe(0);
            repeatGrant.GrantedCount.ShouldBe(0);
            repeatGrant.ActivatedCount.ShouldBe(0);
            activation.ActivatedCount.ShouldBe(1);
            repeatActivation.ActivatedCount.ShouldBe(0);
            persisted.Count.ShouldBe(1);
            persisted[0].CustomerId.ShouldBe(network[0].CustomerId);
            persisted[0].QualifiedNetworkLevel.ShouldBe(
                OnyxNetworkLevel.Level3);
            persisted[0].MemberTripContributionPercent.ShouldBe(10m);
            persisted[0].Status.ShouldBe(OnyxTravelBenefitStatus.Active);
            persisted[0].EligibleAt.ShouldBe(eligibleAt);
            persisted[0].ActivatedAt.ShouldBe(persisted[0].WaitingPeriodEndsAt);
        }

        [Fact]
        public async Task DelayedFirstGrant_ActivatesAtTheContractualWaitingEnd()
        {
            var repository = Substitute.For<
                IRepository<OnyxTravelBenefitEntitlement, Guid>>();
            var persisted = new List<OnyxTravelBenefitEntitlement>();
            repository.GetAllListAsync(
                    Arg.Any<Expression<
                        Func<OnyxTravelBenefitEntitlement, bool>>>()!)
                .Returns(_ => Task.FromResult(persisted));
            repository.InsertAsync(Arg.Any<OnyxTravelBenefitEntitlement>())
                .Returns(call =>
                {
                    var entitlement = call.Arg<OnyxTravelBenefitEntitlement>();
                    persisted.Add(entitlement);
                    return Task.FromResult(entitlement);
                });
            var processor = new OnyxTravelBenefitEligibilityProcessor(
                repository,
                new CurrentOnyxTravelBenefitTermsProvider());
            var network = CreateCompleteLevelThreeNetwork();
            var eligibleAt = new DateTime(
                2026,
                8,
                3,
                10,
                0,
                0,
                DateTimeKind.Utc);
            var effectiveNetwork = EffectiveProgrammeNetwork.BuildOnyx(
                1,
                network,
                eligibleAt);

            var result = await processor.SynchronizeAsync(
                1,
                network,
                effectiveNetwork,
                eligibleAt,
                eligibleAt.AddMonths(3).AddDays(2));

            result.GrantedCount.ShouldBe(1);
            result.ActivatedCount.ShouldBe(1);
            persisted[0].Status.ShouldBe(OnyxTravelBenefitStatus.Active);
            persisted[0].ActivatedAt.ShouldBe(persisted[0].WaitingPeriodEndsAt);
        }

        private static List<OnyxParticipation>
            CreateCompleteLevelThreeNetwork()
        {
            var activatedAt =
                new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);
            var nextCustomerId = 1;
            var nextPaymentNumber = 1;
            var root = Activate(
                OnyxParticipation.StartDirectIndependently(
                    1,
                    nextCustomerId++,
                    1,
                    ParticipationTerms,
                    activatedAt.AddMinutes(-1)),
                activatedAt,
                nextPaymentNumber++);
            var network = new List<OnyxParticipation> { root };
            var currentLevel = new List<OnyxParticipation> { root };

            for (var depth = 0; depth < 3; depth++)
            {
                var nextLevel = new List<OnyxParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var branch = 0; branch < 5; branch++)
                    {
                        var recruit = Activate(
                            OnyxParticipation.StartDirectUnderRecruiter(
                                1,
                                nextCustomerId++,
                                recruiter,
                                1,
                                ParticipationTerms,
                                activatedAt.AddMinutes(-1)),
                            activatedAt,
                            nextPaymentNumber++);
                        network.Add(recruit);
                        nextLevel.Add(recruit);
                    }
                }

                currentLevel = nextLevel;
            }

            return network;
        }

        private static OnyxParticipation Activate(
            OnyxParticipation participation,
            DateTime confirmedAt,
            int paymentNumber)
        {
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                ParticipationTerms.DirectEntryAmount,
                "Test",
                $"travel-benefit-{paymentNumber}",
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            participation.ApplyConfirmedDirectEntryPayment(payment);
            participation.ApproveByAdministrator(1L, confirmedAt);
            return participation;
        }
    }
}
