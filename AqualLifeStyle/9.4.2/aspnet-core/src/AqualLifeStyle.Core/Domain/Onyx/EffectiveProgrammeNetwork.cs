using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum ProgrammeNetworkKind
    {
        AQGreen = 0,
        Onyx = 1
    }

    public sealed class EffectiveNetworkParticipation
    {
        internal EffectiveNetworkParticipation(
            Guid participationId,
            int customerId,
            int? recruiterCustomerId,
            DateTime qualifiedUnderRecruiterAt)
        {
            ParticipationId = participationId;
            CustomerId = customerId;
            RecruiterCustomerId = recruiterCustomerId;
            QualifiedUnderRecruiterAt = qualifiedUnderRecruiterAt;
        }

        public Guid ParticipationId { get; }
        public int CustomerId { get; }
        public int? RecruiterCustomerId { get; }
        public DateTime QualifiedUnderRecruiterAt { get; }
    }

    public sealed class EffectiveProgrammeNetwork
    {
        private readonly IReadOnlyDictionary<int, EffectiveNetworkParticipation> _byCustomer;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<EffectiveNetworkParticipation>>
            _selectedChildrenByRecruiter;

        private EffectiveProgrammeNetwork(
            ProgrammeNetworkKind kind,
            DateTime cutoff,
            IReadOnlyCollection<EffectiveNetworkParticipation> participations)
        {
            Kind = kind;
            Cutoff = cutoff;
            _byCustomer = participations.ToDictionary(item => item.CustomerId);
            _selectedChildrenByRecruiter = participations
                .Where(item => item.RecruiterCustomerId.HasValue)
                .GroupBy(item => item.RecruiterCustomerId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<EffectiveNetworkParticipation>)group
                        .OrderBy(item => item.QualifiedUnderRecruiterAt)
                        .ThenBy(item => item.ParticipationId)
                        .Take(EntryNetworkQualificationEvaluator.BranchSize)
                        .ToList());
        }

        public ProgrammeNetworkKind Kind { get; }
        public DateTime Cutoff { get; }

        public bool ContainsCustomer(int customerId) => _byCustomer.ContainsKey(customerId);

        public IReadOnlyList<EffectiveNetworkParticipation> GetSelectedChildren(int customerId) =>
            _selectedChildrenByRecruiter.TryGetValue(customerId, out var children)
                ? children
                : Array.Empty<EffectiveNetworkParticipation>();

        public static EffectiveProgrammeNetwork BuildAQGreen(
            IEnumerable<EntryParticipation> participations,
            DateTime cutoff)
        {
            if (participations == null) throw new ArgumentNullException(nameof(participations));
            ValidateCutoff(cutoff);

            var active = participations
                .Where(item => item.Status == EntryParticipationStatus.Active)
                .ToList();
            if (active.Any(item => !item.ActivatedAt.HasValue))
            {
                throw new InvalidOperationException(
                    "An active AQGreen participation is missing activation evidence.");
            }

            var rows = active
                .Where(item => item.ActivatedAt.Value <= cutoff)
                .Select(item => BuildNode(
                    item.Id,
                    item.CustomerId,
                    item.RecruiterCustomerId,
                    item.StartedAt,
                    item.ActivatedAt.Value,
                    item.RecruiterCorrections.Select(correction => new PlacementCorrection(
                        correction.PreviousRecruiterCustomerId,
                        correction.NewRecruiterCustomerId,
                        correction.CorrectedAt)),
                    cutoff,
                    "AQGreen"))
                .ToList();
            return Create(ProgrammeNetworkKind.AQGreen, cutoff, rows);
        }

        public static EffectiveProgrammeNetwork BuildOnyx(
            IEnumerable<OnyxParticipation> participations,
            DateTime cutoff)
        {
            if (participations == null) throw new ArgumentNullException(nameof(participations));
            ValidateCutoff(cutoff);

            var active = participations
                .Where(item => item.Status == OnyxParticipationStatus.Active)
                .ToList();
            if (active.Any(item => !item.ActivatedAt.HasValue))
            {
                throw new InvalidOperationException(
                    "An active Onyx participation is missing activation evidence.");
            }

            var rows = active
                .Where(item => item.ActivatedAt.Value <= cutoff)
                .Select(item => BuildNode(
                    item.Id,
                    item.CustomerId,
                    item.RecruiterCustomerId,
                    item.StartedAt,
                    item.ActivatedAt.Value,
                    item.RecruiterCorrections.Select(correction => new PlacementCorrection(
                        correction.PreviousRecruiterCustomerId,
                        correction.NewRecruiterCustomerId,
                        correction.CorrectedAt)),
                    cutoff,
                    "Onyx"))
                .ToList();
            return Create(ProgrammeNetworkKind.Onyx, cutoff, rows);
        }

        internal static DateTime CurrentQualifiedUnderRecruiterAt(
            EntryParticipation participation)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (!participation.ActivatedAt.HasValue)
            {
                throw new InvalidOperationException(
                    "An active AQGreen participation is missing activation evidence.");
            }

            return BuildNode(
                participation.Id,
                participation.CustomerId,
                participation.RecruiterCustomerId,
                participation.StartedAt,
                participation.ActivatedAt.Value,
                participation.RecruiterCorrections.Select(correction => new PlacementCorrection(
                    correction.PreviousRecruiterCustomerId,
                    correction.NewRecruiterCustomerId,
                    correction.CorrectedAt)),
                DateTime.MaxValue,
                "AQGreen").QualifiedUnderRecruiterAt;
        }

        internal static DateTime CurrentQualifiedUnderRecruiterAt(
            OnyxParticipation participation)
        {
            if (participation == null) throw new ArgumentNullException(nameof(participation));
            if (!participation.ActivatedAt.HasValue)
            {
                throw new InvalidOperationException(
                    "An active Onyx participation is missing activation evidence.");
            }

            return BuildNode(
                participation.Id,
                participation.CustomerId,
                participation.RecruiterCustomerId,
                participation.StartedAt,
                participation.ActivatedAt.Value,
                participation.RecruiterCorrections.Select(correction => new PlacementCorrection(
                    correction.PreviousRecruiterCustomerId,
                    correction.NewRecruiterCustomerId,
                    correction.CorrectedAt)),
                DateTime.MaxValue,
                "Onyx").QualifiedUnderRecruiterAt;
        }

        private static EffectiveProgrammeNetwork Create(
            ProgrammeNetworkKind kind,
            DateTime cutoff,
            IReadOnlyCollection<EffectiveNetworkParticipation> rows)
        {
            var duplicateParticipation = rows
                .GroupBy(item => item.ParticipationId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateParticipation != null)
            {
                throw new InvalidOperationException(
                    $"Participation {duplicateParticipation.Key} occurs more than once in the effective network.");
            }

            var duplicateCustomer = rows
                .GroupBy(item => item.CustomerId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateCustomer != null)
            {
                throw new InvalidOperationException(
                    $"Customer {duplicateCustomer.Key} has more than one effective {kind} participation.");
            }

            var customerIds = rows.Select(item => item.CustomerId).ToHashSet();
            var danglingPlacement = rows.FirstOrDefault(item =>
                item.RecruiterCustomerId.HasValue &&
                !customerIds.Contains(item.RecruiterCustomerId.Value));
            if (danglingPlacement != null)
            {
                throw new InvalidOperationException(
                    $"Customer {danglingPlacement.CustomerId} has an unprovable {kind} recruiter placement at the cutoff.");
            }

            EnsureAcyclic(rows, kind);
            return new EffectiveProgrammeNetwork(kind, cutoff, rows);
        }

        private static EffectiveNetworkParticipation BuildNode(
            Guid participationId,
            int customerId,
            int? currentRecruiterCustomerId,
            DateTime startedAt,
            DateTime activatedAt,
            IEnumerable<PlacementCorrection> corrections,
            DateTime cutoff,
            string programme)
        {
            if (startedAt == default || activatedAt == default || activatedAt < startedAt)
            {
                throw new InvalidOperationException(
                    $"Customer {customerId} has invalid {programme} activation evidence.");
            }

            var ordered = corrections.OrderBy(item => item.CorrectedAt).ToList();
            var duplicateTime = ordered
                .GroupBy(item => item.CorrectedAt)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateTime != null)
            {
                throw new InvalidOperationException(
                    $"Customer {customerId} has ambiguous {programme} recruiter corrections at {duplicateTime.Key:O}.");
            }

            int? effectiveRecruiter = currentRecruiterCustomerId;
            var placementEffectiveAt = startedAt;
            if (ordered.Count > 0)
            {
                effectiveRecruiter = ordered[0].PreviousRecruiterCustomerId;
                int? expectedPrevious = effectiveRecruiter;
                foreach (var correction in ordered)
                {
                    if (correction.CorrectedAt < startedAt ||
                        correction.PreviousRecruiterCustomerId != expectedPrevious ||
                        correction.PreviousRecruiterCustomerId == correction.NewRecruiterCustomerId)
                    {
                        throw new InvalidOperationException(
                            $"Customer {customerId} has discontinuous {programme} recruiter history.");
                    }

                    if (correction.CorrectedAt <= cutoff)
                    {
                        effectiveRecruiter = correction.NewRecruiterCustomerId;
                        placementEffectiveAt = correction.CorrectedAt;
                    }

                    expectedPrevious = correction.NewRecruiterCustomerId;
                }

                if (expectedPrevious != currentRecruiterCustomerId)
                {
                    throw new InvalidOperationException(
                        $"Customer {customerId} has {programme} recruiter history that does not match current state.");
                }
            }

            if (effectiveRecruiter == customerId)
            {
                throw new InvalidOperationException(
                    $"Customer {customerId} cannot recruit themselves in the {programme} network.");
            }

            return new EffectiveNetworkParticipation(
                participationId,
                customerId,
                effectiveRecruiter,
                activatedAt > placementEffectiveAt ? activatedAt : placementEffectiveAt);
        }

        private static void EnsureAcyclic(
            IReadOnlyCollection<EffectiveNetworkParticipation> rows,
            ProgrammeNetworkKind kind)
        {
            var recruiterByCustomer = rows.ToDictionary(
                item => item.CustomerId,
                item => item.RecruiterCustomerId);
            foreach (var customerId in recruiterByCustomer.Keys)
            {
                var path = new HashSet<int>();
                var current = customerId;
                while (recruiterByCustomer.TryGetValue(current, out var recruiter) &&
                       recruiter.HasValue)
                {
                    if (!path.Add(current))
                    {
                        throw new InvalidOperationException(
                            $"The effective {kind} recruiter network contains a cycle involving customer {current}.");
                    }

                    current = recruiter.Value;
                }
            }
        }

        private static void ValidateCutoff(DateTime cutoff)
        {
            if (cutoff == default)
            {
                throw new ArgumentException("A network cutoff is required.", nameof(cutoff));
            }
        }

        private sealed class PlacementCorrection
        {
            public PlacementCorrection(
                int? previousRecruiterCustomerId,
                int? newRecruiterCustomerId,
                DateTime correctedAt)
            {
                PreviousRecruiterCustomerId = previousRecruiterCustomerId;
                NewRecruiterCustomerId = newRecruiterCustomerId;
                CorrectedAt = correctedAt;
            }

            public int? PreviousRecruiterCustomerId { get; }
            public int? NewRecruiterCustomerId { get; }
            public DateTime CorrectedAt { get; }
        }
    }
}
