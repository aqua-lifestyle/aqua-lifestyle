using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Pure implementation of the resolved D02 sponsor-local parent-major vacancy
    /// rule. Live placement and read-only legacy simulation both use this selector.
    /// The caller owns validation and must supply the sponsor subtree in canonical
    /// parent-major breadth-first order.
    /// </summary>
    public static class AQGreenSponsorLocalVacancySelector
    {
        public static AQGreenPlacementVacancy FindFirstVacancy(
            IReadOnlyList<AQGreenPlacementVacancyNode> sponsorSubtree)
        {
            if (sponsorSubtree == null || sponsorSubtree.Count == 0)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen placement topology contains no sponsor anchor.");
            }

            var occupiedSlots = sponsorSubtree
                .Where(node => node.PlacementParentParticipantId.HasValue)
                .ToLookup(
                    node => node.PlacementParentParticipantId.Value,
                    node => node.PlacementSlot.Value);
            foreach (var parent in sponsorSubtree)
            {
                var occupied = occupiedSlots[parent.ParticipantId].ToHashSet();
                for (var slot = 1; slot <= AQGreenPlacementRules.MaximumPlacementSlot; slot++)
                {
                    if (!occupied.Contains(slot))
                        return new AQGreenPlacementVacancy(parent.ParticipantId, slot);
                }
            }

            throw new AQGreenPlacementTopologyIntegrityException(
                "AQGreen placement topology contains no canonical vacancy.");
        }
    }

    public sealed class AQGreenPlacementVacancyNode
    {
        public AQGreenPlacementVacancyNode(
            Guid participantId,
            Guid? placementParentParticipantId,
            int? placementSlot)
        {
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (placementParentParticipantId.HasValue && !placementSlot.HasValue)
                throw new ArgumentException(
                    "A non-root placement requires a slot.",
                    nameof(placementSlot));
            if (placementSlot.HasValue &&
                (placementSlot.Value < 1 ||
                 placementSlot.Value > AQGreenPlacementRules.MaximumPlacementSlot))
                throw new ArgumentOutOfRangeException(nameof(placementSlot));

            ParticipantId = participantId;
            PlacementParentParticipantId = placementParentParticipantId;
            PlacementSlot = placementSlot;
        }

        public Guid ParticipantId { get; }
        public Guid? PlacementParentParticipantId { get; }
        public int? PlacementSlot { get; }
    }

    public readonly struct AQGreenPlacementVacancy
    {
        public AQGreenPlacementVacancy(Guid parentParticipantId, int placementSlot)
        {
            ParentParticipantId = parentParticipantId;
            PlacementSlot = placementSlot;
        }

        public Guid ParentParticipantId { get; }
        public int PlacementSlot { get; }
    }
}
