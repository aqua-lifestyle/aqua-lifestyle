using System;
using System.Linq;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class FacilitatorAggregateTests
    {
        [Fact]
        public void Register_SetsExpectedDefaults()
        {
            var facilitator = Facilitator.Register(tenantId: 1, customerId: 100, areaLeaderId: 50);

            facilitator.TenantId.ShouldBe(1);
            facilitator.CustomerId.ShouldBe(100);
            facilitator.AreaLeaderId.ShouldBe(50);
            facilitator.Rank.ShouldBe(FacilitatorRank.Bronze);
            facilitator.DirectReferrals.ShouldBe(0);
            facilitator.IndirectReferrals.ShouldBe(0);
            facilitator.AwardBalance.ShouldBe(0m);
        }

        [Theory]
        [InlineData(0, 100, 50)]
        [InlineData(1, 0, 50)]
        [InlineData(1, 100, 0)]
        public void Register_WithInvalidIdentifiers_Throws(int tenantId, int customerId, int areaLeaderId)
        {
            Should.Throw<ArgumentException>(() => Facilitator.Register(tenantId, customerId, areaLeaderId));
        }

        [Fact]
        public void RecordDirectReferral_IncrementsCount()
        {
            var facilitator = Facilitator.Register(1, 100, 50);

            facilitator.RecordDirectReferral();
            facilitator.RecordDirectReferral();

            facilitator.DirectReferrals.ShouldBe(2);
        }

        [Fact]
        public void RecordIndirectReferral_IncrementsCount()
        {
            var facilitator = Facilitator.Register(1, 100, 50);

            facilitator.RecordIndirectReferral();
            facilitator.RecordIndirectReferral();

            facilitator.IndirectReferrals.ShouldBe(2);
        }

        [Fact]
        public void DeletedFacilitator_RejectsReferralRecordingAndAwards()
        {
            var facilitator = Facilitator.Register(1, 100, 50);
            facilitator.IsDeleted = true;

            Should.Throw<InvalidOperationException>(() => facilitator.RecordDirectReferral());
            Should.Throw<InvalidOperationException>(() => facilitator.RecordIndirectReferral());
            Should.Throw<InvalidOperationException>(() => facilitator.AwardRank(FacilitatorRank.Gold, 250m));
        }

        [Fact]
        public void AwardRank_WithLowerRank_Throws()
        {
            var facilitator = Facilitator.Register(1, 100, 50);

            Should.Throw<ArgumentException>(() => facilitator.AwardRank((FacilitatorRank)(-1), 10m));
        }

        [Fact]
        public void AwardRank_FirstAwardForCurrentRank_AddsBalanceAndEvent()
        {
            var facilitator = Facilitator.Register(1, 100, 50);
            facilitator.Id = 9;

            facilitator.AwardRank(FacilitatorRank.Bronze, 50m);

            facilitator.Rank.ShouldBe(FacilitatorRank.Bronze);
            facilitator.AwardBalance.ShouldBe(50m);
            facilitator.DomainEvents.Count.ShouldBe(1);

            var evt = facilitator.DomainEvents.Single().ShouldBeOfType<FacilitatorRankAchievedEvent>();
            evt.FacilitatorId.ShouldBe(9);
            evt.CustomerId.ShouldBe(100);
            evt.Rank.ShouldBe(FacilitatorRank.Bronze);
            evt.AwardAmount.ShouldBe(50m);
        }

        [Fact]
        public void AwardRank_HigherRank_AccumulatesAwardAndUpdatesRank()
        {
            var facilitator = Facilitator.Register(1, 100, 50);

            facilitator.AwardRank(FacilitatorRank.Bronze, 50m);
            facilitator.AwardRank(FacilitatorRank.Gold, 250m);

            facilitator.Rank.ShouldBe(FacilitatorRank.Gold);
            facilitator.AwardBalance.ShouldBe(300m);
            facilitator.DomainEvents.Count.ShouldBe(2);
        }

        [Fact]
        public void AwardRank_SameRankAfterAward_IsIdempotent()
        {
            var facilitator = Facilitator.Register(1, 100, 50);
            facilitator.AwardRank(FacilitatorRank.Bronze, 50m);

            facilitator.AwardRank(FacilitatorRank.Bronze, 500m);

            facilitator.AwardBalance.ShouldBe(50m);
            facilitator.DomainEvents.Count.ShouldBe(1);
        }
    }
}
