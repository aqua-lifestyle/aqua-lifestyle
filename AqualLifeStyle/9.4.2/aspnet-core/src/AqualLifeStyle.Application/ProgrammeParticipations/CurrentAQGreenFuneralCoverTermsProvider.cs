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
    /// Supplies the R30,000 inclusion terms for the modern AQGreen joining
    /// lifecycle. The Aqua promise predates this implementation. The 2026-07-26
    /// lower bound identifies the modern application joining model only; it is
    /// not the promise inception, software deployment, or insurer activation
    /// date.
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
