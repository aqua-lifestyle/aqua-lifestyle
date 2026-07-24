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
            IEnumerable<EntryMonthlyObligation> customerObligations,
            IEnumerable<OnyxLoanAgreement> customerLoanAgreements = null)
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
            var hasOverdueOwnLoan = (customerLoanAgreements ??
                    Array.Empty<OnyxLoanAgreement>())
                .Any(agreement =>
                    agreement.EntryParticipationId == participation.Id &&
                    agreement.RequiresPayoutHold);

            var holdReasons = new List<string>();
            if (hasOverdueOwnObligation)
            {
                holdReasons.Add("Entry monthly commitment is overdue.");
            }

            if (hasOverdueOwnLoan)
            {
                holdReasons.Add("Onyx loan repayment is overdue.");
            }

            return EntryWeeklyCommission.RecordCalculation(
                participation,
                period,
                terms,
                highestCompletedLevel,
                holdReasons.Count == 0 ? null : string.Join(" ", holdReasons));
        }
    }
}
