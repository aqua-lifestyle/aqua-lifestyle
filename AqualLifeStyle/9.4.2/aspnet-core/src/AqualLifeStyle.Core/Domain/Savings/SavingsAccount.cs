using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.Savings
{
    public class SavingsAccount
    {
        public int MemberId { get; private set; }
        public decimal TotalSaved { get; private set; }
        public int DepositCount { get; private set; }
        public bool IsLockedForFirstYear { get; private set; }
        public DateTime? FirstYearUnlockDate { get; private set; }
        public List<SavingsDeposit> Deposits { get; private set; } = new();

        public SavingsAccount(int memberId, DateTime openedAt)
        {
            MemberId = memberId;
            IsLockedForFirstYear = true;
            FirstYearUnlockDate = openedAt.AddYears(1);
        }

        public void ActivateSavingsAccount(DateTime asOf)
        {
            IsLockedForFirstYear = false;
            FirstYearUnlockDate = asOf;
        }

        public void RecordDeposit(decimal amount, DateTime depositedAt, SavingsWindow window)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive.");
            }

            if (!window.IsOpenFor(depositedAt))
            {
                throw new InvalidOperationException("Savings deposits can only be submitted during the open savings window.");
            }

            if (IsLockedForFirstYear && FirstYearUnlockDate.HasValue && depositedAt < FirstYearUnlockDate.Value)
            {
                throw new InvalidOperationException("Savings deposits are locked during the first year.");
            }

            Deposits.Add(new SavingsDeposit(amount, depositedAt));
            TotalSaved += amount;
            DepositCount++;
        }

        public void UnlockFirstYearFunds(DateTime asOf)
        {
            if (asOf >= FirstYearUnlockDate)
            {
                IsLockedForFirstYear = false;
                FirstYearUnlockDate = null;
            }
        }

        public bool ShouldTriggerRefund(decimal minimumThreshold, int monthsTracked)
        {
            if (monthsTracked < 3)
            {
                return false;
            }

            return TotalSaved < minimumThreshold;
        }

        public decimal CalculateInterest(decimal annualInterestRate, int monthsActive)
        {
            if (monthsActive <= 0)
            {
                return 0m;
            }

            return TotalSaved * (annualInterestRate / 100m) * (monthsActive / 12m);
        }
    }

    public class SavingsDeposit
    {
        public decimal Amount { get; private set; }
        public DateTime DepositedAt { get; private set; }

        public SavingsDeposit(decimal amount, DateTime depositedAt)
        {
            Amount = amount;
            DepositedAt = depositedAt;
        }
    }

    public class SavingsWindow
    {
        public SavingsWindow(DateTime date)
        {
            Date = date;
        }

        public DateTime Date { get; private set; }

        public static SavingsWindow OpenFor(DateTime date)
        {
            return new SavingsWindow(date);
        }

        public bool IsOpenFor(DateTime date)
        {
            return date.Day >= 1 && date.Day <= 15;
        }
    }
}
