using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public enum AQGreenStructuralCompletionLevel
    {
        Level0 = 0,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3
    }

    public interface IAQGreenStructuralCompletionEvaluator
    {
        /// <summary>
        /// Evaluates immutable placement facts and historically represented
        /// participation-activation evidence effective at
        /// <paramref name="cutoff"/>. Customer, user, and unresolved D08
        /// lifecycle/security facts that are only represented as current state
        /// are checked at evaluation time and fail closed when encountered.
        /// This contract does not promise a complete historical replay of those
        /// current-only facts and is not a durable financial decision snapshot.
        /// </summary>
        /// <param name="cutoff">
        /// The UTC boundary for historically represented placement and
        /// participation-activation facts.
        /// </param>
        Task<AQGreenStructuralCompletionResult> EvaluateAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A successful structural projection under the evaluator's mixed evidence
    /// semantics. The result does not certify that current-only identity,
    /// lifecycle, or security facts were reconstructed historically.
    /// </summary>
    public sealed class AQGreenStructuralCompletionResult
    {
        public AQGreenStructuralCompletionResult(
            Guid participantId,
            Guid placementTreeScopeId,
            AQGreenStructuralCompletionLevel structuralCompletionLevel,
            DateTime cutoff,
            string rulesVersion)
        {
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));
            if (structuralCompletionLevel < AQGreenStructuralCompletionLevel.Level0 ||
                structuralCompletionLevel > AQGreenStructuralCompletionLevel.Level3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(structuralCompletionLevel));
            }
            if (cutoff == default || cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "An authoritative UTC structural evaluation cutoff is required.",
                    nameof(cutoff));
            if (string.IsNullOrWhiteSpace(rulesVersion))
                throw new ArgumentException(
                    "A structural rules version is required.",
                    nameof(rulesVersion));

            ParticipantId = participantId;
            PlacementTreeScopeId = placementTreeScopeId;
            StructuralCompletionLevel = structuralCompletionLevel;
            Cutoff = cutoff;
            RulesVersion = rulesVersion.Trim();
        }

        public Guid ParticipantId { get; }
        public Guid PlacementTreeScopeId { get; }
        public AQGreenStructuralCompletionLevel StructuralCompletionLevel { get; }

        /// <summary>
        /// The boundary applied to historically represented placement and
        /// participation-activation facts, not a snapshot time for current-only
        /// Customer, User, or unresolved D08 state.
        /// </summary>
        public DateTime Cutoff { get; }
        public string RulesVersion { get; }
    }

    public sealed class AQGreenStructuralEvaluationNotPlacedException
        : InvalidOperationException
    {
        public AQGreenStructuralEvaluationNotPlacedException(
            Guid participantId,
            DateTime cutoff)
            : base(
                $"AQGreen participant {participantId} has no authoritative " +
                $"placement at structural evaluation cutoff {cutoff:O}.")
        {
            ParticipantId = participantId;
            Cutoff = cutoff;
        }

        public Guid ParticipantId { get; }
        public DateTime Cutoff { get; }
    }

    public sealed class AQGreenStructuralContributionPolicyRequiredException
        : InvalidOperationException
    {
        public AQGreenStructuralContributionPolicyRequiredException(Guid participantId)
            : base(
                $"AQGreen participant {participantId} has a post-Active lifecycle " +
                "or security state whose structural contribution requires the " +
                "unresolved AQG-V2-D08 business decision.")
        {
            ParticipantId = participantId;
        }

        public Guid ParticipantId { get; }
    }
}
