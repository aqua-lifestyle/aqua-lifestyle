using System;
using AqualLifeStyle.Domain.Savings;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class SavingsAccountTests
    {
        [Fact]
        public void RecordDeposit_WithinOpenWindow_AccumulatesAmount()
        {
            var account = new SavingsAccount(memberId: 7, openedAt: new DateTime(2024, 1, 1));
            var window = SavingsWindow.OpenFor(new DateTime(2024, 1, 10));
            account.ActivateSavingsAccount(new DateTime(2024, 2, 1));

            account.RecordDeposit(100m, new DateTime(2024, 2, 10), window);

            Assert.Equal(100m, account.TotalSaved);
            Assert.Equal(1, account.DepositCount);
        }

        [Fact]
        public void RecordDeposit_OutsideSavingsWindow_Throws()
        {
            var account = new SavingsAccount(memberId: 7, openedAt: new DateTime(2024, 1, 1));
            var window = SavingsWindow.OpenFor(new DateTime(2024, 1, 16));
            account.ActivateSavingsAccount(new DateTime(2024, 2, 1));

            Assert.Throws<InvalidOperationException>(() => account.RecordDeposit(100m, new DateTime(2024, 1, 16), window));
        }

        [Fact]
        public void ShouldTriggerRefund_WhenThresholdNotMetAfterThreeMonths()
        {
            var account = new SavingsAccount(memberId: 7, openedAt: new DateTime(2024, 1, 1));
            var window = SavingsWindow.OpenFor(new DateTime(2024, 1, 10));
            account.ActivateSavingsAccount(new DateTime(2024, 2, 1));
            account.RecordDeposit(1000m, new DateTime(2024, 2, 10), window);

            Assert.True(account.ShouldTriggerRefund(minimumThreshold: 1500m, monthsTracked: 3));
        }

        [Fact]
        public void CalculateInterest_UsesAnnualRateAndMonths()
        {
            var account = new SavingsAccount(memberId: 7, openedAt: new DateTime(2024, 1, 1));
            var window = SavingsWindow.OpenFor(new DateTime(2024, 1, 10));
            account.ActivateSavingsAccount(new DateTime(2024, 2, 1));
            account.RecordDeposit(1000m, new DateTime(2024, 2, 10), window);

            var interest = account.CalculateInterest(annualInterestRate: 20m, monthsActive: 6);

            Assert.Equal(100m, interest);
        }
    }
}
