using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenV2WeeklyCommissionEvidenceConfiguration
        : IEntityTypeConfiguration<AQGreenV2WeeklyCommissionEvidence>
    {
        public void Configure(
            EntityTypeBuilder<AQGreenV2WeeklyCommissionEvidence> builder)
        {
            builder.ToTable("AQGreenV2WeeklyCommissionEvidence", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_Versions_NotBlank",
                    "length(trim(\"PlacementRulesVersion\")) > 0 AND " +
                    "length(trim(\"StructuralQualificationRulesVersion\")) > 0 AND " +
                    "length(trim(\"CommissionDecisionRulesVersion\")) > 0 AND " +
                    "length(trim(\"EvidenceSchemaVersion\")) > 0 AND " +
                    "(\"SalesApplicability\" = 1 OR " +
                    "length(trim(\"SalesEligibilityRulesVersion\")) > 0)");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_Versions_Supported",
                    $"\"PlacementRulesVersion\" = '{AQGreenPlacementRules.CurrentVersion}' AND " +
                    $"\"StructuralQualificationRulesVersion\" = '{AQGreenStructuralQualificationRules.CurrentVersion}' AND " +
                    $"\"CommissionDecisionRulesVersion\" = '{AQGreenCommissionDecisionRules.CurrentVersion}' AND " +
                    $"\"EvidenceSchemaVersion\" = '{AQGreenV2WeeklyCommissionEvidenceSchema.CurrentVersion}' AND " +
                    $"((\"SalesApplicability\" = {(int)AQGreenWeeklySalesApplicability.NotApplicable} AND \"SalesEligibilityRulesVersion\" IS NULL) OR " +
                    $"(\"SalesApplicability\" = {(int)AQGreenWeeklySalesApplicability.Applicable} AND \"SalesEligibilityRulesVersion\" = '{AQGreenWeeklySalesEligibilityRules.CurrentVersion}'))");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_Level_Range",
                    "\"QualifiedStructuralLevel\" IN (0, 1, 2, 3) AND " +
                    "\"CommissionedLevel\" IN (0, 1, 2, 3) AND " +
                    "(\"CommissionedLevel\" = 0 OR " +
                    "\"CommissionedLevel\" = \"QualifiedStructuralLevel\")");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_Counts",
                    "\"QualifyingDepth1Count\" BETWEEN 0 AND 5 AND " +
                    "\"QualifyingDepth2Count\" BETWEEN 0 AND 25 AND " +
                    "\"QualifyingDepth3Count\" BETWEEN 0 AND 125 AND " +
                    "\"EvidenceNodeCount\" BETWEEN 1 AND 156");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_SalesShape",
                    "(\"SalesApplicability\" = 1 AND " +
                    "\"WeeklySalesEligibilityDecisionId\" IS NULL AND " +
                    "\"SalesReviewStatus\" IS NULL AND " +
                    "\"SalesThresholdResult\" IS NULL AND " +
                    "\"SalesReviewedAt\" IS NULL AND " +
                    "\"SalesReviewedByUserId\" IS NULL) OR " +
                    "(\"SalesApplicability\" = 2 AND " +
                    "\"WeeklySalesEligibilityDecisionId\" IS NOT NULL AND " +
                    "\"SalesReviewStatus\" = 2 AND " +
                    "\"SalesThresholdResult\" IS NOT NULL AND " +
                    "\"SalesThresholdResult\" IN (1, 2)) OR " +
                    "(\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 3 AND " +
                    "\"WeeklySalesEligibilityDecisionId\" IS NOT NULL AND " +
                    "\"SalesThresholdResult\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_SalesApplicability",
                    "\"SalesApplicability\" IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_CommissionGate",
                    "(\"SalesApplicability\" = 1 AND \"QualifiedStructuralLevel\" = 0 AND " +
                    "\"CommissionedLevel\" = 0) OR " +
                    "(\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 2 AND " +
                    "\"SalesThresholdResult\" IS NOT NULL AND " +
                    "\"SalesThresholdResult\" = 1 AND " +
                    "\"CommissionedLevel\" = \"QualifiedStructuralLevel\") OR " +
                    "((\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 2 AND " +
                    "\"SalesThresholdResult\" IS NOT NULL AND " +
                    "\"SalesThresholdResult\" = 2) OR " +
                    "(\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 3)) AND " +
                    "\"CommissionedLevel\" = 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidence_Reviewer_Positive",
                    "\"SalesApplicability\" = 1 OR \"SalesReviewedByUserId\" > 0");
            });

            builder.Property(evidence => evidence.Id)
                .HasColumnName("EntryWeeklyCommissionId");
            builder.Property(evidence => evidence.TenantId).IsRequired();
            builder.Property(evidence => evidence.EntryParticipationId).IsRequired();
            builder.Property(evidence => evidence.WeeklySalesEligibilityDecisionId)
                .IsRequired(false);
            builder.Property(evidence => evidence.PlacementTreeScopeId).IsRequired();
            builder.Property(evidence => evidence.Cutoff).IsRequired();
            builder.Property(evidence => evidence.PlacementRulesVersion)
                .HasMaxLength(AQGreenPlacementRules.MaximumRulesVersionLength)
                .IsRequired();
            builder.Property(evidence => evidence.StructuralQualificationRulesVersion)
                .HasMaxLength(AQGreenStructuralQualificationRules.MaximumRulesVersionLength)
                .IsRequired();
            builder.Property(evidence => evidence.SalesEligibilityRulesVersion)
                .HasMaxLength(AQGreenWeeklySalesEligibilityRules.MaximumRulesVersionLength)
                .IsRequired(false);
            builder.Property(evidence => evidence.CommissionDecisionRulesVersion)
                .HasMaxLength(AQGreenCommissionDecisionRules.MaximumVersionLength)
                .IsRequired();
            builder.Property(evidence => evidence.EvidenceSchemaVersion)
                .HasMaxLength(AQGreenV2WeeklyCommissionEvidenceSchema.MaximumVersionLength)
                .IsRequired();
            builder.Property(evidence => evidence.SalesApplicability)
                .IsRequired();

            builder.HasAlternateKey(evidence => new { evidence.TenantId, evidence.Id });
            builder.HasOne<EntryWeeklyCommission>()
                .WithOne()
                .HasForeignKey<AQGreenV2WeeklyCommissionEvidence>(evidence =>
                    new { evidence.TenantId, evidence.Id })
                .HasPrincipalKey<EntryWeeklyCommission>(commission =>
                    new { commission.TenantId, commission.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(evidence => new
                    { evidence.TenantId, evidence.EntryParticipationId })
                .HasPrincipalKey(participation => new
                    { participation.TenantId, participation.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AQGreenWeeklySalesEligibilityDecision>()
                .WithMany()
                .HasForeignKey(evidence => new
                    { evidence.TenantId, evidence.WeeklySalesEligibilityDecisionId })
                .HasPrincipalKey(decision => new { decision.TenantId, decision.Id })
                .IsRequired(false)
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

    internal sealed class AQGreenV2WeeklyCommissionEvidenceNodeConfiguration
        : IEntityTypeConfiguration<AQGreenV2WeeklyCommissionEvidenceNode>
    {
        public void Configure(
            EntityTypeBuilder<AQGreenV2WeeklyCommissionEvidenceNode> builder)
        {
            builder.ToTable("AQGreenV2WeeklyCommissionEvidenceNodes", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidenceNodes_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidenceNodes_Ordinal",
                    "\"CanonicalOrdinal\" BETWEEN 0 AND 155");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidenceNodes_Identity",
                    "\"CustomerIdObserved\" > 0 AND \"UserIdObserved\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenV2CommissionEvidenceNodes_Status",
                    "\"ParticipationStatusObserved\" IN (0, 1, 2, 3, 4)");
            });

            builder.HasKey(node => new { node.EvidenceId, node.CanonicalOrdinal });
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
