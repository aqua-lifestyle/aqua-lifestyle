using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface IOnyxTravelBenefitSynchronizer : ITransientDependency
    {
        Task<OnyxTravelBenefitEligibilityResult> SynchronizeAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime processedAt);
    }

    /// <summary>
    /// Synchronizes travel entitlement from the authoritative closed Onyx
    /// network snapshot, independently from weekly commission calculation.
    /// </summary>
    public sealed class OnyxTravelBenefitSynchronizer
        : IOnyxTravelBenefitSynchronizer
    {
        private readonly IRepository<OnyxParticipation, Guid>
            _participationRepository;
        private readonly OnyxTravelBenefitEligibilityProcessor
            _eligibilityProcessor;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public OnyxTravelBenefitSynchronizer(
            IRepository<OnyxParticipation, Guid> participationRepository,
            OnyxTravelBenefitEligibilityProcessor eligibilityProcessor,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _participationRepository = participationRepository;
            _eligibilityProcessor = eligibilityProcessor;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<OnyxTravelBenefitEligibilityResult> SynchronizeAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime processedAt)
        {
            if (closedWeek == null)
            {
                throw new ArgumentNullException(nameof(closedWeek));
            }

            System.Collections.Generic.List<OnyxParticipation> networkParticipations;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                networkParticipations = await _participationRepository
                    .GetAllIncluding(participation => participation.RecruiterCorrections)
                    .Where(participation =>
                        participation.TenantId == tenantId &&
                        participation.Status == OnyxParticipationStatus.Active &&
                        (!participation.ActivatedAt.HasValue ||
                         participation.ActivatedAt <= closedWeek.PeriodEndUtc))
                    .ToListAsync();
            }
            if (networkParticipations.Any(participation => participation.IsDeleted))
            {
                throw new InvalidOperationException(
                    "Onyx travel synchronization cannot prove cutoff participation state because deleted network evidence exists.");
            }
            var effectiveNetwork = EffectiveProgrammeNetwork.BuildOnyx(
                tenantId,
                networkParticipations,
                closedWeek.PeriodEndUtc);

            return await _eligibilityProcessor.SynchronizeAsync(
                tenantId,
                networkParticipations,
                effectiveNetwork,
                closedWeek.PeriodEndUtc,
                processedAt);
        }
    }
}
