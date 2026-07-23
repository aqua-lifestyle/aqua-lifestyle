using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class EntryWeeklyCommissionCalculator
    {
        private readonly EntryNetworkQualificationEvaluator _networkQualificationEvaluator;

        public EntryWeeklyCommissionCalculator(
            EntryNetworkQualificationEvaluator networkQualificationEvaluator)
        {
            _networkQualificationEvaluator = networkQualificationEvaluator ??
                throw new ArgumentNullException(nameof(networkQualificationEvaluator));
        }

        public EntryWeeklyCommission Calculate(
            EntryParticipation participation,
            EntryCommissionPeriod period,
            EntryCommissionTerms terms,
            IEnumerable<EntryParticipation> networkParticipations,
            IEnumerable<EntryMonthlyObligation> customerObligations)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (networkParticipations == null)
            {
                throw new ArgumentNullException(nameof(networkParticipations));
            }

            if (customerObligations == null)
            {
                throw new ArgumentNullException(nameof(customerObligations));
            }

            var highestCompletedLevel = (int)_networkQualificationEvaluator.Evaluate(
                    participation.CustomerId,
                    networkParticipations);
            var hasOverdueOwnObligation = customerObligations.Any(obligation =>
                obligation.EntryParticipationId == participation.Id &&
                obligation.Status == EntryMonthlyObligationStatus.Overdue);

            return EntryWeeklyCommission.RecordCalculation(
                participation,
                period,
                terms,
                highestCompletedLevel,
                !hasOverdueOwnObligation);
        }
    }
}
