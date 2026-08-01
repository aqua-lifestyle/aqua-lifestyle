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
        private static readonly DateTime OnyxEffectiveFrom =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly DateTime AQGreenSinglePaymentEffectiveFrom =
            new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        public EntryProgrammeTerms GetEntryTerms()
        {
            return EntryProgrammeTerms.CreateSingleJoiningPayment(
                version: "2026-08-single-1200",
                effectiveFrom: AQGreenSinglePaymentEffectiveFrom,
                joiningPaymentAmount: 1200m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
        }

        public OnyxPlanTerms GetDirectOnyxTerms()
        {
            return OnyxPlanTerms.Create(
                version: "2026-07",
                effectiveFrom: OnyxEffectiveFrom,
                directEntryAmount: 6120m);
        }
    }
}
