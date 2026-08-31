using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenWeeklySalesEligibilityDecisionConfiguration
        : IEntityTypeConfiguration<AQGreenWeeklySalesEligibilityDecision>
    {
        public void Configure(
            EntityTypeBuilder<AQGreenWeeklySalesEligibilityDecision> builder)
        {
            builder.ToTable("AQGreenWeeklySalesEligibilityDecisions", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_RulesVersion_NotBlank",
                    "length(trim(\"SalesEligibilityRulesVersion\")) > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_RulesVersion_Supported",
                    $"\"SalesEligibilityRulesVersion\" = '{AQGreenWeeklySalesEligibilityRules.CurrentVersion}'");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_Status_Range",
                    "\"ReviewStatus\" IN (1, 2, 3)");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_Threshold_Range",
                    "\"ThresholdResult\" IS NULL OR \"ThresholdResult\" IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_Quantity_NonNegative",
                    "(\"ReviewedSprayQuantity\" IS NULL OR \"ReviewedSprayQuantity\" >= 0) AND " +
                    "(\"ReviewedOneLitreQuantity\" IS NULL OR \"ReviewedOneLitreQuantity\" >= 0) AND " +
                    "(\"ReviewedFiveLitreQuantity\" IS NULL OR \"ReviewedFiveLitreQuantity\" >= 0)");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_Reviewer_Positive",
                    "\"ReviewedByUserId\" IS NULL OR \"ReviewedByUserId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesDecisions_StateShape",
                    "(\"ReviewStatus\" = 1 AND \"ReviewedSprayQuantity\" IS NULL AND " +
                    "\"ReviewedOneLitreQuantity\" IS NULL AND \"ReviewedFiveLitreQuantity\" IS NULL AND " +
                    "\"ThresholdResult\" IS NULL AND \"ReviewedAt\" IS NULL AND " +
                    "\"ReviewedByUserId\" IS NULL AND \"RejectionReason\" IS NULL) OR " +
                    "(\"ReviewStatus\" = 2 AND \"ReviewedSprayQuantity\" IS NOT NULL AND " +
                    "\"ReviewedOneLitreQuantity\" IS NOT NULL AND \"ReviewedFiveLitreQuantity\" IS NOT NULL AND " +
                    "\"ThresholdResult\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL AND " +
                    "\"ReviewedByUserId\" IS NOT NULL AND \"RejectionReason\" IS NULL) OR " +
                    "(\"ReviewStatus\" = 3 AND \"ReviewedSprayQuantity\" IS NULL AND " +
                    "\"ReviewedOneLitreQuantity\" IS NULL AND \"ReviewedFiveLitreQuantity\" IS NULL AND " +
                    "\"ThresholdResult\" IS NULL AND \"ReviewedAt\" IS NOT NULL AND " +
                    "\"ReviewedByUserId\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL AND " +
                    "length(trim(\"RejectionReason\")) > 0)");
            });

            builder.Property(decision => decision.TenantId).IsRequired();
            builder.Property(decision => decision.ParticipantId).IsRequired();
            builder.Property(decision => decision.CommissionWeekStartUtc).IsRequired();
            builder.Property(decision => decision.SalesEligibilityRulesVersion)
                .HasMaxLength(AQGreenWeeklySalesEligibilityRules.MaximumRulesVersionLength)
                .IsRequired();
            builder.Property(decision => decision.ReviewStatus).IsRequired();
            builder.Property(decision => decision.RejectionReason)
                .HasMaxLength(AQGreenWeeklySalesEligibilityDecision.MaximumRejectionReasonLength);

            builder.HasAlternateKey(decision => new { decision.TenantId, decision.Id });
            builder.HasIndex(decision => new
            {
                decision.TenantId,
                decision.ParticipantId,
                decision.CommissionWeekStartUtc,
                decision.SalesEligibilityRulesVersion
            }).IsUnique();
            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(decision => new
                    { decision.TenantId, decision.ParticipantId })
                .HasPrincipalKey(participation => new
                    { participation.TenantId, participation.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(decision => decision.EvidenceReferences)
                .WithOne()
                .HasForeignKey(reference => new
                    { reference.TenantId, reference.DecisionId })
                .HasPrincipalKey(decision => new
                    { decision.TenantId, decision.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(decision => decision.EvidenceReferences)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class AQGreenWeeklySalesEvidenceReferenceConfiguration
        : IEntityTypeConfiguration<AQGreenWeeklySalesEvidenceReference>
    {
        public void Configure(
            EntityTypeBuilder<AQGreenWeeklySalesEvidenceReference> builder)
        {
            // Evidence IDs are assigned by the aggregate. Marking the key as
            // non-generated ensures a reference attached to an already tracked
            // Held decision is classified as Added rather than Modified.
            builder.Property(reference => reference.Id).ValueGeneratedNever();
            builder.ToTable("AQGreenWeeklySalesEvidenceReferences", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesEvidence_TenantId_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesEvidence_Source_Range",
                    "\"Source\" = 1");
                table.HasCheckConstraint(
                    "CK_AQGreenWeeklySalesEvidence_Reference_NotBlank",
                    "length(trim(\"TechnicalReference\")) > 0 AND " +
                    "\"TechnicalReference\" = trim(\"TechnicalReference\")");
            });
            builder.Property(reference => reference.TenantId).IsRequired();
            builder.Property(reference => reference.DecisionId).IsRequired();
            builder.Property(reference => reference.Source).IsRequired();
            builder.Property(reference => reference.TechnicalReference)
                .HasMaxLength(AQGreenWeeklySalesEvidenceReference.MaximumTechnicalReferenceLength)
                .IsRequired();
            builder.Property(reference => reference.RecordedAt).IsRequired();
            builder.HasIndex(reference => new
            {
                reference.TenantId,
                reference.DecisionId,
                reference.Source,
                reference.TechnicalReference
            }).IsUnique();
        }
    }
}
