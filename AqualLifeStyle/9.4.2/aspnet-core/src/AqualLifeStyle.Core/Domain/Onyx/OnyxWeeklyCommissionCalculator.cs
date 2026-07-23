using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Domain.Onyx
{
    public sealed class OnyxWeeklyCommissionCalculator
    {
        private readonly OnyxNetworkQualificationEvaluator _networkQualificationEvaluator;

        public OnyxWeeklyCommissionCalculator(
            OnyxNetworkQualificationEvaluator networkQualificationEvaluator)
        {
            _networkQualificationEvaluator = networkQualificationEvaluator ??
                throw new ArgumentNullException(nameof(networkQualificationEvaluator));
        }

        public OnyxWeeklyCommission Calculate(
            OnyxParticipation participation,
            OnyxCommissionPeriod period,
            OnyxCommissionTerms terms,
            IEnumerable<OnyxParticipation> networkParticipations)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (networkParticipations == null)
            {
                throw new ArgumentNullException(nameof(networkParticipations));
            }

            var highestCompletedLevel = _networkQualificationEvaluator.Evaluate(
                participation,
                networkParticipations);

            return OnyxWeeklyCommission.RecordCalculation(
                participation,
                period,
                terms,
                highestCompletedLevel);
        }
    }
}
