using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Domain.AQGreen
{
    public static class AQGreenStructuralQualificationRules
    {
        public const int MaximumRulesVersionLength = 64;
        public const string CurrentVersion = "AQGreenStructuralQualificationV1";

        public static AQGreenStructuralQualificationOutcome Evaluate(
            int tenantId,
            DateTime cutoff,
            int maximumRelativeDepth,
            IReadOnlyCollection<AQGreenStructuralQualificationNode> nodes)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (cutoff == default || cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC structural evaluation cutoff is required.",
                    nameof(cutoff));
            if (maximumRelativeDepth < 1 ||
                maximumRelativeDepth > AQGreenStructuralCompletionCalculator.MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(maximumRelativeDepth));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (nodes.Count == 0 || nodes.Count(node => node.RelativeDepth == 0) != 1)
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen structural qualification requires exactly one placement anchor.");
            if (nodes.Any(node =>
                    node.RelativeDepth < 0 ||
                    node.RelativeDepth > maximumRelativeDepth) ||
                nodes.GroupBy(node => node.ParticipantId)
                    .Any(group => group.Count() != 1) ||
                nodes.GroupBy(node => node.SourcePlacementId)
                    .Any(group => group.Count() != 1))
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen structural qualification evidence is duplicated or outside its bounded topology.");

            var unresolvedLifecycleState = nodes.FirstOrDefault(node =>
                node.ParticipationIsDeleted ||
                node.CustomerIsDeleted ||
                !node.CustomerIsActive ||
                node.UserIsDeleted ||
                !node.UserIsActive);
            if (unresolvedLifecycleState != null)
                throw new AQGreenStructuralContributionPolicyRequiredException(
                    unresolvedLifecycleState.ParticipantId);

            var invalidParticipation = nodes.FirstOrDefault(node =>
                node.ParticipationTenantId != tenantId ||
                node.CustomerTenantId != tenantId ||
                node.UserTenantId != tenantId ||
                node.ParticipationStatus != EntryParticipationStatus.Active ||
                !node.ParticipationActivatedAt.HasValue ||
                node.ParticipationActivatedAt.Value > cutoff);
            if (invalidParticipation != null)
                throw new AQGreenPlacementTopologyIntegrityException(
                    $"AQGreen placement topology references participant " +
                    $"{invalidParticipation.ParticipantId} without cutoff-effective " +
                    "same-Tenant Active participation identity evidence.");

            var counts = Enumerable.Range(1, maximumRelativeDepth)
                .ToDictionary(
                    relativeDepth => relativeDepth,
                    relativeDepth => nodes.Count(node =>
                        node.RelativeDepth == relativeDepth));
            var level = AQGreenStructuralCompletionCalculator.EvaluateThrough(
                maximumRelativeDepth,
                relativeDepth => counts[relativeDepth]);
            return new AQGreenStructuralQualificationOutcome(level, counts);
        }
    }

    public sealed class AQGreenStructuralQualificationNode
    {
        public Guid SourcePlacementId { get; init; }
        public Guid ParticipantId { get; init; }
        public int RelativeDepth { get; init; }
        public int ParticipationTenantId { get; init; }
        public EntryParticipationStatus ParticipationStatus { get; init; }
        public DateTime? ParticipationActivatedAt { get; init; }
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

    public sealed class AQGreenStructuralQualificationOutcome
    {
        public AQGreenStructuralQualificationOutcome(
            AQGreenStructuralCompletionLevel structuralCompletionLevel,
            IReadOnlyDictionary<int, int> qualifyingDepthCounts)
        {
            StructuralCompletionLevel = structuralCompletionLevel;
            QualifyingDepthCounts = qualifyingDepthCounts ??
                throw new ArgumentNullException(nameof(qualifyingDepthCounts));
        }

        public AQGreenStructuralCompletionLevel StructuralCompletionLevel { get; }
        public IReadOnlyDictionary<int, int> QualifyingDepthCounts { get; }
    }
}
