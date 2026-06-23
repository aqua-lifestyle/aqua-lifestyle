using System;
using System.Linq;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AreaLeaderAggregateTests
    {
        private readonly RankProgressionPolicy _policy = new();

        [Theory]
        [InlineData(LicenseType.EntreLevel, 750)]
        [InlineData(LicenseType.AreaIndependentLeader, 2500)]
        public void Apply_SetsExpectedDefaults(LicenseType licenseType, decimal expectedFee)
        {
            var leader = AreaLeader.Apply(tenantId: 1, customerId: 10, licenseType);

            leader.TenantId.ShouldBe(1);
            leader.CustomerId.ShouldBe(10);
            leader.LicenseType.ShouldBe(licenseType);
            leader.LicenseFee.ShouldBe(expectedFee);
            leader.Rank.ShouldBe(AreaLeaderRank.Ruby);
            leader.AreaSpaceId.ShouldBeNull();
            leader.MonthlySubscription.ShouldBe(0m);
            leader.DirectReferrals.ShouldBe(0);
            leader.IndirectReferrals.ShouldBe(0);
            leader.OrderTarget.ShouldBe(0);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        public void Apply_WithInvalidIdentifiers_Throws(int tenantId, int customerId)
        {
            Should.Throw<ArgumentException>(() => AreaLeader.Apply(tenantId, customerId, LicenseType.EntreLevel));
        }

        [Fact]
        public void Apply_WithUnknownLicenseType_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => AreaLeader.Apply(1, 10, (LicenseType)999));
        }

        [Fact]
        public void LinkAreaSpace_WithValidId_SetsAreaSpaceId()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);

            leader.LinkAreaSpace(42);

            leader.AreaSpaceId.ShouldBe(42);
        }

        [Fact]
        public void LinkAreaSpace_WithInvalidId_Throws()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);

            Should.Throw<ArgumentException>(() => leader.LinkAreaSpace(0));
        }

        [Fact]
        public void RecordFacilitator_IncrementsDirectReferrals()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);

            leader.RecordFacilitator();
            leader.RecordFacilitator();

            leader.DirectReferrals.ShouldBe(2);
        }

        [Fact]
        public void RecordStartupOrder_IncrementsOrderTarget()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);

            leader.RecordStartupOrder();
            leader.RecordStartupOrder();

            leader.OrderTarget.ShouldBe(2);
        }

        [Fact]
        public void RecordIndirectReferral_IncrementsIndirectReferrals()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);

            leader.RecordIndirectReferral();
            leader.RecordIndirectReferral();

            leader.IndirectReferrals.ShouldBe(2);
        }

        [Fact]
        public void DeletedLeader_RejectsReferralAndOrderTracking()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);
            leader.IsDeleted = true;

            Should.Throw<InvalidOperationException>(() => leader.RecordFacilitator());
            Should.Throw<InvalidOperationException>(() => leader.RecordStartupOrder());
            Should.Throw<InvalidOperationException>(() => leader.RecordIndirectReferral());
        }

        [Fact]
        public void PromoteToCurrentRank_RequiresPolicy()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);

            Should.Throw<ArgumentNullException>(() => leader.PromoteToCurrentRank(null));
        }

        [Fact]
        public void PromoteToCurrentRank_UsesOrderTargetToAdvanceRank()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);
            Enumerable.Range(0, 200).ToList().ForEach(_ => leader.RecordStartupOrder());

            leader.PromoteToCurrentRank(_policy);

            leader.Rank.ShouldBe(AreaLeaderRank.Diamond);
        }

        [Fact]
        public void ApproveApplication_RecordsApprovalOnlyOnce()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);
            var approvedAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

            leader.ApproveApplication(approvedAt);
            leader.ApproveApplication(approvedAt.AddHours(1));

            leader.IsApproved.ShouldBeTrue();
            leader.ApprovedAt.ShouldBe(approvedAt);
        }

        [Fact]
        public void DemoteOneRank_MovesDownOneTier()
        {
            var leader = AreaLeader.Apply(1, 10, LicenseType.EntreLevel);
            Enumerable.Range(0, 100).ToList().ForEach(_ => leader.RecordStartupOrder());
            leader.PromoteToCurrentRank(_policy);

            leader.DemoteOneRank();

            leader.Rank.ShouldBe(AreaLeaderRank.Emerald);
        }
    }
}
