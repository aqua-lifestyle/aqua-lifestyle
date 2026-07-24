using System;
using System.Linq;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Savings;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class SavingsAccountTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly SavingsAccountTerms Terms =
            SavingsAccountTerms.Create(
                "savings-2026-07",
                EffectiveFrom,
                maturityPeriodMonths: 12,
                minimumContributionAmount: 100m,
                maturityInterestRatePercent: 20m,
                contributionWindowStartDay: 1,
                contributionWindowEndDay: 15);

        [Fact]
        public void OpeningAccount_SetsTwelveMonthMaturityAndBlocksEarlyWithdrawal()
        {
            var account = OpenAccount();

            Assert.Equal(EffectiveFrom.AddMonths(12), account.MaturesAt);
            Assert.False(account.IsWithdrawalAllowed(account.MaturesAt.AddTicks(-1)));
            Assert.False(account.IsWithdrawalAllowed(account.MaturesAt));
            Assert.Equal(SavingsAccountStatus.Active, account.Status);
        }

        [Fact]
        public void Contribution_RequiresConfirmedSavingsPaymentOfAtLeastOneHundredRand()
        {
            var account = OpenAccount();
            var pendingPayment = CreatePayment(
                100m,
                EffectiveFrom.AddDays(9),
                "savings-pending");
            var smallPayment = CreateConfirmedPayment(
                99.99m,
                EffectiveFrom.AddDays(9),
                "savings-too-small");

            Assert.Throws<InvalidOperationException>(() =>
                account.ApplyConfirmedContribution(pendingPayment));
            Assert.Throws<InvalidOperationException>(() =>
                account.ApplyConfirmedContribution(smallPayment));
            Assert.Empty(account.Contributions);
        }

        [Fact]
        public void Contribution_OutsideFirstThroughFifteenthWindowIsRejected()
        {
            var account = OpenAccount();
            var payment = CreateConfirmedPayment(
                100m,
                new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc),
                "savings-closed-window");

            Assert.Throws<InvalidOperationException>(() =>
                account.ApplyConfirmedContribution(payment));
        }

        [Fact]
        public void EveryContributionEarnsFullTwentyPercentAndIsIdempotent()
        {
            var account = OpenAccount();
            var first = CreateConfirmedPayment(
                100m,
                EffectiveFrom.AddDays(9),
                "savings-first");
            var lateInTerm = CreateConfirmedPayment(
                250.55m,
                new DateTime(2027, 6, 10, 10, 0, 0, DateTimeKind.Utc),
                "savings-late-in-term");

            account.ApplyConfirmedContribution(first);
            account.ApplyConfirmedContribution(lateInTerm);
            account.ApplyConfirmedContribution(lateInTerm);

            Assert.Equal(350.55m, account.PrincipalBalance);
            Assert.Equal(70.11m, account.ProjectedInterestAmount);
            Assert.Equal(420.66m, account.ProjectedMaturityAmount);
            Assert.Equal(2, account.Contributions.Count);
            Assert.All(
                account.Contributions,
                contribution => Assert.Equal(20m, contribution.InterestRatePercent));
            Assert.Equal(
                50.11m,
                account.Contributions.Single(item => item.PaymentId == lateInTerm.Id)
                    .InterestAmount);
        }

        [Fact]
        public void ContributionMustBelongToAccountOwnerAndSavingsPurpose()
        {
            var account = OpenAccount();
            var anotherCustomerPayment = CreateConfirmedPayment(
                100m,
                EffectiveFrom.AddDays(9),
                "savings-wrong-customer",
                customerId: 8);
            var entryPayment = CreateConfirmedPayment(
                600m,
                EffectiveFrom.AddDays(9),
                "savings-wrong-purpose",
                purpose: MemberPaymentPurpose.EntryRegistration);

            Assert.Throws<InvalidOperationException>(() =>
                account.ApplyConfirmedContribution(anotherCustomerPayment));
            Assert.Throws<InvalidOperationException>(() =>
                account.ApplyConfirmedContribution(entryPayment));
        }

        [Fact]
        public void MaturitySnapshotsPrincipalInterestAndPayoutWithoutRewritingContributions()
        {
            var account = OpenAccount();
            account.ApplyConfirmedContribution(CreateConfirmedPayment(
                1000m,
                EffectiveFrom.AddDays(9),
                "savings-maturity"));

            Assert.Throws<InvalidOperationException>(() =>
                account.Mature(account.MaturesAt.AddTicks(-1)));

            account.Mature(account.MaturesAt);
            account.Mature(account.MaturesAt.AddDays(1));

            Assert.Equal(SavingsAccountStatus.Matured, account.Status);
            Assert.Equal(account.MaturesAt, account.MaturedAt);
            Assert.Equal(1000m, account.MaturityPrincipalAmount);
            Assert.Equal(200m, account.MaturityInterestAmount);
            Assert.Equal(1200m, account.MaturityPayoutAmount);
            Assert.Single(account.Contributions);
            Assert.True(account.IsWithdrawalAllowed(account.MaturesAt));
        }

        [Fact]
        public void MaturedAccountRejectsFurtherContributions()
        {
            var account = OpenAccount();
            account.Mature(account.MaturesAt);
            var payment = CreateConfirmedPayment(
                100m,
                account.MaturesAt.AddDays(9),
                "savings-after-maturity");

            Assert.Throws<InvalidOperationException>(() =>
                account.ApplyConfirmedContribution(payment));
        }

        [Fact]
        public void ExistingThreeMonthRefundThresholdRuleRemainsAvailable()
        {
            var account = OpenAccount();
            account.ApplyConfirmedContribution(CreateConfirmedPayment(
                1000m,
                EffectiveFrom.AddDays(9),
                "savings-refund-threshold"));

            Assert.False(account.ShouldTriggerRefund(1500m, monthsTracked: 2));
            Assert.True(account.ShouldTriggerRefund(1500m, monthsTracked: 3));
        }

        private static SavingsAccount OpenAccount()
        {
            return SavingsAccount.Open(
                tenantId: 1,
                customerId: 7,
                EffectiveFrom,
                Terms);
        }

        private static MemberPayment CreateConfirmedPayment(
            decimal amount,
            DateTime confirmedAt,
            string externalReference,
            int customerId = 7,
            MemberPaymentPurpose purpose = MemberPaymentPurpose.SavingsContribution)
        {
            var payment = CreatePayment(
                amount,
                confirmedAt,
                externalReference,
                customerId,
                purpose);
            payment.Confirm(confirmedAt);
            return payment;
        }

        private static MemberPayment CreatePayment(
            decimal amount,
            DateTime initiatedAt,
            string externalReference,
            int customerId = 7,
            MemberPaymentPurpose purpose = MemberPaymentPurpose.SavingsContribution)
        {
            return MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Yoco",
                externalReference,
                initiatedAt);
        }
    }
}
