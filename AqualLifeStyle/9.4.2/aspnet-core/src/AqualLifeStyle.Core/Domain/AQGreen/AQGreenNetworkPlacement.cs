using System;
using System.Globalization;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    public static class AQGreenPlacementRules
    {
        public const int MaximumRulesVersionLength = 64;
        public const int MaximumPlacementSlot = 5;
        public const string CurrentVersion = "AQGreenPlacementV2";
    }

    /// <summary>
    /// Permanent AQGreen topology. Parent participant plus slot are authoritative;
    /// CanonicalPath is an immutable representation derived from those facts.
    /// </summary>
    public sealed class AQGreenNetworkPlacement
        : AggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; private set; }
        public Guid PlacementTreeScopeId { get; private set; }
        public Guid ParticipantId { get; private set; }
        public Guid? PlacementParentParticipantId { get; private set; }
        public int? PlacementSlot { get; private set; }
        public string CanonicalPath { get; private set; }
        public DateTime PlacedAt { get; private set; }
        public string RulesVersion { get; private set; }

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenNetworkPlacement()
        {
        }

        private AQGreenNetworkPlacement(
            int tenantId,
            Guid placementTreeScopeId,
            Guid participantId,
            Guid? placementParentParticipantId,
            int? placementSlot,
            string canonicalPath,
            DateTime placedAt,
            string rulesVersion)
        {
            EnsureIdentity(tenantId, placementTreeScopeId, participantId);
            EnsurePlacedAt(placedAt);
            var normalizedRulesVersion = NormalizeRulesVersion(rulesVersion);

            Id = Guid.NewGuid();
            TenantId = tenantId;
            PlacementTreeScopeId = placementTreeScopeId;
            ParticipantId = participantId;
            PlacementParentParticipantId = placementParentParticipantId;
            PlacementSlot = placementSlot;
            CanonicalPath = canonicalPath;
            PlacedAt = placedAt;
            RulesVersion = normalizedRulesVersion;
        }

        public static AQGreenNetworkPlacement CreateRoot(
            AQGreenPlacementTreeScope scope,
            Guid participantId,
            DateTime placedAt,
            string rulesVersion)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            return new AQGreenNetworkPlacement(
                scope.TenantId,
                scope.Id,
                participantId,
                null,
                null,
                string.Empty,
                placedAt,
                rulesVersion);
        }

        public static AQGreenNetworkPlacement CreateChild(
            AQGreenNetworkPlacement parent,
            Guid participantId,
            int placementSlot,
            DateTime placedAt,
            string rulesVersion)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (placementSlot < 1 ||
                placementSlot > AQGreenPlacementRules.MaximumPlacementSlot)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(placementSlot),
                    $"AQGreen placement slots must be between 1 and {AQGreenPlacementRules.MaximumPlacementSlot}.");
            }
            if (participantId == parent.ParticipantId)
            {
                throw new InvalidOperationException(
                    "An AQGreen participant cannot be their own placement parent.");
            }
            if (placedAt < parent.PlacedAt)
            {
                throw new ArgumentException(
                    "An AQGreen child placement cannot precede its parent placement.",
                    nameof(placedAt));
            }

            return new AQGreenNetworkPlacement(
                parent.TenantId,
                parent.PlacementTreeScopeId,
                participantId,
                parent.ParticipantId,
                placementSlot,
                parent.CanonicalPath + placementSlot.ToString(CultureInfo.InvariantCulture),
                placedAt,
                rulesVersion);
        }

        private static void EnsureIdentity(
            int tenantId,
            Guid placementTreeScopeId,
            Guid participantId)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (placementTreeScopeId == Guid.Empty)
                throw new ArgumentException(
                    "A placement-tree scope is required.",
                    nameof(placementTreeScopeId));
            if (participantId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen participation is required.",
                    nameof(participantId));
        }

        private static void EnsurePlacedAt(DateTime placedAt)
        {
            if (placedAt == default || placedAt.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "An authoritative UTC placement time is required.",
                    nameof(placedAt));
            }
        }

        private static string NormalizeRulesVersion(string rulesVersion)
        {
            if (string.IsNullOrWhiteSpace(rulesVersion))
                throw new ArgumentException(
                    "A placement rules version is required.",
                    nameof(rulesVersion));

            var normalized = rulesVersion.Trim();
            if (normalized.Length > AQGreenPlacementRules.MaximumRulesVersionLength)
                throw new ArgumentException(
                    $"Placement rules versions cannot exceed {AQGreenPlacementRules.MaximumRulesVersionLength} characters.",
                    nameof(rulesVersion));
            return normalized;
        }
    }
}
