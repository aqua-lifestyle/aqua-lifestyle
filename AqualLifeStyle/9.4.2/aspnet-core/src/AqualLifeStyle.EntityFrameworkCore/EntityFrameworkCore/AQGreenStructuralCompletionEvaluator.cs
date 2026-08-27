using System;
using System.Collections.Generic;
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
    public sealed class AQGreenStructuralCompletionEvaluator
        : IAQGreenStructuralCompletionEvaluator, ITransientDependency
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;
        private readonly IAQGreenPlacementTopologyReader _topologyReader;

        public AQGreenStructuralCompletionEvaluator(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IAQGreenPlacementTopologyReader topologyReader)
        {
            _dbContextProvider = dbContextProvider;
            _topologyReader = topologyReader;
        }

        public async Task<AQGreenStructuralCompletionResult> EvaluateAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            CancellationToken cancellationToken = default)
        {
            EnsureInput(tenantId, participantId, cutoff);
            var context = GetPostgreSqlContext();
            var anchorPlacement = await context.AQGreenNetworkPlacements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    placement => placement.TenantId == tenantId &&
                                 placement.ParticipantId == participantId,
                    cancellationToken);
            if (anchorPlacement == null || anchorPlacement.PlacedAt > cutoff)
            {
                throw new AQGreenStructuralEvaluationNotPlacedException(
                    participantId,
                    cutoff);
            }

            var subtree = await _topologyReader.GetSubtreeInCanonicalOrderAsync(
                tenantId,
                anchorPlacement.PlacementTreeScopeId,
                participantId,
                cancellationToken);
            var structuralNodes = subtree
                .Where(node =>
                    node.RelativeDepth <=
                    AQGreenStructuralCompletionCalculator.MaximumLevel)
                .ToList();
            var structuralParticipantIds = structuralNodes
                .Select(node => node.ParticipantId)
                .ToList();
            var placementFacts = await context.AQGreenNetworkPlacements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(placement =>
                    placement.TenantId == tenantId &&
                    structuralParticipantIds.Contains(placement.ParticipantId))
                .Select(placement => new StructuralPlacementFact
                {
                    ParticipantId = placement.ParticipantId,
                    PlacementTreeScopeId = placement.PlacementTreeScopeId,
                    PlacedAt = placement.PlacedAt
                })
                .ToListAsync(cancellationToken);
            EnsurePlacementFactsMatchTopology(
                structuralNodes,
                placementFacts,
                anchorPlacement.PlacementTreeScopeId);

            var placementByParticipant = placementFacts.ToDictionary(
                fact => fact.ParticipantId);
            var effectiveNodes = structuralNodes
                .Where(node => placementByParticipant[node.ParticipantId].PlacedAt <= cutoff)
                .ToList();
            var effectiveParticipantIds = effectiveNodes
                .Select(node => node.ParticipantId)
                .ToList();
            var participantStates = await (
                    from participation in context.EntryParticipations
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                    join customer in context.Customers
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                        on participation.CustomerId equals customer.Id
                    join user in context.Users
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                        on customer.UserId equals user.Id
                    where effectiveParticipantIds.Contains(participation.Id)
                    select new StructuralParticipantState
                    {
                        ParticipantId = participation.Id,
                        ParticipationTenantId = participation.TenantId,
                        ParticipationStatus = participation.Status,
                        ActivatedAt = participation.ActivatedAt,
                        ParticipationIsDeleted = participation.IsDeleted,
                        CustomerTenantId = customer.TenantId,
                        CustomerIsActive = customer.IsActive,
                        CustomerIsDeleted = customer.IsDeleted,
                        UserTenantId = user.TenantId,
                        UserIsActive = user.IsActive,
                        UserIsDeleted = user.IsDeleted
                    })
                .ToListAsync(cancellationToken);
            EnsureParticipantsQualify(
                tenantId,
                cutoff,
                effectiveParticipantIds,
                participantStates);

            var level = AQGreenStructuralCompletionCalculator.Evaluate(
                relativeDepth => effectiveNodes.Count(node =>
                    node.RelativeDepth == relativeDepth));
            return new AQGreenStructuralCompletionResult(
                participantId,
                anchorPlacement.PlacementTreeScopeId,
                level,
                cutoff,
                AQGreenPlacementRules.CurrentVersion);
        }

        private AqualLifeStyleDbContext GetPostgreSqlContext()
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
            {
                throw new NotSupportedException(
                    "AQGreen structural completion evaluation requires PostgreSQL.");
            }

            return context;
        }

        private static void EnsurePlacementFactsMatchTopology(
            IReadOnlyCollection<AQGreenPlacementTopologyNode> structuralNodes,
            IReadOnlyCollection<StructuralPlacementFact> placementFacts,
            Guid placementTreeScopeId)
        {
            if (placementFacts.Count != structuralNodes.Count ||
                placementFacts.Any(fact =>
                    fact.PlacementTreeScopeId != placementTreeScopeId) ||
                placementFacts.GroupBy(fact => fact.ParticipantId)
                    .Any(group => group.Count() != 1))
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology changed or crossed a scope " +
                    "while structural completion was being evaluated.");
            }
        }

        private static void EnsureParticipantsQualify(
            int tenantId,
            DateTime cutoff,
            IReadOnlyCollection<Guid> effectiveParticipantIds,
            IReadOnlyCollection<StructuralParticipantState> participantStates)
        {
            if (participantStates.Count != effectiveParticipantIds.Count ||
                participantStates.GroupBy(state => state.ParticipantId)
                    .Any(group => group.Count() != 1) ||
                participantStates.Any(state =>
                    state.ParticipationTenantId != tenantId ||
                    state.CustomerTenantId != tenantId ||
                    state.UserTenantId != tenantId))
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology references missing or cross-Tenant " +
                    "participation identity evidence.");
            }

            var unresolvedLifecycleState = participantStates.FirstOrDefault(state =>
                state.ParticipationIsDeleted ||
                state.CustomerIsDeleted ||
                !state.CustomerIsActive ||
                state.UserIsDeleted ||
                !state.UserIsActive);
            if (unresolvedLifecycleState != null)
            {
                throw new AQGreenStructuralContributionPolicyRequiredException(
                    unresolvedLifecycleState.ParticipantId);
            }

            var invalidParticipation = participantStates.FirstOrDefault(state =>
                state.ParticipationStatus != EntryParticipationStatus.Active ||
                !state.ActivatedAt.HasValue ||
                state.ActivatedAt.Value > cutoff);
            if (invalidParticipation != null)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    $"AQGreen placement topology references participant " +
                    $"{invalidParticipation.ParticipantId} without cutoff-effective " +
                    "Active participation evidence.");
            }
        }

        private static void EnsureInput(
            int tenantId,
            Guid participantId,
            DateTime cutoff)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (cutoff == default || cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC structural evaluation cutoff is required.",
                    nameof(cutoff));
        }

        private sealed class StructuralPlacementFact
        {
            public Guid ParticipantId { get; set; }
            public Guid PlacementTreeScopeId { get; set; }
            public DateTime PlacedAt { get; set; }
        }

        private sealed class StructuralParticipantState
        {
            public Guid ParticipantId { get; set; }
            public int ParticipationTenantId { get; set; }
            public EntryParticipationStatus ParticipationStatus { get; set; }
            public DateTime? ActivatedAt { get; set; }
            public bool ParticipationIsDeleted { get; set; }
            public int? CustomerTenantId { get; set; }
            public bool CustomerIsActive { get; set; }
            public bool CustomerIsDeleted { get; set; }
            public int? UserTenantId { get; set; }
            public bool UserIsActive { get; set; }
            public bool UserIsDeleted { get; set; }
        }
    }
}
