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
            int qualifyingDepth1Count,
            int qualifyingDepth2Count,
            int qualifyingDepth3Count,
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
                throw new AQGreenPlacementTopologyIntegrityException(
                    $"AQGreen structural evaluation returned unsupported completion " +
                    $"level {(int)structuralCompletionLevel}.");
            }
            EnsureQualifyingCount(qualifyingDepth1Count, 1);
            EnsureQualifyingCount(qualifyingDepth2Count, 2);
            EnsureQualifyingCount(qualifyingDepth3Count, 3);

            var calculatedLevel = AQGreenStructuralCompletionCalculator.Evaluate(
                relativeDepth => relativeDepth switch
                {
                    1 => qualifyingDepth1Count,
                    2 => qualifyingDepth2Count,
                    3 => qualifyingDepth3Count,
                    _ => throw new ArgumentOutOfRangeException(nameof(relativeDepth))
                });
            if (calculatedLevel != structuralCompletionLevel)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    "AQGreen structural completion level contradicts its " +
                    "qualifying relative-depth evidence.");
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
            QualifyingDepth1Count = qualifyingDepth1Count;
            QualifyingDepth2Count = qualifyingDepth2Count;
            QualifyingDepth3Count = qualifyingDepth3Count;
            Cutoff = cutoff;
            RulesVersion = rulesVersion.Trim();
        }

        public Guid ParticipantId { get; }
        public Guid PlacementTreeScopeId { get; }
        public AQGreenStructuralCompletionLevel StructuralCompletionLevel { get; }

        /// <summary>
        /// Cutoff-qualifying V2 placement occupants at relative depths one,
        /// two, and three. These counts are produced from the same topology,
        /// participant predicate, cutoff, Tenant/scope checks, and fail-closed
        /// lifecycle/security evidence as <see cref="StructuralCompletionLevel"/>.
        /// They are placement occupancy, not recruitment, sales, commission,
        /// graduation, or Area counts.
        /// </summary>
        public int QualifyingDepth1Count { get; }
        public int QualifyingDepth2Count { get; }
        public int QualifyingDepth3Count { get; }

        public int GetQualifyingCountAtRelativeDepth(int relativeDepth) =>
            relativeDepth switch
            {
                1 => QualifyingDepth1Count,
                2 => QualifyingDepth2Count,
                3 => QualifyingDepth3Count,
                _ => throw new ArgumentOutOfRangeException(nameof(relativeDepth))
            };

        /// <summary>
        /// The boundary applied to historically represented placement and
        /// participation-activation facts, not a snapshot time for current-only
        /// Customer, User, or unresolved D08 state.
        /// </summary>
        public DateTime Cutoff { get; }
        public string RulesVersion { get; }

        private static void EnsureQualifyingCount(int count, int relativeDepth)
        {
            var maximum = AQGreenStructuralCompletionCalculator
                .GetRequiredPopulation(relativeDepth);
            if (count < 0 || count > maximum)
            {
                throw new AQGreenPlacementTopologyIntegrityException(
                    $"AQGreen qualifying relative-depth-{relativeDepth} count " +
                    $"{count} is outside the supported range 0..{maximum}.");
            }
        }
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
