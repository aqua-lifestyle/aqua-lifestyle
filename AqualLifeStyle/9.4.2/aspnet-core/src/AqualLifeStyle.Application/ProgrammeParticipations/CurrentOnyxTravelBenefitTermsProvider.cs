using System;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface ICurrentOnyxTravelBenefitTermsProvider
    {
        OnyxTravelBenefitTerms GetTerms();
    }

    public class CurrentOnyxTravelBenefitTermsProvider
        : ICurrentOnyxTravelBenefitTermsProvider, ITransientDependency
    {
        public OnyxTravelBenefitTerms GetTerms() =>
            OnyxTravelBenefitTerms.Create(
                version: "2026-07",
                effectiveFrom: new DateTime(
                    2026,
                    7,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                requiredNetworkLevel: OnyxNetworkLevel.Level3,
                waitingPeriodMonths: 3,
                memberTripContributionPercent: 10m);
    }
}
