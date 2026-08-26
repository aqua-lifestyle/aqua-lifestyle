using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class AQGreenRecruitmentAttributionPersistenceTests : AqualLifeStyleTestBase
    {
        [Fact]
        public void Model_SeparatesAttributionConfirmationAreaAndPlacement()
        {
            UsingDbContext(context =>
            {
                var attribution = context.Model.FindEntityType(
                    typeof(AQGreenRecruitmentAttribution));
                attribution.ShouldNotBeNull();
                attribution.GetTableName().ShouldBe("AQGreenRecruitmentAttributions");
                attribution.FindProperty("AreaId").ShouldBeNull();
                attribution.FindProperty("PlacementParentParticipantId").ShouldBeNull();
                attribution.FindProperty("PlacementTreeScopeId").ShouldBeNull();
                attribution.FindProperty("RecruiterCustomerId").ShouldBeNull();
                attribution.FindProperty(nameof(AQGreenRecruitmentAttribution.AttributionKind))
                    .ShouldNotBeNull();
                attribution.GetForeignKeys().Count().ShouldBe(3);
                attribution.GetForeignKeys().All(foreignKey =>
                        foreignKey.DeleteBehavior == DeleteBehavior.Restrict)
                    .ShouldBeTrue();
                attribution.GetForeignKeys().Single(foreignKey =>
                        foreignKey.PrincipalEntityType.ClrType.Name ==
                        "ProgrammeInvitation")
                    .Properties.Select(property => property.Name)
                    .ShouldBe(new[]
                    {
                        "SourceReferenceId",
                        "TenantId",
                        "CreditedSponsorParticipantId"
                    });
                attribution.GetIndexes().Single(index =>
                        index.Properties.Select(property => property.Name)
                            .SequenceEqual(new[] { "TenantId", "ParticipantId" }))
                    .IsUnique.ShouldBeTrue();

                var confirmation = context.Model.FindEntityType(
                    typeof(AQGreenRecruitmentAttributionConfirmation));
                confirmation.ShouldNotBeNull();
                confirmation.GetTableName()
                    .ShouldBe("AQGreenRecruitmentAttributionConfirmations");
                confirmation.FindProperty("ParticipantId").ShouldBeNull();
                confirmation.FindProperty("IsConfirmed").ShouldBeNull();
                confirmation.FindProperty("Status").ShouldBeNull();
                confirmation.GetForeignKeys().Single().DeleteBehavior
                    .ShouldBe(DeleteBehavior.Restrict);
                confirmation.GetIndexes().Single().IsUnique.ShouldBeTrue();
            });
        }

        [Fact]
        public async Task DbContext_RejectsTrackedAttributionAndConfirmationMutation()
        {
            var attribution = CreateAttribution();
            var confirmation = AQGreenRecruitmentAttributionConfirmation.Confirm(
                attribution,
                attribution.AttributedAt.AddMinutes(1),
                null,
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                Guid.NewGuid(),
                AQGreenRecruitmentAttributionRules.CurrentVersion);

            await AssertTrackedMutationRejectedAsync(attribution, EntityState.Modified);
            await AssertTrackedMutationRejectedAsync(attribution, EntityState.Deleted);
            await AssertTrackedMutationRejectedAsync(confirmation, EntityState.Modified);
            await AssertTrackedMutationRejectedAsync(confirmation, EntityState.Deleted);
        }

        private async Task AssertTrackedMutationRejectedAsync(object entity, EntityState state)
        {
            await Should.ThrowAsync<InvalidOperationException>(() =>
                UsingDbContextAsync(null, async context =>
                {
                    context.Attach(entity);
                    context.Entry(entity).State = state;
                    await context.SaveChangesAsync();
                }));
        }

        private static AQGreenRecruitmentAttribution CreateAttribution() =>
            AQGreenRecruitmentAttribution.Create(
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                AQGreenAcquisitionSource.MemberInvitation,
                Guid.NewGuid(),
                new DateTime(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc),
                null,
                null,
                AQGreenRecruitmentAttributionRules.CurrentVersion);
    }
}
