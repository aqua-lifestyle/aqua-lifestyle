using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenPlacementAllocator
    {
        Task<AQGreenPlacementAllocationResult> AllocateAsync(
            int tenantId,
            Guid participantId,
            CancellationToken cancellationToken = default);
    }

    public sealed class AQGreenPlacementAllocationResult
    {
        public AQGreenPlacementAllocationResult(
            AQGreenNetworkPlacement placement,
            bool wasAlreadyPlaced)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            WasAlreadyPlaced = wasAlreadyPlaced;
        }

        public AQGreenNetworkPlacement Placement { get; }
        public bool WasAlreadyPlaced { get; }
    }

    public enum AQGreenPlacementMissingFact
    {
        Participant = 1,
        Attribution = 2,
        SponsorParticipation = 3,
        SponsorPlacement = 4,
        PlacementTreeScope = 5
    }

    public sealed class AQGreenPlacementAllocationNotFoundException
        : InvalidOperationException
    {
        public AQGreenPlacementAllocationNotFoundException(
            AQGreenPlacementMissingFact missingFact)
            : base($"AQGreen placement allocation requires {Describe(missingFact)}.")
        {
            MissingFact = missingFact;
        }

        public AQGreenPlacementMissingFact MissingFact { get; }

        private static string Describe(AQGreenPlacementMissingFact missingFact) =>
            missingFact switch
            {
                AQGreenPlacementMissingFact.Participant => "an existing participant",
                AQGreenPlacementMissingFact.Attribution => "recruitment attribution",
                AQGreenPlacementMissingFact.SponsorParticipation => "the credited sponsor participation",
                AQGreenPlacementMissingFact.SponsorPlacement => "the credited sponsor placement",
                AQGreenPlacementMissingFact.PlacementTreeScope => "the sponsor placement-tree scope",
                _ => throw new ArgumentOutOfRangeException(nameof(missingFact))
            };
    }

    public sealed class AQGreenPlacementAttributionNotConfirmedException
        : InvalidOperationException
    {
        public AQGreenPlacementAttributionNotConfirmedException()
            : base("AQGreen placement allocation requires immutable attribution confirmation.")
        {
        }
    }

    public sealed class AQGreenPlacementUnsupportedAttributionException
        : InvalidOperationException
    {
        public AQGreenPlacementUnsupportedAttributionException(
            AQGreenRecruitmentAttributionKind attributionKind)
            : base(
                $"AQGreen attribution kind '{attributionKind}' is not supported by the normal placement allocator.")
        {
            AttributionKind = attributionKind;
        }

        public AQGreenRecruitmentAttributionKind AttributionKind { get; }
    }

    public sealed class AQGreenPlacementConflictException
        : InvalidOperationException
    {
        public AQGreenPlacementConflictException(string message)
            : base(message)
        {
        }
    }
}
