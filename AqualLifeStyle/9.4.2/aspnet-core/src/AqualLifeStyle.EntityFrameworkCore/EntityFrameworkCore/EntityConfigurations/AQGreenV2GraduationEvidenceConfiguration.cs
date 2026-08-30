using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenV2GraduationEvidenceConfiguration
        : IEntityTypeConfiguration<AQGreenV2GraduationEvidence>
    {
        public void Configure(EntityTypeBuilder<AQGreenV2GraduationEvidence> builder)
        {
            builder.ToTable("AQGreenV2GraduationEvidence", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidence_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidence_StructuralVersion_NotBlank",
                    "length(trim(\"StructuralQualificationRulesVersion\")) > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidence_SchemaVersion_NotBlank",
                    "length(trim(\"EvidenceSchemaVersion\")) > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidence_Result_NonNegative",
                    "\"QualifyingDepth1Count\" >= 0 AND " +
                    "\"QualifyingDepth2Count\" >= 0 AND \"EvidenceNodeCount\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidence_Level_Range",
                    "\"EvaluatedStructuralCompletionLevel\" IN (0, 1, 2, 3)");
            });

            builder.Property(evidence => evidence.Id)
                .HasColumnName("OnyxGraduationDecisionId");
            builder.Property(evidence => evidence.TenantId).IsRequired();
            builder.Property(evidence => evidence.Cutoff).IsRequired();
            builder.Property(evidence => evidence.StructuralQualificationRulesVersion)
                .HasMaxLength(AQGreenStructuralQualificationRules.MaximumRulesVersionLength)
                .IsRequired();
            builder.Property(evidence => evidence.EvidenceSchemaVersion)
                .HasMaxLength(AQGreenV2GraduationEvidenceSchema.MaximumVersionLength)
                .IsRequired();
            builder.Property(evidence => evidence.EvaluatedStructuralCompletionLevel)
                .IsRequired();
            builder.Property(evidence => evidence.QualifyingDepth1Count).IsRequired();
            builder.Property(evidence => evidence.QualifyingDepth2Count).IsRequired();
            builder.Property(evidence => evidence.EvidenceNodeCount).IsRequired();

            builder.HasAlternateKey(evidence => new { evidence.TenantId, evidence.Id });
            builder.HasOne<OnyxGraduationDecision>()
                .WithOne()
                .HasForeignKey<AQGreenV2GraduationEvidence>(evidence =>
                    new { evidence.TenantId, evidence.Id })
                .HasPrincipalKey<OnyxGraduationDecision>(decision =>
                    new { decision.TenantId, decision.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(evidence => evidence.Nodes)
                .WithOne()
                .HasForeignKey(node => new { node.TenantId, node.EvidenceId })
                .HasPrincipalKey(evidence => new { evidence.TenantId, evidence.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(evidence => evidence.Nodes)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class AQGreenV2GraduationEvidenceNodeConfiguration
        : IEntityTypeConfiguration<AQGreenV2GraduationEvidenceNode>
    {
        public void Configure(EntityTypeBuilder<AQGreenV2GraduationEvidenceNode> builder)
        {
            builder.ToTable("AQGreenV2GraduationEvidenceNodes", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidenceNodes_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidenceNodes_CanonicalOrdinal_NonNegative",
                    "\"CanonicalOrdinal\" >= 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidenceNodes_CustomerId_Positive",
                    "\"CustomerIdObserved\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidenceNodes_UserId_Positive",
                    "\"UserIdObserved\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2GraduationEvidenceNodes_ParticipationStatus_Range",
                    "\"ParticipationStatusObserved\" IN (0, 1, 2, 3, 4)");
            });

            builder.HasKey(node => new { node.EvidenceId, node.CanonicalOrdinal });
            builder.Property(node => node.TenantId).IsRequired();
            builder.Property(node => node.EvidenceId).IsRequired();
            builder.Property(node => node.CanonicalOrdinal).IsRequired();
            builder.Property(node => node.SourcePlacementId).IsRequired();
            builder.Property(node => node.ParticipationStatusObserved).IsRequired();
            builder.Property(node => node.ParticipationActivatedAtObserved);
            builder.Property(node => node.ParticipationIsDeletedObserved).IsRequired();
            builder.Property(node => node.CustomerIdObserved).IsRequired();
            builder.Property(node => node.CustomerTenantMatchedObserved).IsRequired();
            builder.Property(node => node.CustomerIsActiveObserved).IsRequired();
            builder.Property(node => node.CustomerIsDeletedObserved).IsRequired();
            builder.Property(node => node.UserIdObserved).IsRequired();
            builder.Property(node => node.UserTenantMatchedObserved).IsRequired();
            builder.Property(node => node.UserIsActiveObserved).IsRequired();
            builder.Property(node => node.UserIsDeletedObserved).IsRequired();

            builder.HasIndex(node => new { node.EvidenceId, node.SourcePlacementId })
                .IsUnique();
            builder.HasOne<AQGreenNetworkPlacement>()
                .WithMany()
                .HasForeignKey(node => new { node.TenantId, node.SourcePlacementId })
                .HasPrincipalKey(placement => new { placement.TenantId, placement.Id })
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
