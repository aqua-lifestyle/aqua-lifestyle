using System;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class TierBenefitsTests
    {
        [Theory]
        [InlineData(MembershipType.Jasper)]
        [InlineData(MembershipType.Onyx)]
        [InlineData(MembershipType.AQGreen)]
        [InlineData(MembershipType.BusinessPremier)]
        public void SavingsTerms_AreSharedByAllMembershipTiers(MembershipType membershipType)
        {
            var benefits = TierBenefits.ForTier(membershipType);

            Assert.Equal(1, benefits.SavingsWindowOpenDay);
            Assert.Equal(15, benefits.SavingsWindowCloseDay);
            Assert.Equal(0.20m, benefits.SavingsMaturityInterestRate);
            Assert.Equal(20m, benefits.CalculateSavingsMaturityInterest(100m));
            Assert.True(benefits.IsSavingsWindowOpen(new DateTime(2026, 7, 15)));
            Assert.False(benefits.IsSavingsWindowOpen(new DateTime(2026, 7, 16)));
        }
    }
}
