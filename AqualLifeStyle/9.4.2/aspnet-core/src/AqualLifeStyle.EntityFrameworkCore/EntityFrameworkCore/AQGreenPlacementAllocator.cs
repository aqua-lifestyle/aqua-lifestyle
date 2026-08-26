using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class AQGreenPlacementAllocator
        : IAQGreenPlacementAllocator, ITransientDependency
    {
        private const string ParticipantUniqueConstraint =
            "IX_AQGreenNetworkPlacements_TenantId_ParticipantId";
        private const string ScopeParticipantUniqueConstraint =
            "AK_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId_Part~";
        private const string ParentSlotUniqueConstraint =
            "IX_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId_Plac~";

        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;
        private readonly IAQGreenPlacementTreeLock _placementTreeLock;
        private readonly IAQGreenPlacementTopologyReader _topologyReader;
        private readonly IAQGreenPlacementClock _clock;

        public AQGreenPlacementAllocator(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IAQGreenPlacementTreeLock placementTreeLock,
            IAQGreenPlacementTopologyReader topologyReader,
            IAQGreenPlacementClock clock)
        {
            _dbContextProvider = dbContextProvider;
            _placementTreeLock = placementTreeLock;
            _topologyReader = topologyReader;
            _clock = clock;
        }

        public async Task<AQGreenPlacementAllocationResult> AllocateAsync(
            int tenantId,
            Guid participantId,
            CancellationToken cancellationToken = default)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));

            var context = GetPostgreSqlContext();
            AQGreenPlacementTreeLock.EnsureActiveTransaction(context);
            EnsureReadCommittedTransaction(context);

            var scopeHint = await ResolveScopeHintAsync(
                context,
                tenantId,
                participantId,
                cancellationToken);
            await _placementTreeLock.AcquireAsync(scopeHint, cancellationToken);

            var facts = await ReadAuthoritativeFactsAsync(
                context,
                tenantId,
                participantId,
                cancellationToken);
            if (facts.SponsorPlacement.PlacementTreeScopeId != scopeHint)
            {
                throw new AQGreenPlacementConflictException(
                    "The credited sponsor placement-tree scope changed before allocation could be locked.");
            }

            var subtree = await _topologyReader.GetSubtreeInCanonicalOrderAsync(
                tenantId,
                scopeHint,
                facts.Attribution.CreditedSponsorParticipantId.Value,
                cancellationToken);

            if (facts.ExistingPlacement != null)
            {
                EnsureExistingPlacementMatches(
                    facts.ExistingPlacement,
                    scopeHint,
                    participantId,
                    subtree);
                return new AQGreenPlacementAllocationResult(
                    facts.ExistingPlacement,
                    wasAlreadyPlaced: true);
            }

            var vacancy = FindFirstVacancy(subtree);
            var parent = await context.AQGreenNetworkPlacements
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    placement => placement.TenantId == tenantId &&
                                 placement.PlacementTreeScopeId == scopeHint &&
                                 placement.ParticipantId == vacancy.ParentParticipantId,
                    cancellationToken);
            if (parent == null)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology changed while selecting the canonical vacancy.");
            }

            var placedAt = await _clock.GetUtcNowAsync(cancellationToken);
            var placement = AQGreenNetworkPlacement.CreateChild(
                parent,
                participantId,
                vacancy.PlacementSlot,
                placedAt,
                AQGreenPlacementRules.CurrentVersion);
            context.AQGreenNetworkPlacements.Add(placement);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return new AQGreenPlacementAllocationResult(
                    placement,
                    wasAlreadyPlaced: false);
            }
            catch (DbUpdateException exception)
                when (TryGetUniqueViolation(exception, out var constraintName))
            {
                context.Entry(placement).State = EntityState.Detached;
                return await ClassifyUniqueViolationAsync(
                    context,
                    tenantId,
                    participantId,
                    scopeHint,
                    facts.Attribution.CreditedSponsorParticipantId.Value,
                    vacancy,
                    constraintName,
                    cancellationToken);
            }
        }

        private static async Task<Guid> ResolveScopeHintAsync(
            AqualLifeStyleDbContext context,
            int tenantId,
            Guid participantId,
            CancellationToken cancellationToken)
        {
            await RequireParticipantAsync(
                context,
                tenantId,
                participantId,
                AQGreenPlacementMissingFact.Participant,
                cancellationToken);
            var attribution = await RequireSponsoredAttributionAsync(
                context,
                tenantId,
                participantId,
                cancellationToken);
            await RequireParticipantAsync(
                context,
                tenantId,
                attribution.CreditedSponsorParticipantId.Value,
                AQGreenPlacementMissingFact.SponsorParticipation,
                cancellationToken,
                rejectDeletedSponsor: true);
            var sponsorPlacement = await RequireSponsorPlacementAsync(
                context,
                tenantId,
                attribution.CreditedSponsorParticipantId.Value,
                cancellationToken);
            return sponsorPlacement.PlacementTreeScopeId;
        }

        private static async Task<AuthoritativeFacts> ReadAuthoritativeFactsAsync(
            AqualLifeStyleDbContext context,
            int tenantId,
            Guid participantId,
            CancellationToken cancellationToken)
        {
            await RequireParticipantAsync(
                context,
                tenantId,
                participantId,
                AQGreenPlacementMissingFact.Participant,
                cancellationToken);
            var attribution = await RequireSponsoredAttributionAsync(
                context,
                tenantId,
                participantId,
                cancellationToken);
            var confirmation = await context.AQGreenRecruitmentAttributionConfirmations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.TenantId == tenantId &&
                           row.AttributionId == attribution.Id,
                    cancellationToken);
            if (confirmation == null)
                throw new AQGreenPlacementAttributionNotConfirmedException();
            if (confirmation.ConfirmationMethod !=
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance)
            {
                throw new AQGreenPlacementConflictException(
                    "The attribution confirmation method conflicts with sponsored placement.");
            }

            await RequireParticipantAsync(
                context,
                tenantId,
                attribution.CreditedSponsorParticipantId.Value,
                AQGreenPlacementMissingFact.SponsorParticipation,
                cancellationToken,
                rejectDeletedSponsor: true);
            var sponsorPlacement = await RequireSponsorPlacementAsync(
                context,
                tenantId,
                attribution.CreditedSponsorParticipantId.Value,
                cancellationToken);
            var scopeExists = await context.AQGreenPlacementTreeScopes
                .AsNoTracking()
                .AnyAsync(
                    scope => scope.TenantId == tenantId &&
                             scope.Id == sponsorPlacement.PlacementTreeScopeId,
                    cancellationToken);
            if (!scopeExists)
            {
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.PlacementTreeScope);
            }

            var existingPlacement = await context.AQGreenNetworkPlacements
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    placement => placement.TenantId == tenantId &&
                                 placement.ParticipantId == participantId,
                    cancellationToken);
            return new AuthoritativeFacts(
                attribution,
                sponsorPlacement,
                existingPlacement);
        }

        private static async Task<EntryParticipation> RequireParticipantAsync(
            AqualLifeStyleDbContext context,
            int tenantId,
            Guid participantId,
            AQGreenPlacementMissingFact missingFact,
            CancellationToken cancellationToken,
            bool rejectDeletedSponsor = false)
        {
            var participant = await context.EntryParticipations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == participantId,
                    cancellationToken);
            if (participant == null || (!rejectDeletedSponsor && participant.IsDeleted))
                throw new AQGreenPlacementAllocationNotFoundException(missingFact);
            if (participant.TenantId != tenantId)
            {
                throw new AQGreenPlacementConflictException(
                    "AQGreen placement cannot cross the Tenant boundary.");
            }
            if (rejectDeletedSponsor && participant.IsDeleted)
            {
                throw new AQGreenPlacementConflictException(
                    "The credited sponsor is deleted and terminal placement handling is unresolved.");
            }

            return participant;
        }

        private static async Task<AQGreenRecruitmentAttribution>
            RequireSponsoredAttributionAsync(
                AqualLifeStyleDbContext context,
                int tenantId,
                Guid participantId,
                CancellationToken cancellationToken)
        {
            var attribution = await context.AQGreenRecruitmentAttributions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.TenantId == tenantId &&
                           row.ParticipantId == participantId,
                    cancellationToken);
            if (attribution == null)
            {
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.Attribution);
            }
            if (attribution.AttributionKind !=
                AQGreenRecruitmentAttributionKind.SponsoredParticipant)
            {
                throw new AQGreenPlacementUnsupportedAttributionException(
                    attribution.AttributionKind);
            }
            if (attribution.AcquisitionSource != AQGreenAcquisitionSource.MemberInvitation ||
                !attribution.CreditedSponsorParticipantId.HasValue)
            {
                throw new AQGreenPlacementConflictException(
                    "Sponsored AQGreen attribution has conflicting acquisition evidence.");
            }

            return attribution;
        }

        private static async Task<AQGreenNetworkPlacement> RequireSponsorPlacementAsync(
            AqualLifeStyleDbContext context,
            int tenantId,
            Guid sponsorParticipantId,
            CancellationToken cancellationToken)
        {
            var sponsorPlacement = await context.AQGreenNetworkPlacements
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    placement => placement.ParticipantId == sponsorParticipantId,
                    cancellationToken);
            if (sponsorPlacement == null)
            {
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.SponsorPlacement);
            }
            if (sponsorPlacement.TenantId != tenantId)
            {
                throw new AQGreenPlacementConflictException(
                    "The credited sponsor placement crosses the Tenant boundary.");
            }

            return sponsorPlacement;
        }

        private static Vacancy FindFirstVacancy(
            IReadOnlyList<AQGreenPlacementTopologyNode> subtree)
        {
            if (subtree == null || subtree.Count == 0)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology contains no sponsor anchor.");
            }

            var occupiedSlots = subtree
                .Where(node => node.PlacementParentParticipantId.HasValue)
                .ToLookup(
                    node => node.PlacementParentParticipantId.Value,
                    node => node.PlacementSlot.Value);
            foreach (var parent in subtree)
            {
                var occupied = occupiedSlots[parent.ParticipantId].ToHashSet();
                for (var slot = 1; slot <= AQGreenPlacementRules.MaximumPlacementSlot; slot++)
                {
                    if (!occupied.Contains(slot))
                        return new Vacancy(parent.ParticipantId, slot);
                }
            }

            throw new AQGreenPlacementTopologyIntegrityException(
                "AQGreen placement topology contains no canonical vacancy.");
        }

        private static void EnsureExistingPlacementMatches(
            AQGreenNetworkPlacement existingPlacement,
            Guid placementTreeScopeId,
            Guid participantId,
            IReadOnlyCollection<AQGreenPlacementTopologyNode> sponsorSubtree)
        {
            if (existingPlacement.ParticipantId != participantId ||
                existingPlacement.PlacementTreeScopeId != placementTreeScopeId ||
                !sponsorSubtree.Any(node =>
                    node.ParticipantId == participantId && node.RelativeDepth > 0))
            {
                throw new AQGreenPlacementConflictException(
                    "The participant already has a placement that conflicts with authoritative attribution.");
            }
        }

        private async Task<AQGreenPlacementAllocationResult> ClassifyUniqueViolationAsync(
            AqualLifeStyleDbContext context,
            int tenantId,
            Guid participantId,
            Guid placementTreeScopeId,
            Guid sponsorParticipantId,
            Vacancy attemptedVacancy,
            string constraintName,
            CancellationToken cancellationToken)
        {
            if (constraintName == ParticipantUniqueConstraint ||
                constraintName == ScopeParticipantUniqueConstraint)
            {
                var existingPlacement = await context.AQGreenNetworkPlacements
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        placement => placement.TenantId == tenantId &&
                                     placement.ParticipantId == participantId,
                        cancellationToken);
                if (existingPlacement != null)
                {
                    var subtree = await _topologyReader.GetSubtreeInCanonicalOrderAsync(
                        tenantId,
                        placementTreeScopeId,
                        sponsorParticipantId,
                        cancellationToken);
                    EnsureExistingPlacementMatches(
                        existingPlacement,
                        placementTreeScopeId,
                        participantId,
                        subtree);
                    return new AQGreenPlacementAllocationResult(
                        existingPlacement,
                        wasAlreadyPlaced: true);
                }

                throw new AQGreenPlacementConflictException(
                    "The participant placement uniqueness conflict could not be reconciled.");
            }

            if (constraintName == ParentSlotUniqueConstraint)
            {
                var occupantExists = await context.AQGreenNetworkPlacements
                    .AsNoTracking()
                    .AnyAsync(
                        placement => placement.TenantId == tenantId &&
                                     placement.PlacementTreeScopeId == placementTreeScopeId &&
                                     placement.PlacementParentParticipantId ==
                                         attemptedVacancy.ParentParticipantId &&
                                     placement.PlacementSlot == attemptedVacancy.PlacementSlot,
                        cancellationToken);
                if (occupantExists)
                {
                    await _topologyReader.GetSubtreeInCanonicalOrderAsync(
                        tenantId,
                        placementTreeScopeId,
                        sponsorParticipantId,
                        cancellationToken);
                    throw new AQGreenPlacementConflictException(
                        "The selected placement slot was occupied without using the required scope lock.");
                }

                throw new AQGreenPlacementConflictException(
                    "The placement-slot uniqueness conflict could not be reconciled.");
            }

            throw new AQGreenPlacementConflictException(
                $"Unexpected AQGreen placement uniqueness conflict '{constraintName}'.");
        }

        private AqualLifeStyleDbContext GetPostgreSqlContext()
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
            {
                throw new NotSupportedException(
                    "AQGreen placement allocation requires PostgreSQL.");
            }

            return context;
        }

        private static void EnsureReadCommittedTransaction(
            AqualLifeStyleDbContext context)
        {
            var isolationLevel = context.Database.CurrentTransaction
                .GetDbTransaction()
                .IsolationLevel;
            if (isolationLevel != IsolationLevel.ReadCommitted)
            {
                throw new InvalidOperationException(
                    "AQGreen placement allocation requires a caller-owned READ COMMITTED " +
                    "transaction so authoritative facts can be re-read after lock acquisition.");
            }
        }

        private static bool TryGetUniqueViolation(
            DbUpdateException exception,
            out string constraintName)
        {
            if (exception.InnerException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                constraintName = postgresException.ConstraintName;
                return true;
            }

            constraintName = null;
            return false;
        }

        private sealed class AuthoritativeFacts
        {
            public AuthoritativeFacts(
                AQGreenRecruitmentAttribution attribution,
                AQGreenNetworkPlacement sponsorPlacement,
                AQGreenNetworkPlacement existingPlacement)
            {
                Attribution = attribution;
                SponsorPlacement = sponsorPlacement;
                ExistingPlacement = existingPlacement;
            }

            public AQGreenRecruitmentAttribution Attribution { get; }
            public AQGreenNetworkPlacement SponsorPlacement { get; }
            public AQGreenNetworkPlacement ExistingPlacement { get; }
        }

        private readonly struct Vacancy
        {
            public Vacancy(Guid parentParticipantId, int placementSlot)
            {
                ParentParticipantId = parentParticipantId;
                PlacementSlot = placementSlot;
            }

            public Guid ParentParticipantId { get; }
            public int PlacementSlot { get; }
        }
    }
}
