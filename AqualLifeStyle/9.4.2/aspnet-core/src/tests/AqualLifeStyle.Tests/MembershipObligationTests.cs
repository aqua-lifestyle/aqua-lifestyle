using System;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class MembershipObligationTests
    {
        [Fact]
        public void Create_AssignsDefaultMonthlyObligationByType()
        {
            // Standard = 100m, Premium = 250m, Vip = 500m
            var standard = Membership.Create("Standard", "Standard tier", MembershipType.Standard);
            var premium = Membership.Create("Premium", "Premium tier", MembershipType.Premium);
            var vip = Membership.Create("Vip", "Vip tier", MembershipType.Vip);

            Assert.Equal(100m, standard.MonthlyObligationAmount);
            Assert.Equal(250m, premium.MonthlyObligationAmount);
            Assert.Equal(500m, vip.MonthlyObligationAmount);
        }

        [Fact]
        public void ChangeType_UpdatesMonthlyObligationToNewTierDefault()
        {
            var membership = Membership.Create("Standard", "Standard tier", MembershipType.Standard);
            Assert.Equal(100m, membership.MonthlyObligationAmount);

            membership.ChangeType(MembershipType.Premium);
            Assert.Equal(250m, membership.MonthlyObligationAmount);
        }

        [Fact]
        public void SetActivationDate_WithValidDate_SetsSuccessfully()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            var activationDate = new DateTime(2024, 1, 15);

            membership.SetActivationDate(activationDate);

            Assert.Equal(activationDate, membership.ActivationDate);
        }

        [Fact]
        public void SetActivationDate_WithInvalidDate_Throws()
        {
            var membership = Membership.Create("Standard", "Standard tier");

            Assert.Throws<ArgumentException>(() => membership.SetActivationDate(default));
        }

        [Fact]
        public void SetMonthlyObligation_WithValidAmount_SetsSuccessfully()
        {
            var membership = Membership.Create("Standard", "Standard tier");

            membership.SetMonthlyObligation(350m);

            Assert.Equal(350m, membership.MonthlyObligationAmount);
        }

        [Fact]
        public void SetMonthlyObligation_WithNegativeAmount_Throws()
        {
            var membership = Membership.Create("Standard", "Standard tier");

            Assert.Throws<ArgumentException>(() => membership.SetMonthlyObligation(-10m));
        }

        [Fact]
        public void MarkObligationMet_WithValidDate_SetsLastObligationDate()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            var metDate = new DateTime(2024, 2, 10);

            membership.MarkObligationMet(metDate);

            Assert.Equal(metDate, membership.LastObligationMetDate);
        }

        [Fact]
        public void MarkObligationMet_WithInvalidDate_Throws()
        {
            var membership = Membership.Create("Standard", "Standard tier");

            Assert.Throws<ArgumentException>(() => membership.MarkObligationMet(default));
        }

        [Fact]
        public void IsObligationMetForMonth_WithoutActivationDate_ReturnsFalse()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            var checkMonth = new DateTime(2024, 2, 1);

            Assert.False(membership.IsObligationMetForMonth(checkMonth));
        }

        [Fact]
        public void IsObligationMetForMonth_WithoutObligationRecord_ReturnsFalse()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            membership.SetActivationDate(new DateTime(2024, 1, 1));

            Assert.False(membership.IsObligationMetForMonth(new DateTime(2024, 2, 1)));
        }

        [Fact]
        public void IsObligationMetForMonth_WhenObligationMetInSameMonth_ReturnsTrue()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            membership.SetActivationDate(new DateTime(2024, 1, 1));
            membership.MarkObligationMet(new DateTime(2024, 2, 15));

            Assert.True(membership.IsObligationMetForMonth(new DateTime(2024, 2, 1)));
        }

        [Fact]
        public void IsObligationMetForMonth_WhenObligationMetInEarlierMonth_ReturnsFalse()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            membership.SetActivationDate(new DateTime(2024, 1, 1));
            membership.MarkObligationMet(new DateTime(2024, 2, 15));

            Assert.False(membership.IsObligationMetForMonth(new DateTime(2024, 3, 1)));
        }

        [Fact]
        public void IsObligationMetForMonth_WhenObligationMetInLaterMonth_ReturnsTrue()
        {
            var membership = Membership.Create("Standard", "Standard tier");
            membership.SetActivationDate(new DateTime(2024, 1, 1));
            membership.MarkObligationMet(new DateTime(2024, 3, 15));

            Assert.True(membership.IsObligationMetForMonth(new DateTime(2024, 2, 1)));
        }
    }
}
