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
        : IAQGreenStructuralCompletionEvaluator,
          IAQGreenGraduationStructuralEvidenceEvaluator,
          IAQGreenCommissionStructuralEvidenceEvaluator,
          ITransientDependency
    {
        private const int GraduationMaximumRelativeDepth = 2;
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
            var evaluation = await EvaluateCoreAsync(
                tenantId,
                participantId,
                cutoff,
                AQGreenStructuralCompletionCalculator.MaximumLevel,
                cancellationToken);
            return new AQGreenStructuralCompletionResult(
                participantId,
                evaluation.PlacementTreeScopeId,
                evaluation.Outcome.StructuralCompletionLevel,
                evaluation.Outcome.QualifyingDepthCounts[1],
                evaluation.Outcome.QualifyingDepthCounts[2],
                evaluation.Outcome.QualifyingDepthCounts[3],
                cutoff,
                AQGreenStructuralQualificationRules.CurrentVersion);
        }

        async Task<AQGreenGraduationStructuralEvidenceResult>
            IAQGreenGraduationStructuralEvidenceEvaluator.EvaluateAsync(
                int tenantId,
                Guid participantId,
                DateTime cutoff,
                CancellationToken cancellationToken)
        {
            var evaluation = await EvaluateCoreAsync(
                tenantId,
                participantId,
                cutoff,
                GraduationMaximumRelativeDepth,
                cancellationToken);
            var observations = evaluation.Nodes
                .Select((node, ordinal) =>
                    new AQGreenGraduationStructuralEvidenceObservation
                    {
                        CanonicalOrdinal = ordinal,
                        SourcePlacementId = node.SourcePlacementId,
                        ParticipationStatusObserved = node.ParticipationStatus,
                        ParticipationActivatedAtObserved = node.ParticipationActivatedAt,
                        ParticipationIsDeletedObserved = node.ParticipationIsDeleted,
                        CustomerIdObserved = node.CustomerId,
                        CustomerTenantMatchedObserved = node.CustomerTenantId == tenantId,
                        CustomerIsActiveObserved = node.CustomerIsActive,
                        CustomerIsDeletedObserved = node.CustomerIsDeleted,
                        UserIdObserved = node.UserId,
                        UserTenantMatchedObserved = node.UserTenantId == tenantId,
                        UserIsActiveObserved = node.UserIsActive,
                        UserIsDeletedObserved = node.UserIsDeleted
                    })
                .ToList();
            return new AQGreenGraduationStructuralEvidenceResult(
                participantId,
                evaluation.PlacementTreeScopeId,
                cutoff,
                evaluation.Outcome.StructuralCompletionLevel,
                evaluation.Outcome.QualifyingDepthCounts[1],
                evaluation.Outcome.QualifyingDepthCounts[2],
                AQGreenStructuralQualificationRules.CurrentVersion,
                observations);
        }

        async Task<AQGreenCommissionStructuralEvidenceResult>
            IAQGreenCommissionStructuralEvidenceEvaluator.EvaluateAsync(
                int tenantId,
                Guid participantId,
                DateTime cutoff,
                CancellationToken cancellationToken)
        {
            var evaluation = await EvaluateCoreAsync(
                tenantId,
                participantId,
                cutoff,
                AQGreenStructuralCompletionCalculator.MaximumLevel,
                cancellationToken);
            var observations = evaluation.Nodes
                .Select((node, ordinal) =>
                    new AQGreenCommissionStructuralEvidenceObservation
                    {
                        CanonicalOrdinal = ordinal,
                        SourcePlacementId = node.SourcePlacementId,
                        ParticipationStatusObserved = node.ParticipationStatus,
                        ParticipationActivatedAtObserved = node.ParticipationActivatedAt,
                        ParticipationIsDeletedObserved = node.ParticipationIsDeleted,
                        CustomerIdObserved = node.CustomerId,
                        CustomerTenantMatchedObserved = node.CustomerTenantId == tenantId,
                        CustomerIsActiveObserved = node.CustomerIsActive,
                        CustomerIsDeletedObserved = node.CustomerIsDeleted,
                        UserIdObserved = node.UserId,
                        UserTenantMatchedObserved = node.UserTenantId == tenantId,
                        UserIsActiveObserved = node.UserIsActive,
                        UserIsDeletedObserved = node.UserIsDeleted
                    })
                .ToList();
            return new AQGreenCommissionStructuralEvidenceResult(
                participantId,
                evaluation.PlacementTreeScopeId,
                cutoff,
                evaluation.Outcome.StructuralCompletionLevel,
                evaluation.Outcome.QualifyingDepthCounts[1],
                evaluation.Outcome.QualifyingDepthCounts[2],
                evaluation.Outcome.QualifyingDepthCounts[3],
                AQGreenPlacementRules.CurrentVersion,
                AQGreenStructuralQualificationRules.CurrentVersion,
                observations);
        }

        private async Task<StructuralEvaluation> EvaluateCoreAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            int maximumRelativeDepth,
            CancellationToken cancellationToken)
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
                throw new AQGreenStructuralEvaluationNotPlacedException(
                    participantId,
                    cutoff);

            var structuralNodes = await _topologyReader
                .GetSubtreeInCanonicalOrderAsync(
                    tenantId,
                    anchorPlacement.PlacementTreeScopeId,
                    participantId,
                    maximumRelativeDepth,
                    cancellationToken);
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
                    SourcePlacementId = placement.Id,
                    TenantId = placement.TenantId,
                    ParticipantId = placement.ParticipantId,
                    PlacementTreeScopeId = placement.PlacementTreeScopeId,
                    PlacementParentParticipantId =
                        placement.PlacementParentParticipantId,
                    PlacementSlot = placement.PlacementSlot,
                    CanonicalPath = placement.CanonicalPath,
                    PlacedAt = placement.PlacedAt,
                    RulesVersion = placement.RulesVersion
                })
                .ToListAsync(cancellationToken);
            EnsurePlacementFactsMatchTopology(
                structuralNodes,
                placementFacts,
                anchorPlacement.PlacementTreeScopeId);

            var effectivePlacementFacts = placementFacts
                .Where(fact => fact.PlacedAt <= cutoff)
                .ToList();
            var validatedTopology = AQGreenBoundedPlacementTopologyValidator.Validate(
                tenantId,
                anchorPlacement.PlacementTreeScopeId,
                participantId,
                cutoff,
                maximumRelativeDepth,
                effectivePlacementFacts.Select(MapPlacementFact).ToList());
            var topologyReaderOrder = structuralNodes
                .Where(node => effectivePlacementFacts.Any(fact =>
                    fact.ParticipantId == node.ParticipantId))
                .Select(node => node.ParticipantId);
            if (!topologyReaderOrder.SequenceEqual(
                    validatedTopology.Select(node => node.Placement.ParticipantId)))
                throw new AQGreenPlacementTopologyIntegrityException(
                    "The live topology traversal and immutable bounded manifest disagree.");

            var effectiveParticipantIds = validatedTopology
                .Select(node => node.Placement.ParticipantId)
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
                        CustomerId = customer.Id,
                        CustomerTenantId = customer.TenantId,
                        CustomerIsActive = customer.IsActive,
                        CustomerIsDeleted = customer.IsDeleted,
                        UserId = user.Id,
                        UserTenantId = user.TenantId,
                        UserIsActive = user.IsActive,
                        UserIsDeleted = user.IsDeleted
                    })
                .ToListAsync(cancellationToken);
            EnsureParticipantStateCoverage(
                effectiveParticipantIds,
                participantStates);

            var stateByParticipant = participantStates.ToDictionary(
                state => state.ParticipantId);
            var qualificationNodes = validatedTopology.Select(node =>
            {
                var placement = node.Placement;
                var state = stateByParticipant[placement.ParticipantId];
                return new AQGreenStructuralQualificationNode
                {
                    SourcePlacementId = placement.Id,
                    ParticipantId = placement.ParticipantId,
                    RelativeDepth = node.RelativeDepth,
                    ParticipationTenantId = state.ParticipationTenantId,
                    ParticipationStatus = state.ParticipationStatus,
                    ParticipationActivatedAt = state.ActivatedAt,
                    ParticipationIsDeleted = state.ParticipationIsDeleted,
                    CustomerId = state.CustomerId,
                    CustomerTenantId = state.CustomerTenantId,
                    CustomerIsActive = state.CustomerIsActive,
                    CustomerIsDeleted = state.CustomerIsDeleted,
                    UserId = state.UserId,
                    UserTenantId = state.UserTenantId,
                    UserIsActive = state.UserIsActive,
                    UserIsDeleted = state.UserIsDeleted
                };
            }).ToList();
            var outcome = AQGreenStructuralQualificationRules.Evaluate(
                tenantId,
                cutoff,
                maximumRelativeDepth,
                qualificationNodes);
            return new StructuralEvaluation(
                anchorPlacement.PlacementTreeScopeId,
                qualificationNodes,
                outcome);
        }

        private AqualLifeStyleDbContext GetPostgreSqlContext()
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
                throw new NotSupportedException(
                    "AQGreen structural completion evaluation requires PostgreSQL.");
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
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology changed or crossed a scope " +
                    "while structural completion was being evaluated.");
        }

        private static void EnsureParticipantStateCoverage(
            IReadOnlyCollection<Guid> effectiveParticipantIds,
            IReadOnlyCollection<StructuralParticipantState> participantStates)
        {
            if (participantStates.Count != effectiveParticipantIds.Count ||
                participantStates.GroupBy(state => state.ParticipantId)
                    .Any(group => group.Count() != 1))
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology references missing participation " +
                    "identity evidence.");
        }

        private static AQGreenImmutablePlacementFact MapPlacementFact(
            StructuralPlacementFact fact) =>
            new()
            {
                Id = fact.SourcePlacementId,
                TenantId = fact.TenantId,
                PlacementTreeScopeId = fact.PlacementTreeScopeId,
                ParticipantId = fact.ParticipantId,
                PlacementParentParticipantId = fact.PlacementParentParticipantId,
                PlacementSlot = fact.PlacementSlot,
                CanonicalPath = fact.CanonicalPath,
                PlacedAt = fact.PlacedAt,
                RulesVersion = fact.RulesVersion
            };

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

        private sealed class StructuralEvaluation
        {
            public StructuralEvaluation(
                Guid placementTreeScopeId,
                IReadOnlyList<AQGreenStructuralQualificationNode> nodes,
                AQGreenStructuralQualificationOutcome outcome)
            {
                PlacementTreeScopeId = placementTreeScopeId;
                Nodes = nodes;
                Outcome = outcome;
            }

            public Guid PlacementTreeScopeId { get; }
            public IReadOnlyList<AQGreenStructuralQualificationNode> Nodes { get; }
            public AQGreenStructuralQualificationOutcome Outcome { get; }
        }

        private sealed class StructuralPlacementFact
        {
            public Guid SourcePlacementId { get; init; }
            public int TenantId { get; init; }
            public Guid ParticipantId { get; init; }
            public Guid PlacementTreeScopeId { get; init; }
            public Guid? PlacementParentParticipantId { get; init; }
            public int? PlacementSlot { get; init; }
            public string CanonicalPath { get; init; }
            public DateTime PlacedAt { get; init; }
            public string RulesVersion { get; init; }
        }

        private sealed class StructuralParticipantState
        {
            public Guid ParticipantId { get; init; }
            public int ParticipationTenantId { get; init; }
            public EntryParticipationStatus ParticipationStatus { get; init; }
            public DateTime? ActivatedAt { get; init; }
            public bool ParticipationIsDeleted { get; init; }
            public int CustomerId { get; init; }
            public int? CustomerTenantId { get; init; }
            public bool CustomerIsActive { get; init; }
            public bool CustomerIsDeleted { get; init; }
            public long UserId { get; init; }
            public int? UserTenantId { get; init; }
            public bool UserIsActive { get; init; }
            public bool UserIsDeleted { get; init; }
        }
    }
}
