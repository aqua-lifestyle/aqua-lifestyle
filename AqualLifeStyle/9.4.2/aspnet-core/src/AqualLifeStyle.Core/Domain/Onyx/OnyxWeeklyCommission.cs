using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    public class OnyxWeeklyCommission : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        private readonly List<OnyxCommissionComponent> _components = new();

        public int TenantId { get; set; }
        public Guid OnyxParticipationId { get; private set; }
        public int CustomerId { get; private set; }
        public Guid CommissionPeriodId { get; private set; }
        public int HighestQualifiedNetworkLevel { get; private set; }
        public int HighestCommissionedLevel { get; private set; }
        public decimal TotalAmount { get; private set; }
        public string Currency { get; private set; }
        public string RulesVersion { get; private set; }
        public DateTime CalculatedAt { get; private set; }
        public WeeklyCommissionPayoutStatus PayoutStatus { get; private set; }
        public DateTime? ReleasedAt { get; private set; }
        public string ReleaseReason { get; private set; }
        public DateTime? PaidAt { get; private set; }
        public string PaymentReference { get; private set; }
        public IReadOnlyCollection<OnyxCommissionComponent> Components =>
            _components.AsReadOnly();

        protected OnyxWeeklyCommission()
        {
        }

        private OnyxWeeklyCommission(
            OnyxParticipation participation,
            OnyxCommissionPeriod period,
            OnyxCommissionTerms terms,
            OnyxNetworkLevel highestQualifiedNetworkLevel,
            OnyxNetworkLevel highestCommissionedLevel)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (period == null) throw new ArgumentNullException(nameof(period));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (participation.Status != OnyxParticipationStatus.Active)
            {
                throw new InvalidOperationException(
                    "Weekly commission can only be calculated for an active Onyx participant.");
            }

            if (participation.TenantId != period.TenantId)
            {
                throw new InvalidOperationException(
                    "The participation and commission period must belong to the same Area.");
            }

            if (!string.Equals(period.RulesVersion, terms.Version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The Onyx commission terms do not match the period rules version.");
            }

            if (highestQualifiedNetworkLevel < OnyxNetworkLevel.None ||
                highestQualifiedNetworkLevel > OnyxNetworkLevel.Level5)
            {
                throw new ArgumentOutOfRangeException(nameof(highestQualifiedNetworkLevel));
            }

            if (highestCommissionedLevel < OnyxNetworkLevel.None ||
                highestCommissionedLevel > OnyxNetworkLevel.Level5 ||
                highestCommissionedLevel > highestQualifiedNetworkLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(highestCommissionedLevel));
            }

            Id = Guid.NewGuid();
            TenantId = participation.TenantId;
            OnyxParticipationId = participation.Id;
            CustomerId = participation.CustomerId;
            CommissionPeriodId = period.Id;
            HighestQualifiedNetworkLevel = (int)highestQualifiedNetworkLevel;
            HighestCommissionedLevel = (int)highestCommissionedLevel;
            Currency = terms.Currency;
            RulesVersion = terms.Version;
            CalculatedAt = period.CalculatedAt;

            if (highestCommissionedLevel > OnyxNetworkLevel.None)
            {
                for (var level = OnyxNetworkLevel.Level1;
                     level <= highestCommissionedLevel;
                     level++)
                {
                    var amount = terms.GetLevelComponentAmount(level);
                    _components.Add(OnyxCommissionComponent.Create(level, amount));
                    TotalAmount += amount;
                }

                PayoutStatus = WeeklyCommissionPayoutStatus.Earned;
            }
            else
            {
                TotalAmount = 0m;
                PayoutStatus = WeeklyCommissionPayoutStatus.NotEarned;
            }
        }

        internal static OnyxWeeklyCommission RecordCalculation(
            OnyxParticipation participation,
            OnyxCommissionPeriod period,
            OnyxCommissionTerms terms,
            OnyxNetworkLevel highestQualifiedNetworkLevel,
            OnyxNetworkLevel highestCommissionedLevel)
        {
            return new OnyxWeeklyCommission(
                participation,
                period,
                terms,
                highestQualifiedNetworkLevel,
                highestCommissionedLevel);
        }

        public void ReleaseEligiblePayout(DateTime releasedAt)
        {
            const string releaseReason = "Eligible Onyx weekly commission released.";
            if (PayoutStatus == WeeklyCommissionPayoutStatus.Released ||
                PayoutStatus == WeeklyCommissionPayoutStatus.Paid)
            {
                if (ReleasedAt != releasedAt ||
                    !string.Equals(ReleaseReason, releaseReason, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "This commission was already released using different release facts.");
                }

                return;
            }

            if (PayoutStatus != WeeklyCommissionPayoutStatus.Earned)
            {
                throw new InvalidOperationException(
                    "Only an earned Onyx commission can be released.");
            }

            if (releasedAt == default || releasedAt < CalculatedAt)
            {
                throw new ArgumentException(
                    "The release time cannot precede commission calculation.",
                    nameof(releasedAt));
            }

            ReleasedAt = releasedAt;
            ReleaseReason = releaseReason;
            PayoutStatus = WeeklyCommissionPayoutStatus.Released;
        }

        public void MarkPaid(DateTime paidAt, string paymentReference)
        {
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
                    !string.Equals(PaymentReference, normalizedReference, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "This commission was already paid using different payment facts.");
                }

                return;
            }

            if (PayoutStatus != WeeklyCommissionPayoutStatus.Released)
            {
                throw new InvalidOperationException(
                    "An Onyx commission must be released before it can be marked paid.");
            }

            if (paidAt == default || paidAt < ReleasedAt.Value)
            {
                throw new ArgumentException(
                    "The payment time cannot precede release.",
                    nameof(paidAt));
            }

            PaidAt = paidAt;
            PaymentReference = normalizedReference;
            PayoutStatus = WeeklyCommissionPayoutStatus.Paid;
        }
    }

    public class OnyxCommissionComponent : Entity<Guid>
    {
        public int Level { get; private set; }
        public decimal Amount { get; private set; }

        protected OnyxCommissionComponent()
        {
        }

        private OnyxCommissionComponent(OnyxNetworkLevel level, decimal amount)
        {
            Id = Guid.NewGuid();
            Level = (int)level;
            Amount = amount;
        }

        internal static OnyxCommissionComponent Create(
            OnyxNetworkLevel level,
            decimal amount)
        {
            if (level < OnyxNetworkLevel.Level1 ||
                level > OnyxNetworkLevel.Level5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level),
                    "An Onyx commission component must be for Levels 1 through 5.");
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return new OnyxCommissionComponent(level, amount);
        }
    }
}
