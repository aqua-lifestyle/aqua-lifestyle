using System;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface ICurrentAQGreenFuneralCoverTermsProvider
    {
        AQGreenFuneralCoverTerms GetTerms();
    }

    /// <summary>
    /// Supplies the confirmed R30,000 funeral-cover benefit terms that apply to
    /// AQGreen joining completions from 2026-07-26.
    /// </summary>
    public class CurrentAQGreenFuneralCoverTermsProvider
        : ICurrentAQGreenFuneralCoverTermsProvider, ITransientDependency
    {
        public AQGreenFuneralCoverTerms GetTerms() =>
            AQGreenFuneralCoverTerms.Create(
                version: "2026-08-funeral-30000",
                effectiveFrom: new DateTime(
                    2026,
                    7,
                    26,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                funeralCoverAmount: 30000m);
    }
}
