using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum OnyxTravelBenefitStatus
    {
        WaitingPeriod = 0,
        Active = 1
    }

    public class OnyxTravelBenefitEntitlement
        : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public Guid OnyxParticipationId { get; private set; }
        public int CustomerId { get; private set; }
        public OnyxNetworkLevel QualifiedNetworkLevel { get; private set; }
        public OnyxNetworkLevel RequiredNetworkLevel { get; private set; }
        public DateTime EligibleAt { get; private set; }
        public DateTime WaitingPeriodEndsAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public OnyxTravelBenefitStatus Status { get; private set; }
        public int WaitingPeriodMonths { get; private set; }
        public decimal MemberTripContributionPercent { get; private set; }
        public string TermsVersion { get; private set; }
        public DateTime TermsEffectiveFrom { get; private set; }

        protected OnyxTravelBenefitEntitlement()
        {
        }

        private OnyxTravelBenefitEntitlement(
            OnyxParticipation participation,
            OnyxNetworkLevel qualifiedNetworkLevel,
            OnyxTravelBenefitTerms terms,
            DateTime eligibleAt)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (participation.Status != OnyxParticipationStatus.Active)
            {
                throw new InvalidOperationException(
                    "Travel eligibility requires active Onyx participation.");
            }

            if (qualifiedNetworkLevel < terms.RequiredNetworkLevel)
            {
                throw new InvalidOperationException(
                    "The Club Member must complete Onyx Level 3 before receiving the travel benefit.");
            }

            if (eligibleAt == default || eligibleAt < terms.EffectiveFrom)
            {
                throw new ArgumentException(
                    "The eligibility time must fall within the applicable travel benefit terms.",
                    nameof(eligibleAt));
            }

            if (participation.ActivatedAt.HasValue &&
                eligibleAt < participation.ActivatedAt.Value)
            {
                throw new ArgumentException(
                    "Travel eligibility cannot precede Onyx activation.",
                    nameof(eligibleAt));
            }

            Id = Guid.NewGuid();
            TenantId = participation.TenantId;
            OnyxParticipationId = participation.Id;
            CustomerId = participation.CustomerId;
            QualifiedNetworkLevel = qualifiedNetworkLevel;
            RequiredNetworkLevel = terms.RequiredNetworkLevel;
            EligibleAt = eligibleAt;
            WaitingPeriodMonths = terms.WaitingPeriodMonths;
            WaitingPeriodEndsAt = eligibleAt.AddMonths(terms.WaitingPeriodMonths);
            MemberTripContributionPercent = terms.MemberTripContributionPercent;
            TermsVersion = terms.Version;
            TermsEffectiveFrom = terms.EffectiveFrom;
            Status = OnyxTravelBenefitStatus.WaitingPeriod;
        }

        public static OnyxTravelBenefitEntitlement GrantForQualifiedParticipant(
            OnyxParticipation participation,
            IEnumerable<OnyxParticipation> networkParticipations,
            OnyxNetworkQualificationEvaluator networkQualificationEvaluator,
            OnyxTravelBenefitTerms terms,
            DateTime eligibleAt)
        {
            if (networkQualificationEvaluator == null)
            {
                throw new ArgumentNullException(nameof(networkQualificationEvaluator));
            }

            var qualifiedNetworkLevel = networkQualificationEvaluator.Evaluate(
                participation,
                networkParticipations);

            return new OnyxTravelBenefitEntitlement(
                participation,
                qualifiedNetworkLevel,
                terms,
                eligibleAt);
        }

        public bool IsWaitingPeriodComplete(DateTime asOf)
        {
            if (asOf == default)
            {
                throw new ArgumentException("A status time is required.", nameof(asOf));
            }

            return asOf >= WaitingPeriodEndsAt;
        }

        public void ActivateAfterWaitingPeriod(DateTime activatedAt)
        {
            if (Status == OnyxTravelBenefitStatus.Active)
            {
                if (ActivatedAt != activatedAt)
                {
                    throw new InvalidOperationException(
                        "This travel benefit was already activated at a different time.");
                }

                return;
            }

            if (!IsWaitingPeriodComplete(activatedAt))
            {
                throw new InvalidOperationException(
                    "The three-month travel benefit waiting period is not complete.");
            }

            ActivatedAt = activatedAt;
            Status = OnyxTravelBenefitStatus.Active;
        }
    }
}
