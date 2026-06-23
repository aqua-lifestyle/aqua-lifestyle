using System;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class ReferralAttributionTests
    {
        private readonly CommissionCalculator _commissionCalculator = new();

        private static (Facilitator facilitator, AreaLeader areaLeader, EnquiryConvertedEvent evt) Arrange(int directReferralsAlready = 0)
        {
            var facilitator = Facilitator.Register(tenantId: 1, customerId: 100, areaLeaderId: 50);
            facilitator.Id = 7;
            for (var i = 0; i < directReferralsAlready; i++)
            {
                facilitator.RecordDirectReferral();
            }

            var areaLeader = AreaLeader.Apply(tenantId: 1, customerId: 200, LicenseType.EntreLevel);
            areaLeader.Id = 50;

            var evt = new EnquiryConvertedEvent(enquiryId: 1, customerId: 999, productId: 2, referredByFacilitatorId: 7, convertedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            return (facilitator, areaLeader, evt);
        }

        [Fact]
        public void Attribute_CreatesDirectAndIndirectReferrals()
        {
            var (facilitator, areaLeader, evt) = Arrange();

            var service = new AqualLifeStyle.Domain.AreaNetwork.ReferralAttributionService(_commissionCalculator);
            var result = service.Attribute(evt, facilitator, areaLeader);

            result.DirectReferral.ReferrerFacilitatorId.ShouldBe(7);
            result.DirectReferral.Type.ShouldBe(ReferralType.Direct);
            result.DirectReferral.ReferredCustomerId.ShouldBe(999);
            result.DirectReferral.SourceEnquiryId.ShouldBe(1);

            result.IndirectReferral.ReferrerAreaLeaderId.ShouldBe(50);
            result.IndirectReferral.Type.ShouldBe(ReferralType.Indirect);
        }

        [Fact]
        public void Attribute_FirstReferral_NoRankAwardYet()
        {
            var (facilitator, areaLeader, evt) = Arrange();

            var service = new AqualLifeStyle.Domain.AreaNetwork.ReferralAttributionService(_commissionCalculator);
            var result = service.Attribute(evt, facilitator, areaLeader);

            facilitator.DirectReferrals.ShouldBe(1);
            facilitator.AwardBalance.ShouldBe(0m);
            result.FacilitatorAward.ShouldBe(0m);
            result.DirectReferral.AwardAmount.ShouldBe(0m);
            areaLeader.IndirectReferrals.ShouldBe(1);
        }

        [Fact]
        public void Attribute_AtBronzeThreshold_IssuesBronzeAward()
        {
            var (facilitator, areaLeader, evt) = Arrange(directReferralsAlready: 9);

            var service = new AqualLifeStyle.Domain.AreaNetwork.ReferralAttributionService(_commissionCalculator);
            var result = service.Attribute(evt, facilitator, areaLeader);

            facilitator.DirectReferrals.ShouldBe(10);
            facilitator.AwardBalance.ShouldBe(50m);
            facilitator.Rank.ShouldBe(FacilitatorRank.Bronze);
            result.FacilitatorAward.ShouldBe(50m);
            result.DirectReferral.AwardAmount.ShouldBe(50m);
        }

        [Fact]
        public void Attribute_AtGoldThreshold_IssuesGoldAward()
        {
            var (facilitator, areaLeader, evt) = Arrange(directReferralsAlready: 19);

            var service = new AqualLifeStyle.Domain.AreaNetwork.ReferralAttributionService(_commissionCalculator);
            var result = service.Attribute(evt, facilitator, areaLeader);

            facilitator.DirectReferrals.ShouldBe(20);
            facilitator.Rank.ShouldBe(FacilitatorRank.Gold);
            facilitator.AwardBalance.ShouldBe(250m);
        }

        [Fact]
        public void Attribute_AtSharedThreshold_AwardsOnlyHighestRankOnce()
        {
            var (facilitator, areaLeader, evt) = Arrange(directReferralsAlready: 59);

            var service = new AqualLifeStyle.Domain.AreaNetwork.ReferralAttributionService(_commissionCalculator);
            var result = service.Attribute(evt, facilitator, areaLeader);

            facilitator.DirectReferrals.ShouldBe(60);
            facilitator.Rank.ShouldBe(FacilitatorRank.PremierT60);
            facilitator.AwardBalance.ShouldBe(68750m);
            result.FacilitatorAward.ShouldBe(68750m);
            result.DirectReferral.AwardAmount.ShouldBe(68750m);
        }

        [Fact]
        public void Attribute_RequiresSourcingFacilitator()
        {
            var (facilitator, areaLeader, _) = Arrange();
            var evt = new EnquiryConvertedEvent(1, 999, 2, null, DateTime.UtcNow);

            var service = new AqualLifeStyle.Domain.AreaNetwork.ReferralAttributionService(_commissionCalculator);
            Should.Throw<ArgumentException>(() => service.Attribute(evt, facilitator, areaLeader));
        }
    }
}
