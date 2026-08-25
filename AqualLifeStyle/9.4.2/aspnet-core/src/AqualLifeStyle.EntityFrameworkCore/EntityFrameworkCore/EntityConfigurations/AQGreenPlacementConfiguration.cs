using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenPlacementTreeScopeConfiguration
        : IEntityTypeConfiguration<AQGreenPlacementTreeScope>
    {
        public void Configure(EntityTypeBuilder<AQGreenPlacementTreeScope> builder)
        {
            builder.ToTable("AQGreenPlacementTreeScopes", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenPlacementTreeScopes_TenantId_Positive",
                    "\"TenantId\" > 0");
            });

            builder.Property(scope => scope.TenantId).IsRequired();
            builder.HasAlternateKey(scope => new { scope.TenantId, scope.Id });
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(scope => scope.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal sealed class AQGreenNetworkPlacementConfiguration
        : IEntityTypeConfiguration<AQGreenNetworkPlacement>
    {
        public void Configure(EntityTypeBuilder<AQGreenNetworkPlacement> builder)
        {
            builder.ToTable("AQGreenNetworkPlacements", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenNetworkPlacements_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenNetworkPlacements_RootOrNonRootShape",
                    "(\"PlacementParentParticipantId\" IS NULL AND " +
                    "\"PlacementSlot\" IS NULL AND \"CanonicalPath\" = '') OR " +
                    "(\"PlacementParentParticipantId\" IS NOT NULL AND " +
                    "\"PlacementSlot\" IS NOT NULL AND \"CanonicalPath\" <> '')");
                table.HasCheckConstraint(
                    "CK_AQGreenNetworkPlacements_PlacementSlot_Range",
                    "\"PlacementSlot\" IS NULL OR " +
                    $"(\"PlacementSlot\" >= 1 AND \"PlacementSlot\" <= {AQGreenPlacementRules.MaximumPlacementSlot})");
                table.HasCheckConstraint(
                    "CK_AQGreenNetworkPlacements_NoSelfParent",
                    "\"PlacementParentParticipantId\" IS NULL OR " +
                    "\"ParticipantId\" <> \"PlacementParentParticipantId\"");
                table.HasCheckConstraint(
                    "CK_AQGreenNetworkPlacements_CanonicalPath_Characters",
                    "length(replace(replace(replace(replace(replace(" +
                    "\"CanonicalPath\", '1', ''), '2', ''), '3', ''), " +
                    "'4', ''), '5', '')) = 0");
                table.HasCheckConstraint(
                    "CK_AQGreenNetworkPlacements_RulesVersion_NotBlank",
                    "length(trim(\"RulesVersion\")) > 0");
            });

            builder.Property(placement => placement.TenantId).IsRequired();
            builder.Property(placement => placement.PlacementTreeScopeId).IsRequired();
            builder.Property(placement => placement.ParticipantId).IsRequired();
            builder.Property(placement => placement.PlacementSlot);
            builder.Property(placement => placement.CanonicalPath)
                .HasColumnType("text")
                .IsRequired();
            builder.Property(placement => placement.PlacedAt).IsRequired();
            builder.Property(placement => placement.RulesVersion)
                .HasMaxLength(AQGreenPlacementRules.MaximumRulesVersionLength)
                .IsRequired();

            builder.HasIndex(placement => new
                { placement.TenantId, placement.ParticipantId })
                .IsUnique();
            builder.HasAlternateKey(placement => new
            {
                placement.TenantId,
                placement.PlacementTreeScopeId,
                placement.ParticipantId
            });
            builder.HasIndex(placement => new
                { placement.TenantId, placement.PlacementTreeScopeId })
                .IsUnique()
                .HasFilter("\"PlacementParentParticipantId\" IS NULL");
            builder.HasIndex(placement => new
            {
                placement.TenantId,
                placement.PlacementTreeScopeId,
                placement.PlacementParentParticipantId,
                placement.PlacementSlot
            })
                .IsUnique()
                .HasFilter(
                    "\"PlacementParentParticipantId\" IS NOT NULL AND " +
                    "\"PlacementSlot\" IS NOT NULL");

            builder.HasOne<AQGreenPlacementTreeScope>()
                .WithMany()
                .HasForeignKey(placement => new
                    { placement.TenantId, placement.PlacementTreeScopeId })
                .HasPrincipalKey(scope => new { scope.TenantId, scope.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(placement => new
                    { placement.TenantId, placement.ParticipantId })
                .HasPrincipalKey(participation => new
                    { participation.TenantId, participation.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AQGreenNetworkPlacement>()
                .WithMany()
                .HasForeignKey(placement => new
                {
                    placement.TenantId,
                    placement.PlacementTreeScopeId,
                    placement.PlacementParentParticipantId
                })
                .HasPrincipalKey(parent => new
                {
                    parent.TenantId,
                    parent.PlacementTreeScopeId,
                    parent.ParticipantId
                })
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
