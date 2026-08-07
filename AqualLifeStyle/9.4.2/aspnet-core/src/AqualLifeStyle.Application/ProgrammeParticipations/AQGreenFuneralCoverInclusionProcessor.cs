using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public class AQGreenFuneralCoverInclusionResult
    {
        public bool Included { get; }
        public Guid? EntitlementId { get; }

        public AQGreenFuneralCoverInclusionResult(bool included, Guid? entitlementId)
        {
            Included = included;
            EntitlementId = entitlementId;
        }
    }

    /// <summary>
    /// Records the R30,000 funeral-cover benefit as included for an AQGreen
    /// participation whose R1,200 joining obligation is satisfied. Idempotent:
    /// a participation is granted the entitlement at most once, regardless of how
    /// many confirmed joining-payment confirmations arrive.
    /// </summary>
    public class AQGreenFuneralCoverInclusionProcessor : ITransientDependency
    {
        private readonly IRepository<AQGreenFuneralCoverEntitlement, Guid>
            _entitlementRepository;
        private readonly ICurrentAQGreenFuneralCoverTermsProvider _termsProvider;

        public AQGreenFuneralCoverInclusionProcessor(
            IRepository<AQGreenFuneralCoverEntitlement, Guid> entitlementRepository,
            ICurrentAQGreenFuneralCoverTermsProvider termsProvider)
        {
            _entitlementRepository = entitlementRepository;
            _termsProvider = termsProvider;
        }

        public async Task<AQGreenFuneralCoverInclusionResult> EnsureIncludedAsync(
            EntryParticipation participation,
            DateTime processedAt)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (processedAt == default)
            {
                throw new ArgumentException(
                    "A funeral-cover inclusion time is required.",
                    nameof(processedAt));
            }

            if (!participation.IsJoiningObligationSatisfied)
            {
                return new AQGreenFuneralCoverInclusionResult(false, null);
            }

            var existing = await _entitlementRepository.FirstOrDefaultAsync(
                entitlement =>
                    entitlement.EntryParticipationId == participation.Id);
            if (existing != null)
            {
                return new AQGreenFuneralCoverInclusionResult(
                    existing.Status == AQGreenFuneralCoverStatus.Included,
                    existing.Id);
            }

            var terms = _termsProvider.GetTerms();
            if (processedAt < terms.EffectiveFrom)
            {
                return new AQGreenFuneralCoverInclusionResult(false, null);
            }

            var entitlement = AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                participation,
                terms,
                processedAt);
            await _entitlementRepository.InsertAsync(entitlement);
            return new AQGreenFuneralCoverInclusionResult(true, entitlement.Id);
        }
    }
}
