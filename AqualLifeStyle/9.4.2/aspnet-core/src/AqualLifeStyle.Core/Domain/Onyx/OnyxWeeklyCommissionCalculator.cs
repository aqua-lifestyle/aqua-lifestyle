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

            return Calculate(
                participation,
                period,
                terms,
                EffectiveProgrammeNetwork.BuildOnyx(
                    networkParticipations,
                    period.PeriodEnd));
        }

        public OnyxWeeklyCommission Calculate(
            OnyxParticipation participation,
            OnyxCommissionPeriod period,
            OnyxCommissionTerms terms,
            EffectiveProgrammeNetwork network)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (network == null) throw new ArgumentNullException(nameof(network));

            var highestQualifiedNetworkLevel = _networkQualificationEvaluator.Evaluate(
                participation.CustomerId,
                network);
            return OnyxWeeklyCommission.RecordCalculation(
                participation,
                period,
                terms,
                highestQualifiedNetworkLevel,
                highestQualifiedNetworkLevel);
        }
    }
}
