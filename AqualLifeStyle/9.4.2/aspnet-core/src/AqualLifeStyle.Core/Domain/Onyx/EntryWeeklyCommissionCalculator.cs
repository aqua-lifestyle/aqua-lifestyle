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

            return Calculate(
                participation,
                period,
                terms,
                EffectiveProgrammeNetwork.BuildAQGreen(
                    networkParticipations,
                    period.PeriodEnd),
                customerObligations,
                customerLoanAgreements);
        }

        public EntryWeeklyCommission Calculate(
            EntryParticipation participation,
            EntryCommissionPeriod period,
            EntryCommissionTerms terms,
            EffectiveProgrammeNetwork network,
            IEnumerable<EntryMonthlyObligation> customerObligations,
            IEnumerable<OnyxLoanAgreement> customerLoanAgreements = null)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (customerObligations == null)
            {
                throw new ArgumentNullException(nameof(customerObligations));
            }

            var highestQualifiedNetworkLevel = _networkQualificationEvaluator.Evaluate(
                participation.CustomerId,
                network);
            var hasOverdueOwnObligation = customerObligations.Any(obligation =>
                obligation.EntryParticipationId == participation.Id &&
                obligation.WasOverdueAt(period.PeriodEnd));
            var hasOverdueOwnLoan = (customerLoanAgreements ??
                    Array.Empty<OnyxLoanAgreement>())
                .Any(agreement =>
                    agreement.EntryParticipationId == participation.Id &&
                    agreement.WasRequiringPayoutHoldAt(period.PeriodEnd));

            var holdReasons = new List<string>();
            if (hasOverdueOwnObligation)
            {
                holdReasons.Add("AQGreen monthly commitment is overdue.");
            }

            if (hasOverdueOwnLoan)
            {
                holdReasons.Add("Onyx loan repayment is overdue.");
            }

            return EntryWeeklyCommission.RecordCalculation(
                participation,
                period,
                terms,
                highestQualifiedNetworkLevel,
                holdReasons.Count == 0 ? null : string.Join(" ", holdReasons));
        }
    }
}
