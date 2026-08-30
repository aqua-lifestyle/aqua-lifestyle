using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenV2GraduationEvidenceReplayValidator
        : IAQGreenV2GraduationEvidenceReplayValidator, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public AQGreenV2GraduationEvidenceReplayValidator(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<AQGreenV2GraduationEvidenceReplayResult> ValidateAsync(
            Guid graduationDecisionId,
            CancellationToken cancellationToken = default)
        {
            if (graduationDecisionId == Guid.Empty)
                throw new ArgumentException(
                    "A graduation decision identity is required.",
                    nameof(graduationDecisionId));

            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
                throw new NotSupportedException(
                    "AQGreen V2 graduation evidence replay requires PostgreSQL.");

            var decision = await context.OnyxGraduationDecisions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == graduationDecisionId,
                    cancellationToken);
            if (decision == null)
                throw new AQGreenGraduationEvidenceReplayException(
                    "The graduation decision is missing.");

            var evidence = await context.AQGreenV2GraduationEvidence
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == graduationDecisionId,
                    cancellationToken);
            if (evidence == null)
                throw new AQGreenGraduationEvidenceReplayException(
                    "The Placement V2 graduation evidence header is missing.");

            var nodes = await context.AQGreenV2GraduationEvidenceNodes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(node => node.EvidenceId == graduationDecisionId)
                .OrderBy(node => node.CanonicalOrdinal)
                .ToListAsync(cancellationToken);
            var sourcePlacementIds = nodes
                .Select(node => node.SourcePlacementId)
                .Distinct()
                .ToList();
            var placements = await context.AQGreenNetworkPlacements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(placement => sourcePlacementIds.Contains(placement.Id))
                .ToListAsync(cancellationToken);

            return AQGreenV2GraduationEvidenceReplay.Validate(
                decision,
                evidence,
                nodes,
                placements);
        }
    }
}
