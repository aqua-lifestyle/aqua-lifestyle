using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AreaLeaderRankTests
    {
        private readonly RankProgressionPolicy _policy = new();

        [Theory]
        [InlineData(0, AreaLeaderRank.Ruby)]
        [InlineData(19, AreaLeaderRank.Ruby)]
        [InlineData(20, AreaLeaderRank.Ruby)]
        [InlineData(60, AreaLeaderRank.Emerald)]
        [InlineData(100, AreaLeaderRank.Premier)]
        [InlineData(200, AreaLeaderRank.Diamond)]
        [InlineData(18000, AreaLeaderRank.Ambassador)]
        public void EvaluateAreaLeaderRank_MapsOrderTarget(int orderTarget, AreaLeaderRank expected)
        {
            _policy.EvaluateAreaLeaderRank(orderTarget).ShouldBe(expected);
        }

        [Fact]
        public void Apply_SetsLicenseFeeAndEntryRank()
        {
            var leader = AreaLeader.Apply(tenantId: 1, customerId: 10, LicenseType.AreaIndependentLeader);
            leader.LicenseFee.ShouldBe(2500m);
            leader.Rank.ShouldBe(AreaLeaderRank.Ruby);
        }

        [Fact]
        public void RecordStartupOrder_AdvancesRankByOrderTarget()
        {
            var leader = AreaLeader.Apply(tenantId: 1, customerId: 10, LicenseType.EntreLevel);
            for (var i = 0; i < 60; i++) leader.RecordStartupOrder();

            leader.PromoteToCurrentRank(_policy);
            leader.Rank.ShouldBe(AreaLeaderRank.Emerald);
            leader.OrderTarget.ShouldBe(60);
        }

        [Fact]
        public void RecordIndirectReferral_TracksUplineReferrals()
        {
            var leader = AreaLeader.Apply(tenantId: 1, customerId: 10, LicenseType.EntreLevel);
            leader.RecordIndirectReferral();
            leader.RecordIndirectReferral();
            leader.IndirectReferrals.ShouldBe(2);
        }
    }
}
