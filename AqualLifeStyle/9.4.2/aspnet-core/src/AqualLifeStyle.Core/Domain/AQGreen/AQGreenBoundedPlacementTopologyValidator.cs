using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Pure bounded-topology validator shared by live decision capture and
    /// historical evidence replay. It derives relative depth from immutable
    /// canonical paths; relative depth is never persisted as evidence.
    /// </summary>
    public static class AQGreenBoundedPlacementTopologyValidator
    {
        public static IReadOnlyList<AQGreenBoundedPlacementTopologyNode> Validate(
            int tenantId,
            Guid placementTreeScopeId,
            Guid anchorParticipantId,
            DateTime cutoff,
            int maximumRelativeDepth,
            IReadOnlyCollection<AQGreenImmutablePlacementFact> placements)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));
            if (anchorParticipantId == Guid.Empty)
                throw new ArgumentException(
                    "A placement anchor is required.",
                    nameof(anchorParticipantId));
            if (cutoff == default || cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC cutoff is required.",
                    nameof(cutoff));
            if (maximumRelativeDepth < 1 ||
                maximumRelativeDepth > AQGreenStructuralCompletionCalculator.MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(maximumRelativeDepth));
            if (placements == null) throw new ArgumentNullException(nameof(placements));

            var facts = placements.ToList();
            var anchors = facts
                .Where(fact => fact.ParticipantId == anchorParticipantId)
                .ToList();
            if (anchors.Count != 1 ||
                facts.Count == 0 ||
                facts.GroupBy(fact => fact.Id).Any(group => group.Count() != 1) ||
                facts.GroupBy(fact => fact.ParticipantId).Any(group => group.Count() != 1))
                throw Corrupt("The bounded placement manifest has a missing or ambiguous anchor.");

            var anchor = anchors[0];
            if (anchor.CanonicalPath == null ||
                anchor.TenantId != tenantId ||
                anchor.PlacementTreeScopeId != placementTreeScopeId ||
                anchor.PlacedAt > cutoff)
                throw Corrupt("The bounded placement anchor conflicts with its decision boundary.");

            var byParticipant = facts.ToDictionary(fact => fact.ParticipantId);
            var nodes = new List<AQGreenBoundedPlacementTopologyNode>(facts.Count);
            foreach (var fact in facts)
            {
                if (fact.Id == Guid.Empty ||
                    fact.ParticipantId == Guid.Empty ||
                    fact.TenantId != tenantId ||
                    fact.PlacementTreeScopeId != placementTreeScopeId ||
                    fact.CanonicalPath == null ||
                    fact.CanonicalPath.Any(character => character < '1' || character > '5') ||
                    !string.Equals(
                        fact.RulesVersion,
                        AQGreenPlacementRules.CurrentVersion,
                        StringComparison.Ordinal) ||
                    fact.PlacedAt == default ||
                    fact.PlacedAt.Kind != DateTimeKind.Utc ||
                    fact.PlacedAt > cutoff ||
                    !fact.CanonicalPath.StartsWith(
                        anchor.CanonicalPath,
                        StringComparison.Ordinal))
                    throw Corrupt("A placement fact is invalid or outside the decision boundary.");

                var relativeDepth =
                    fact.CanonicalPath.Length - anchor.CanonicalPath.Length;
                if (relativeDepth < 0 || relativeDepth > maximumRelativeDepth ||
                    (relativeDepth == 0 && fact.Id != anchor.Id))
                    throw Corrupt("A placement fact is outside the bounded topology.");

                if (relativeDepth > 0)
                {
                    if (!fact.PlacementParentParticipantId.HasValue ||
                        !fact.PlacementSlot.HasValue ||
                        fact.PlacementSlot.Value < 1 ||
                        fact.PlacementSlot.Value > AQGreenPlacementRules.MaximumPlacementSlot ||
                        !byParticipant.TryGetValue(
                            fact.PlacementParentParticipantId.Value,
                            out var parent) ||
                        parent.CanonicalPath == null ||
                        parent.CanonicalPath.Length != fact.CanonicalPath.Length - 1 ||
                        !string.Equals(
                            fact.CanonicalPath,
                            parent.CanonicalPath + fact.PlacementSlot.Value,
                            StringComparison.Ordinal) ||
                        fact.PlacedAt < parent.PlacedAt)
                        throw Corrupt("The bounded placement manifest contains a broken edge.");
                }

                nodes.Add(new AQGreenBoundedPlacementTopologyNode(fact, relativeDepth));
            }

            return nodes
                .OrderBy(node => node.RelativeDepth)
                .ThenBy(node => node.Placement.CanonicalPath, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }

        private static AQGreenPlacementTopologyIntegrityException Corrupt(string message) =>
            new(message);
    }

    public sealed class AQGreenImmutablePlacementFact
    {
        public Guid Id { get; init; }
        public int TenantId { get; init; }
        public Guid PlacementTreeScopeId { get; init; }
        public Guid ParticipantId { get; init; }
        public Guid? PlacementParentParticipantId { get; init; }
        public int? PlacementSlot { get; init; }
        public string CanonicalPath { get; init; }
        public DateTime PlacedAt { get; init; }
        public string RulesVersion { get; init; }
    }

    public sealed class AQGreenBoundedPlacementTopologyNode
    {
        public AQGreenBoundedPlacementTopologyNode(
            AQGreenImmutablePlacementFact placement,
            int relativeDepth)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            RelativeDepth = relativeDepth;
        }

        public AQGreenImmutablePlacementFact Placement { get; }
        public int RelativeDepth { get; }
    }
}
