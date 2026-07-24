using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public class OnyxTravelBenefitEligibilityProcessor : ITransientDependency
    {
        private readonly IRepository<OnyxTravelBenefitEntitlement, Guid>
            _entitlementRepository;
        private readonly ICurrentOnyxTravelBenefitTermsProvider _termsProvider;

        public OnyxTravelBenefitEligibilityProcessor(
            IRepository<OnyxTravelBenefitEntitlement, Guid> entitlementRepository,
            ICurrentOnyxTravelBenefitTermsProvider termsProvider)
        {
            _entitlementRepository = entitlementRepository;
            _termsProvider = termsProvider;
        }

        public async Task<OnyxTravelBenefitEligibilityResult> SynchronizeAsync(
            int tenantId,
            IReadOnlyCollection<OnyxParticipation> networkParticipations,
            DateTime processedAt)
        {
            if (tenantId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenantId));
            }

            if (networkParticipations == null)
            {
                throw new ArgumentNullException(nameof(networkParticipations));
            }

            if (processedAt == default)
            {
                throw new ArgumentException(
                    "A travel benefit processing time is required.",
                    nameof(processedAt));
            }

            var targetParticipations = networkParticipations
                .Where(participation =>
                    participation.TenantId == tenantId &&
                    participation.Status == OnyxParticipationStatus.Active)
                .ToList();
            var targetIds = targetParticipations
                .Select(participation => participation.Id)
                .ToList();
            var existingEntitlements = await _entitlementRepository.GetAllListAsync(
                entitlement => targetIds.Contains(entitlement.OnyxParticipationId));
            var existingByParticipation = existingEntitlements.ToDictionary(
                entitlement => entitlement.OnyxParticipationId);
            var evaluator = new OnyxNetworkQualificationEvaluator();
            var terms = _termsProvider.GetTerms();
            var grantedCount = 0;
            var activatedCount = 0;

            foreach (var participation in targetParticipations)
            {
                if (existingByParticipation.TryGetValue(
                        participation.Id,
                        out var existing))
                {
                    if (existing.Status == OnyxTravelBenefitStatus.WaitingPeriod &&
                        existing.IsWaitingPeriodComplete(processedAt))
                    {
                        existing.ActivateAfterWaitingPeriod(processedAt);
                        activatedCount++;
                    }

                    continue;
                }

                var qualifiedLevel = evaluator.Evaluate(
                    participation,
                    networkParticipations);
                if (qualifiedLevel < terms.RequiredNetworkLevel)
                {
                    continue;
                }

                var entitlement =
                    OnyxTravelBenefitEntitlement.GrantForQualifiedParticipant(
                        participation,
                        networkParticipations,
                        evaluator,
                        terms,
                        processedAt);
                await _entitlementRepository.InsertAsync(entitlement);
                grantedCount++;
            }

            return new OnyxTravelBenefitEligibilityResult(
                grantedCount,
                activatedCount);
        }
    }

    public sealed class OnyxTravelBenefitEligibilityResult
    {
        public int GrantedCount { get; }
        public int ActivatedCount { get; }

        public OnyxTravelBenefitEligibilityResult(
            int grantedCount,
            int activatedCount)
        {
            GrantedCount = grantedCount;
            ActivatedCount = activatedCount;
        }
    }
}
