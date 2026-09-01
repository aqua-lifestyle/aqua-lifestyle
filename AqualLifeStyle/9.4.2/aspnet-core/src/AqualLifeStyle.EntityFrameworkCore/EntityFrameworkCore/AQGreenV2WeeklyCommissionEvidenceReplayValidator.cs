using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenV2WeeklyCommissionEvidenceReplayValidator
        : IAQGreenV2WeeklyCommissionEvidenceReplayValidator, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenV2WeeklyCommissionEvidenceReplayValidator(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<AQGreenV2WeeklyCommissionEvidenceReplayResult> ValidateAsync(
            Guid weeklyCommissionId,
            CancellationToken cancellationToken = default)
        {
            if (weeklyCommissionId == Guid.Empty)
                throw new ArgumentException(
                    "A weekly commission identity is required.",
                    nameof(weeklyCommissionId));

            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
                throw new NotSupportedException(
                    "AQGreen V2 weekly commission evidence replay requires PostgreSQL.");

            var commission = await context.EntryWeeklyCommissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(item => item.Components)
                .SingleOrDefaultAsync(
                    item => item.Id == weeklyCommissionId,
                    cancellationToken);
            if (commission == null)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The weekly commission decision is missing.");

            var period = await context.EntryCommissionPeriods
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == commission.CommissionPeriodId,
                    cancellationToken);
            var evidence = await context.AQGreenV2WeeklyCommissionEvidence
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == weeklyCommissionId,
                    cancellationToken);
            if (period == null || evidence == null)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The weekly commission period or Placement V2 evidence is missing.");

            var nodes = await context.AQGreenV2WeeklyCommissionEvidenceNodes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(node => node.EvidenceId == weeklyCommissionId)
                .OrderBy(node => node.CanonicalOrdinal)
                .ToListAsync(cancellationToken);
            var anchorPlacementId = nodes
                .Where(node => node.CanonicalOrdinal == 0)
                .Select(node => (Guid?)node.SourcePlacementId)
                .SingleOrDefault();
            var anchorPlacement = anchorPlacementId.HasValue
                ? await context.AQGreenNetworkPlacements
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        placement =>
                            placement.Id == anchorPlacementId.Value &&
                            placement.TenantId == evidence.TenantId &&
                            placement.PlacementTreeScopeId ==
                                evidence.PlacementTreeScopeId,
                        cancellationToken)
                : null;
            if (anchorPlacement == null)
                throw new AQGreenCommissionEvidenceReplayException(
                    "The immutable source placement anchor is missing or ambiguous.");

            var anchorPath = anchorPlacement.CanonicalPath;
            var placements = await context.AQGreenNetworkPlacements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(placement =>
                    placement.TenantId == evidence.TenantId &&
                    placement.PlacementTreeScopeId == evidence.PlacementTreeScopeId &&
                    placement.PlacedAt <= evidence.Cutoff &&
                    placement.CanonicalPath.StartsWith(anchorPath) &&
                    placement.CanonicalPath.Length - anchorPath.Length <=
                        AQGreenStructuralCompletionCalculator.MaximumLevel)
                .ToListAsync(cancellationToken);
            AQGreenWeeklySalesEligibilityDecision salesDecision = null;
            if (evidence.QualifiedStructuralLevel !=
                AQGreenStructuralCompletionLevel.Level0)
            {
                if (!evidence.WeeklySalesEligibilityDecisionId.HasValue)
                    throw new AQGreenCommissionEvidenceReplayException(
                        "Candidate Placement V2 evidence is missing its weekly-sales decision identity.");
                salesDecision = await context.AQGreenWeeklySalesEligibilityDecisions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(item => item.EvidenceReferences)
                    .SingleOrDefaultAsync(
                        item => item.Id == evidence.WeeklySalesEligibilityDecisionId.Value,
                        cancellationToken);
            }
            var termsVersion = await context.EntryCommissionTermsVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Version == commission.RulesVersion,
                    cancellationToken);

            return AQGreenV2WeeklyCommissionEvidenceReplay.Validate(
                commission,
                period,
                evidence,
                nodes,
                placements,
                salesDecision,
                termsVersion?.ToTerms());
        }
    }
}
