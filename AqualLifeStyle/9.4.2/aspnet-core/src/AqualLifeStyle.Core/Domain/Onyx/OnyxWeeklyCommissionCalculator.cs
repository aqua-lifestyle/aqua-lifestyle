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

            var highestQualifiedNetworkLevel = _networkQualificationEvaluator.Evaluate(
                participation,
                networkParticipations);
            var highestCommissionedLevel =
                highestQualifiedNetworkLevel >= OnyxNetworkLevel.Level1
                    ? OnyxNetworkLevel.Level1
                    : OnyxNetworkLevel.None;

            return OnyxWeeklyCommission.RecordCalculation(
                participation,
                period,
                terms,
                highestQualifiedNetworkLevel,
                highestCommissionedLevel);
        }
    }
}
