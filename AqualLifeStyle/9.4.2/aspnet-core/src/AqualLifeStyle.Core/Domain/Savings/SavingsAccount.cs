using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Domain.Savings
{
    public enum SavingsAccountStatus
    {
        Active = 0,
        Matured = 1
    }

    public class SavingsAccount : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<SavingsContribution> _contributions = new();

        public int TenantId { get; set; }
        public int CustomerId { get; private set; }
        public DateTime OpenedAt { get; private set; }
        public DateTime MaturesAt { get; private set; }
        public DateTime? MaturedAt { get; private set; }
        public SavingsAccountStatus Status { get; private set; }
        public decimal PrincipalBalance { get; private set; }
        public decimal ProjectedInterestAmount { get; private set; }
        public decimal ProjectedMaturityAmount =>
            PrincipalBalance + ProjectedInterestAmount;
        public decimal? MaturityPrincipalAmount { get; private set; }
        public decimal? MaturityInterestAmount { get; private set; }
        public decimal? MaturityPayoutAmount { get; private set; }
        public int MaturityPeriodMonths { get; private set; }
        public decimal MinimumContributionAmount { get; private set; }
        public decimal MaturityInterestRatePercent { get; private set; }
        public int ContributionWindowStartDay { get; private set; }
        public int ContributionWindowEndDay { get; private set; }
        public string Currency { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }
        public IReadOnlyCollection<SavingsContribution> Contributions =>
            _contributions.AsReadOnly();

        protected SavingsAccount()
        {
        }

        private SavingsAccount(
            int tenantId,
            int customerId,
            DateTime openedAt,
            SavingsAccountTerms terms)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (openedAt == default || openedAt < terms.EffectiveFrom)
            {
                throw new ArgumentException(
                    "The savings account must open within the applicable terms.",
                    nameof(openedAt));
            }

            Id = Guid.NewGuid();
            TenantId = tenantId;
            CustomerId = customerId;
            OpenedAt = openedAt;
            MaturityPeriodMonths = terms.MaturityPeriodMonths;
            MaturesAt = openedAt.AddMonths(terms.MaturityPeriodMonths);
            MinimumContributionAmount = terms.MinimumContributionAmount;
            MaturityInterestRatePercent = terms.MaturityInterestRatePercent;
            ContributionWindowStartDay = terms.ContributionWindowStartDay;
            ContributionWindowEndDay = terms.ContributionWindowEndDay;
            Currency = terms.Currency;
            TermsVersion = terms.Version;
            TermsEffectiveFrom = terms.EffectiveFrom;
            Status = SavingsAccountStatus.Active;
        }

        public static SavingsAccount Open(
            int tenantId,
            int customerId,
            DateTime openedAt,
            SavingsAccountTerms terms)
        {
            return new SavingsAccount(tenantId, customerId, openedAt, terms);
        }

        public void ApplyConfirmedContribution(MemberPayment payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (_contributions.Any(contribution => contribution.PaymentId == payment.Id))
            {
                return;
            }

            if (Status != SavingsAccountStatus.Active)
            {
                throw new InvalidOperationException(
                    "A matured savings account cannot accept further contributions.");
            }

            if (payment.Status != MemberPaymentStatus.Confirmed ||
                !payment.ConfirmedAt.HasValue)
            {
                throw new InvalidOperationException(
                    "Only a confirmed payment can be added to savings.");
            }

            if (payment.TenantId != TenantId || payment.CustomerId != CustomerId)
            {
                throw new InvalidOperationException(
                    "The payment does not belong to this savings account.");
            }

            if (payment.Purpose != MemberPaymentPurpose.SavingsContribution)
            {
                throw new InvalidOperationException(
                    "The payment is not a savings contribution.");
            }

            if (!string.Equals(payment.Currency, Currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Savings contributions must use {Currency}.");
            }

            if (payment.Amount < MinimumContributionAmount)
            {
                throw new InvalidOperationException(
                    $"Each savings contribution must be at least {Currency} {MinimumContributionAmount:0.00}.");
            }

            var contributionAt = payment.ConfirmedAt.Value;
            if (contributionAt < OpenedAt)
            {
                throw new InvalidOperationException(
                    "A contribution cannot precede the savings account opening.");
            }

            if (contributionAt >= MaturesAt)
            {
                throw new InvalidOperationException(
                    "The savings account has reached its 12-month maturity date.");
            }

            if (contributionAt.Day < ContributionWindowStartDay ||
                contributionAt.Day > ContributionWindowEndDay)
            {
                throw new InvalidOperationException(
                    $"Savings contributions are accepted from day {ContributionWindowStartDay} through day {ContributionWindowEndDay} of each month.");
            }

            var contribution = SavingsContribution.Create(
                payment,
                MaturityInterestRatePercent,
                TermsVersion);
            _contributions.Add(contribution);
            PrincipalBalance += contribution.Amount;
            ProjectedInterestAmount += contribution.InterestAmount;
        }

        public bool IsWithdrawalAllowed(DateTime asOf)
        {
            if (asOf == default)
            {
                throw new ArgumentException("A withdrawal assessment time is required.", nameof(asOf));
            }

            return Status == SavingsAccountStatus.Matured && asOf >= MaturesAt;
        }

        public void Mature(DateTime maturedAt)
        {
            if (Status == SavingsAccountStatus.Matured)
            {
                return;
            }

            if (maturedAt < MaturesAt)
            {
                throw new InvalidOperationException(
                    "The savings account cannot mature before 12 months have elapsed.");
            }

            MaturedAt = maturedAt;
            MaturityPrincipalAmount = PrincipalBalance;
            MaturityInterestAmount = ProjectedInterestAmount;
            MaturityPayoutAmount = ProjectedMaturityAmount;
            Status = SavingsAccountStatus.Matured;
        }

        public bool ShouldTriggerRefund(decimal minimumThreshold, int monthsTracked)
        {
            if (minimumThreshold <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumThreshold));
            }

            if (monthsTracked < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsTracked));
            }

            return monthsTracked >= 3 && PrincipalBalance < minimumThreshold;
        }
    }

    public class SavingsContribution : Entity<Guid>
    {
        public Guid PaymentId { get; private set; }
        public decimal Amount { get; private set; }
        public DateTime ContributedAt { get; private set; }
        public decimal InterestRatePercent { get; private set; }
        public decimal InterestAmount { get; private set; }
        public string TermsVersion { get; private set; }

        protected SavingsContribution()
        {
        }

        private SavingsContribution(
            MemberPayment payment,
            decimal interestRatePercent,
            string termsVersion)
        {
            Id = Guid.NewGuid();
            PaymentId = payment.Id;
            Amount = payment.Amount;
            ContributedAt = payment.ConfirmedAt.Value;
            InterestRatePercent = interestRatePercent;
            InterestAmount = decimal.Round(
                payment.Amount * interestRatePercent / 100m,
                2,
                MidpointRounding.AwayFromZero);
            TermsVersion = termsVersion;
        }

        internal static SavingsContribution Create(
            MemberPayment payment,
            decimal interestRatePercent,
            string termsVersion)
        {
            return new SavingsContribution(payment, interestRatePercent, termsVersion);
        }
    }
}
