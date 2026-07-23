using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    public class EntryWeeklyCommission : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<EntryCommissionComponent> _components = new();

        public int TenantId { get; set; }
        public Guid EntryParticipationId { get; private set; }
        public int CustomerId { get; private set; }
        public Guid CommissionPeriodId { get; private set; }
        public int HighestCompletedLevel { get; private set; }
        public decimal TotalAmount { get; private set; }
        public string Currency { get; private set; }
        public string RulesVersion { get; private set; }
        public DateTime CalculatedAt { get; private set; }
        public WeeklyCommissionPayoutStatus PayoutStatus { get; private set; }
        public string HoldReason { get; private set; }
        public DateTime? ReleasedAt { get; private set; }
        public string ReleaseReason { get; private set; }
        public DateTime? PaidAt { get; private set; }
        public string PaymentReference { get; private set; }
        public IReadOnlyCollection<EntryCommissionComponent> Components =>
            _components.AsReadOnly();

        protected EntryWeeklyCommission()
        {
        }

        private EntryWeeklyCommission(
            EntryParticipation participation,
            EntryCommissionPeriod period,
            EntryCommissionTerms terms,
            int highestCompletedLevel,
            string holdReason)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (period == null)
            {
                throw new ArgumentNullException(nameof(period));
            }

            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            if (!participation.IsQualifiedForNetwork)
            {
                throw new InvalidOperationException(
                    "Weekly commission can only be calculated for an active Entry participant.");
            }

            if (participation.TenantId != period.TenantId)
            {
                throw new InvalidOperationException(
                    "The participation and commission period must belong to the same Area.");
            }

            if (!string.Equals(period.RulesVersion, terms.Version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The commission terms do not match the period rules version.");
            }

            if (highestCompletedLevel < 0 ||
                highestCompletedLevel > EntryNetworkQualificationEvaluator.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(highestCompletedLevel));
            }

            Id = Guid.NewGuid();
            TenantId = participation.TenantId;
            EntryParticipationId = participation.Id;
            CustomerId = participation.CustomerId;
            CommissionPeriodId = period.Id;
            HighestCompletedLevel = highestCompletedLevel;
            Currency = terms.Currency;
            RulesVersion = terms.Version;
            CalculatedAt = period.CalculatedAt;

            for (var level = 1; level <= highestCompletedLevel; level++)
            {
                _components.Add(EntryCommissionComponent.Create(
                    level,
                    terms.GetComponentAmount(level)));
            }

            TotalAmount = _components.Sum(component => component.Amount);
            if (highestCompletedLevel == 0)
            {
                PayoutStatus = WeeklyCommissionPayoutStatus.NotEarned;
            }
            else if (string.IsNullOrWhiteSpace(holdReason))
            {
                PayoutStatus = WeeklyCommissionPayoutStatus.Earned;
            }
            else
            {
                PayoutStatus = WeeklyCommissionPayoutStatus.Held;
                HoldReason = NormalizeHoldReason(holdReason);
            }
        }

        internal static EntryWeeklyCommission RecordCalculation(
            EntryParticipation participation,
            EntryCommissionPeriod period,
            EntryCommissionTerms terms,
            int highestCompletedLevel,
            string holdReason)
        {
            return new EntryWeeklyCommission(
                participation,
                period,
                terms,
                highestCompletedLevel,
                holdReason);
        }

        public void HoldPayout(string reason)
        {
            var normalizedReason = NormalizeHoldReason(reason);
            if (PayoutStatus == WeeklyCommissionPayoutStatus.Held)
            {
                if (!string.Equals(HoldReason, normalizedReason, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "This commission was already held using a different reason.");
                }

                return;
            }

            if (PayoutStatus != WeeklyCommissionPayoutStatus.Earned)
            {
                throw new InvalidOperationException(
                    "Only an earned commission can be placed on hold.");
            }

            HoldReason = normalizedReason;
            PayoutStatus = WeeklyCommissionPayoutStatus.Held;
        }

        public void ReleaseEligiblePayout(DateTime releasedAt)
        {
            if (PayoutStatus == WeeklyCommissionPayoutStatus.Released ||
                PayoutStatus == WeeklyCommissionPayoutStatus.Paid)
            {
                EnsureMatchingRelease(releasedAt, ReleaseReason);
                return;
            }

            if (PayoutStatus != WeeklyCommissionPayoutStatus.Earned)
            {
                throw new InvalidOperationException(
                    "Only an earned eligible commission can be released normally.");
            }

            RecordRelease(releasedAt, "Eligible weekly commission released.");
        }

        public void ReleaseHeldPayoutAfterComplianceRestored(
            DateTime releasedAt,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A release reason is required.", nameof(reason));
            }

            var normalizedReason = reason.Trim();
            if (PayoutStatus == WeeklyCommissionPayoutStatus.Released ||
                PayoutStatus == WeeklyCommissionPayoutStatus.Paid)
            {
                EnsureMatchingRelease(releasedAt, normalizedReason);
                return;
            }

            if (PayoutStatus != WeeklyCommissionPayoutStatus.Held)
            {
                throw new InvalidOperationException(
                    "Only a held commission can be released after compliance is restored.");
            }

            RecordRelease(releasedAt, normalizedReason);
        }

        public void MarkPaid(DateTime paidAt, string paymentReference)
        {
            if (paidAt == default)
            {
                throw new ArgumentException(
                    "The payment time cannot precede release.",
                    nameof(paidAt));
            }

            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                throw new ArgumentException(
                    "A commission payment reference is required.",
                    nameof(paymentReference));
            }

            var normalizedReference = paymentReference.Trim();
            if (PayoutStatus == WeeklyCommissionPayoutStatus.Paid)
            {
                if (PaidAt != paidAt ||
                    !string.Equals(
                        PaymentReference,
                        normalizedReference,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "This commission was already paid using different payment facts.");
                }

                return;
            }

            if (PayoutStatus != WeeklyCommissionPayoutStatus.Released)
            {
                throw new InvalidOperationException(
                    "A commission must be released before it can be marked paid.");
            }

            if (paidAt < ReleasedAt.Value)
            {
                throw new ArgumentException(
                    "The payment time cannot precede release.",
                    nameof(paidAt));
            }

            PaidAt = paidAt;
            PaymentReference = normalizedReference;
            PayoutStatus = WeeklyCommissionPayoutStatus.Paid;
        }

        private void RecordRelease(DateTime releasedAt, string reason)
        {
            if (releasedAt == default || releasedAt < CalculatedAt)
            {
                throw new ArgumentException(
                    "The release time cannot precede commission calculation.",
                    nameof(releasedAt));
            }

            ReleasedAt = releasedAt;
            ReleaseReason = reason;
            PayoutStatus = WeeklyCommissionPayoutStatus.Released;
        }

        private void EnsureMatchingRelease(DateTime releasedAt, string reason)
        {
            if (ReleasedAt != releasedAt ||
                !string.Equals(ReleaseReason, reason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "This commission was already released using different release facts.");
            }
        }

        private static string NormalizeHoldReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A hold reason is required.", nameof(reason));
            }

            var normalizedReason = reason.Trim();
            if (normalizedReason.Length > 500)
            {
                throw new ArgumentException(
                    "The hold reason cannot exceed 500 characters.",
                    nameof(reason));
            }

            return normalizedReason;
        }
    }

    public class EntryCommissionComponent : Entity<Guid>
    {
        public int Level { get; private set; }
        public decimal Amount { get; private set; }

        protected EntryCommissionComponent()
        {
        }

        private EntryCommissionComponent(int level, decimal amount)
        {
            Id = Guid.NewGuid();
            Level = level;
            Amount = amount;
        }

        internal static EntryCommissionComponent Create(int level, decimal amount)
        {
            if (level < 1 || level > EntryNetworkQualificationEvaluator.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return new EntryCommissionComponent(level, amount);
        }
    }
}
