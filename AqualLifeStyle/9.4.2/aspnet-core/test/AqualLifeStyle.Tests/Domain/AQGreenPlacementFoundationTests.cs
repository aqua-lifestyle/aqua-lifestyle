using System;
using System.Linq;
using AqualLifeStyle.Domain.AQGreen;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AQGreenPlacementFoundationTests
    {
        private static readonly DateTime PlacedAt =
            new(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void RootPlacement_HasTheOnlyValidRootShape()
        {
            var scope = AQGreenPlacementTreeScope.Create(1);

            var root = AQGreenNetworkPlacement.CreateRoot(
                scope,
                Guid.NewGuid(),
                PlacedAt,
                AQGreenPlacementRules.CurrentVersion);

            root.TenantId.ShouldBe(scope.TenantId);
            root.PlacementTreeScopeId.ShouldBe(scope.Id);
            root.PlacementParentParticipantId.ShouldBeNull();
            root.PlacementSlot.ShouldBeNull();
            root.CanonicalPath.ShouldBe(string.Empty);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void ChildPlacement_AcceptsBoundarySlotsAndDerivesPath(int slot)
        {
            var root = CreateRoot();

            var child = AQGreenNetworkPlacement.CreateChild(
                root,
                Guid.NewGuid(),
                slot,
                PlacedAt.AddMinutes(1),
                AQGreenPlacementRules.CurrentVersion);

            child.TenantId.ShouldBe(root.TenantId);
            child.PlacementTreeScopeId.ShouldBe(root.PlacementTreeScopeId);
            child.PlacementParentParticipantId.ShouldBe(root.ParticipantId);
            child.PlacementSlot.ShouldBe(slot);
            child.CanonicalPath.ShouldBe(slot.ToString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        public void ChildPlacement_RejectsSlotsOutsideFiveWideTopology(int slot)
        {
            var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
                AQGreenNetworkPlacement.CreateChild(
                    CreateRoot(),
                    Guid.NewGuid(),
                    slot,
                    PlacedAt.AddMinutes(1),
                    AQGreenPlacementRules.CurrentVersion));

            exception.ParamName.ShouldBe("placementSlot");
        }

        [Fact]
        public void ChildPlacement_RejectsSelfParenting()
        {
            var root = CreateRoot();

            Should.Throw<InvalidOperationException>(() =>
                AQGreenNetworkPlacement.CreateChild(
                    root,
                    root.ParticipantId,
                    1,
                    PlacedAt.AddMinutes(1),
                    AQGreenPlacementRules.CurrentVersion));
        }

        [Fact]
        public void ChildPlacement_RejectsTimeBeforeParentPlacement()
        {
            var root = CreateRoot();

            var exception = Should.Throw<ArgumentException>(() =>
                AQGreenNetworkPlacement.CreateChild(
                    root,
                    Guid.NewGuid(),
                    1,
                    root.PlacedAt.AddTicks(-1),
                    AQGreenPlacementRules.CurrentVersion));

            exception.ParamName.ShouldBe("placedAt");
        }

        [Fact]
        public void ChildPlacement_DerivesDeepPathWithoutAConfiguredDepthLimit()
        {
            var placement = CreateRoot();

            for (var depth = 0; depth < 2048; depth++)
            {
                placement = AQGreenNetworkPlacement.CreateChild(
                    placement,
                    Guid.NewGuid(),
                    depth % 5 + 1,
                    PlacedAt.AddTicks(depth + 1),
                    AQGreenPlacementRules.CurrentVersion);
            }

            placement.CanonicalPath.Length.ShouldBe(2048);
            placement.CanonicalPath.All(character => character is >= '1' and <= '5')
                .ShouldBeTrue();
        }

        [Fact]
        public void TopologyProperties_HaveNoPublicMutationSurface()
        {
            var scopeProperties = new[] { "TenantId" };
            var placementProperties = new[]
            {
                "TenantId",
                "PlacementTreeScopeId",
                "ParticipantId",
                "PlacementParentParticipantId",
                "PlacementSlot",
                "CanonicalPath",
                "PlacedAt",
                "RulesVersion"
            };

            AssertNoPublicSetters(typeof(AQGreenPlacementTreeScope), scopeProperties);
            AssertNoPublicSetters(typeof(AQGreenNetworkPlacement), placementProperties);
        }

        private static AQGreenNetworkPlacement CreateRoot()
        {
            return AQGreenNetworkPlacement.CreateRoot(
                AQGreenPlacementTreeScope.Create(1),
                Guid.NewGuid(),
                PlacedAt,
                AQGreenPlacementRules.CurrentVersion);
        }

        private static void AssertNoPublicSetters(Type type, string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                type.GetProperty(propertyName)
                    .ShouldNotBeNull()
                    .GetSetMethod(nonPublic: false)
                    .ShouldBeNull($"{type.Name}.{propertyName} must not expose topology mutation");
            }
        }
    }
}
