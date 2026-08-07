using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum AQGreenFuneralCoverStatus
    {
        /// <summary>
        /// The benefit is recorded as included/eligible because the Club Member
        /// satisfied the R1,200 AQGreen joining obligation. Activation or
        /// enrolment is deliberately never encoded (Product Decision PD-06).
        /// </summary>
        Included = 0
    }

    /// <summary>
    /// Records that a Club Member became entitled to the R30,000 funeral-cover
    /// benefit by completing the AQGreen joining obligation (R1,200 once, or two
    /// R600 instalments). The record is appended once per AQGreen participation
    /// and never encodes insurance activation or a waiting period, which remain
    /// unresolved (PD-06).
    /// </summary>
    public class AQGreenFuneralCoverEntitlement
        : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public Guid EntryParticipationId { get; private set; }
        public int CustomerId { get; private set; }
        public decimal FuneralCoverAmount { get; private set; }
        public string Currency { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime IncludedAt { get; private set; }
        public AQGreenFuneralCoverStatus Status { get; private set; }

        protected AQGreenFuneralCoverEntitlement()
        {
        }

        private AQGreenFuneralCoverEntitlement(
            EntryParticipation participation,
            AQGreenFuneralCoverTerms terms,
            DateTime includedAt)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            if (!participation.IsJoiningObligationSatisfied)
            {
                throw new InvalidOperationException(
                    "The funeral-cover benefit is included only after the R1,200 AQGreen joining obligation is satisfied.");
            }

            if (includedAt == default || includedAt < terms.EffectiveFrom)
            {
                throw new ArgumentException(
                    "The inclusion time must fall within the applicable funeral-cover terms.",
                    nameof(includedAt));
            }

            Id = Guid.NewGuid();
            TenantId = participation.TenantId;
            EntryParticipationId = participation.Id;
            CustomerId = participation.CustomerId;
            FuneralCoverAmount = terms.FuneralCoverAmount;
            Currency = terms.Currency;
            TermsVersion = terms.Version;
            IncludedAt = includedAt;
            Status = AQGreenFuneralCoverStatus.Included;
        }

        public static AQGreenFuneralCoverEntitlement GrantForJoiningCompletion(
            EntryParticipation participation,
            AQGreenFuneralCoverTerms terms,
            DateTime includedAt)
        {
            return new AQGreenFuneralCoverEntitlement(
                participation,
                terms,
                includedAt);
        }
    }
}
