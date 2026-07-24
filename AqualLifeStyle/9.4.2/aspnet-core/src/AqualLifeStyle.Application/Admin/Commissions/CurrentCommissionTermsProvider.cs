using System;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public interface ICurrentCommissionTermsProvider
    {
        EntryCommissionTerms GetEntryTerms();
        OnyxCommissionTerms GetOnyxTerms();
    }

    public class CurrentCommissionTermsProvider
        : ICurrentCommissionTermsProvider, ITransientDependency
    {
        private static readonly DateTime EffectiveFrom =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        public EntryCommissionTerms GetEntryTerms() =>
            EntryCommissionTerms.Create(
                version: "2026-07",
                effectiveFrom: EffectiveFrom,
                levelOneComponentAmount: 150m,
                levelTwoComponentAmount: 250m,
                levelThreeComponentAmount: 1250m);

        public OnyxCommissionTerms GetOnyxTerms() =>
            OnyxCommissionTerms.Create(
                version: "2026-07",
                effectiveFrom: EffectiveFrom,
                levelOneCommissionAmount: 250m);
    }
}
