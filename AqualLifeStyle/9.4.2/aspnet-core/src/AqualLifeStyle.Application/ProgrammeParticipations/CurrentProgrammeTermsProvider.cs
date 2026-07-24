using System;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface ICurrentProgrammeTermsProvider
    {
        EntryProgrammeTerms GetEntryTerms();
        OnyxPlanTerms GetDirectOnyxTerms();
    }

    public class CurrentProgrammeTermsProvider : ICurrentProgrammeTermsProvider, ITransientDependency
    {
        private static readonly DateTime EffectiveFrom =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        public EntryProgrammeTerms GetEntryTerms()
        {
            return EntryProgrammeTerms.Create(
                version: "2026-07",
                effectiveFrom: EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
        }

        public OnyxPlanTerms GetDirectOnyxTerms()
        {
            return OnyxPlanTerms.Create(
                version: "2026-07",
                effectiveFrom: EffectiveFrom,
                directEntryAmount: 6120m);
        }
    }
}
