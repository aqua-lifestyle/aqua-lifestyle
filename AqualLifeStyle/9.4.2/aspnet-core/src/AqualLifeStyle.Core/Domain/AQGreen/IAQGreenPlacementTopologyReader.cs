using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenPlacementTopologyReader
    {
        Task<AQGreenPlacementTopologyNode> GetPlacementAsync(
            int tenantId,
            Guid placementTreeScopeId,
            Guid participantId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AQGreenPlacementTopologyNode>> GetChildrenAsync(
            int tenantId,
            Guid placementTreeScopeId,
            Guid parentParticipantId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AQGreenPlacementTopologyNode>> GetSubtreeInCanonicalOrderAsync(
            int tenantId,
            Guid placementTreeScopeId,
            Guid sponsorParticipantId,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenPlacementTopologyIntegrityException
        : InvalidOperationException
    {
        public AQGreenPlacementTopologyIntegrityException(string message)
            : base(message)
        {
        }
    }

    public sealed class AQGreenPlacementTopologyNode
    {
        public AQGreenPlacementTopologyNode(
            Guid participantId,
            Guid placementTreeScopeId,
            Guid? placementParentParticipantId,
            int? placementSlot,
            int relativeDepth)
        {
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));
            if (placementSlot.HasValue &&
                (placementSlot < 1 ||
                 placementSlot > AQGreenPlacementRules.MaximumPlacementSlot))
            {
                throw new ArgumentOutOfRangeException(nameof(placementSlot));
            }
            if (relativeDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(relativeDepth));

            ParticipantId = participantId;
            PlacementTreeScopeId = placementTreeScopeId;
            PlacementParentParticipantId = placementParentParticipantId;
            PlacementSlot = placementSlot;
            RelativeDepth = relativeDepth;
        }

        public Guid ParticipantId { get; }
        public Guid PlacementTreeScopeId { get; }
        public Guid? PlacementParentParticipantId { get; }
        public int? PlacementSlot { get; }
        public int RelativeDepth { get; }
    }
}
