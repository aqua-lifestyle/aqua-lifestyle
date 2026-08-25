using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class AQGreenPlacementFoundationPersistenceTests : AqualLifeStyleTestBase
    {
        [Fact]
        public void Model_ContainsOnlyTopologyFoundationFieldsWithRestrictRelationships()
        {
            UsingDbContext(context =>
            {
                var placement = context.Model.FindEntityType(typeof(AQGreenNetworkPlacement));
                placement.ShouldNotBeNull();
                placement.GetTableName().ShouldBe("AQGreenNetworkPlacements");
                placement.FindProperty("AreaId").ShouldBeNull();
                placement.FindProperty("PlacementSequence").ShouldBeNull();
                placement.FindProperty("CreditedSponsorParticipantId").ShouldBeNull();
                placement.GetForeignKeys().Count().ShouldBe(3);
                placement.GetForeignKeys().All(foreignKey =>
                        foreignKey.DeleteBehavior == DeleteBehavior.Restrict)
                    .ShouldBeTrue();

                var scope = context.Model.FindEntityType(typeof(AQGreenPlacementTreeScope));
                scope.ShouldNotBeNull();
                scope.GetTableName().ShouldBe("AQGreenPlacementTreeScopes");
                scope.FindProperty("RootParticipantId").ShouldBeNull();
                scope.FindProperty("AreaId").ShouldBeNull();
            });
        }

        [Fact]
        public async Task DbContext_RejectsTrackedTopologyUpdatesAndDeletes()
        {
            var scope = AQGreenPlacementTreeScope.Create(1);
            var placement = AQGreenNetworkPlacement.CreateRoot(
                scope,
                Guid.NewGuid(),
                new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc),
                AQGreenPlacementRules.CurrentVersion);

            await AssertTrackedMutationRejectedAsync(scope, EntityState.Modified);
            await AssertTrackedMutationRejectedAsync(scope, EntityState.Deleted);
            await AssertTrackedMutationRejectedAsync(placement, EntityState.Modified);
            await AssertTrackedMutationRejectedAsync(placement, EntityState.Deleted);
        }

        private async Task AssertTrackedMutationRejectedAsync(
            object entity,
            EntityState state)
        {
            await Should.ThrowAsync<InvalidOperationException>(() =>
                UsingDbContextAsync(null, async context =>
                {
                    context.Attach(entity);
                    context.Entry(entity).State = state;
                    await context.SaveChangesAsync();
                }));
        }
    }
}
