using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class FacilitatorRankProgressionTests
    {
        private readonly RankProgressionPolicy _policy = new();

        [Theory]
        [InlineData(0, FacilitatorRank.Bronze)]
        [InlineData(9, FacilitatorRank.Bronze)]
        [InlineData(10, FacilitatorRank.Bronze)]
        [InlineData(20, FacilitatorRank.Gold)]
        [InlineData(25, FacilitatorRank.Pearl)]
        [InlineData(29, FacilitatorRank.Pearl)]
        [InlineData(30, FacilitatorRank.Sapphire)]
        [InlineData(50, FacilitatorRank.Ruby)]
        [InlineData(60, FacilitatorRank.PremierT60)]
        public void EvaluateFacilitatorRank_MapsCumulativeDirectReferrals(int directReferrals, FacilitatorRank expected)
        {
            _policy.EvaluateFacilitatorRank(directReferrals).ShouldBe(expected);
        }

        [Fact]
        public void NextRank_WhenThresholdsTie_PrefersHigherRankOrder()
        {
            _policy.NextRank(FacilitatorRank.Platinum).ShouldBe(FacilitatorRank.PremierT60);
        }

        [Fact]
        public void EvaluateFacilitatorRank_Negative_Throws()
        {
            Should.Throw<System.ArgumentException>(() => _policy.EvaluateFacilitatorRank(-1));
        }

        [Fact]
        public void CommissionCalculator_ReturnsRankAward()
        {
            var calculator = new CommissionCalculator();
            calculator.ComputeFacilitatorAward(FacilitatorRank.Ruby).Amount.ShouldBe(11250m);
            calculator.ComputeFacilitatorAward(FacilitatorRank.PremierT60).Amount.ShouldBe(68750m);
        }

        [Fact]
        public void FacilitatorRankTable_For_UnknownRank_ThrowsClearArgumentException()
        {
            var ex = Should.Throw<System.ArgumentException>(() => FacilitatorRankTable.For((FacilitatorRank)999));
            ex.ParamName.ShouldBe("rank");
            ex.Message.ShouldContain("No facilitator rank configuration exists for rank '999'.");
        }

        [Fact]
        public void Money_Add_CombinesSameCurrency()
        {
            var total = Money.Of(50m).Add(Money.Of(250m));
            total.Amount.ShouldBe(300m);
            total.Currency.ShouldBe("ZAR");
        }

        [Fact]
        public void Money_Add_DifferentCurrency_Throws()
        {
            Should.Throw<System.InvalidOperationException>(() => Money.Of(1m, "ZAR").Add(Money.Of(1m, "USD")));
        }
    }
}
