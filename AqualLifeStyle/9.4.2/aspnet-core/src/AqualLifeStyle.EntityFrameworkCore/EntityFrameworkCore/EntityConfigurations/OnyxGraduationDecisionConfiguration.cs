using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxGraduationDecisionConfiguration
        : IEntityTypeConfiguration<OnyxGraduationDecision>
    {
        public void Configure(EntityTypeBuilder<OnyxGraduationDecision> builder)
        {
            builder.ToTable("OnyxGraduationDecisions", table =>
            {
                table.HasCheckConstraint(
                    "CK_OnyxGraduationDecisions_StructuralModel_Range",
                    $"\"StructuralModel\" IN ({(int)AQGreenGraduationStructuralModel.LegacyV1}, " +
                    $"{(int)AQGreenGraduationStructuralModel.PlacementV2})");
                table.HasCheckConstraint(
                    "CK_OnyxGraduationDecisions_StructuralModel_LevelShape",
                    $"(\"StructuralModel\" = {(int)AQGreenGraduationStructuralModel.LegacyV1} AND " +
                    "\"EvaluatedNetworkLevel\" IS NOT NULL) OR " +
                    $"(\"StructuralModel\" = {(int)AQGreenGraduationStructuralModel.PlacementV2} AND " +
                    "\"EvaluatedNetworkLevel\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_OnyxGraduationDecisions_GraduationRulesVersion_NotBlank",
                    "\"GraduationRulesVersion\" IS NULL OR " +
                    "length(trim(\"GraduationRulesVersion\")) > 0");
                table.HasCheckConstraint(
                    "CK_OnyxGraduationDecisions_EvaluatedLoanTermsVersion_NotBlank",
                    "\"EvaluatedLoanTermsVersion\" IS NULL OR " +
                    "length(trim(\"EvaluatedLoanTermsVersion\")) > 0");
                table.HasCheckConstraint(
                    "CK_OnyxGraduationDecisions_VersionSnapshots_Required",
                    "\"GraduationRulesVersion\" IS NOT NULL AND " +
                    "\"EvaluatedLoanTermsVersion\" IS NOT NULL");
            });
            builder.Property(decision => decision.TenantId).IsRequired();
            builder.Property(decision => decision.CustomerId).IsRequired();
            builder.Property(decision => decision.EntryParticipationId).IsRequired();
            builder.Property(decision => decision.LoanAgreementId).IsRequired();
            builder.Property(decision => decision.OnyxParticipationId).IsRequired();
            builder.Property(decision => decision.AdministratorUserId).IsRequired();
            builder.Property(decision => decision.DecidedAt).IsRequired();
            builder.Property(decision => decision.Justification).HasMaxLength(2000).IsRequired();
            builder.Property(decision => decision.StructuralModel).IsRequired();
            builder.Property(decision => decision.GraduationRulesVersion)
                .HasMaxLength(OnyxGraduationRules.MaximumVersionLength);
            builder.Property(decision => decision.EvaluatedNetworkLevel);
            builder.Property(decision => decision.EvaluatedFundingAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(decision => decision.EvaluatedFundingCurrency).HasMaxLength(3).IsRequired();
            builder.Property(decision => decision.EvaluatedLoanTermsVersion)
                .HasMaxLength(32);

            builder.HasAlternateKey(decision => new { decision.TenantId, decision.Id });
            builder.HasIndex(decision => decision.EntryParticipationId).IsUnique();
            builder.HasIndex(decision => decision.LoanAgreementId).IsUnique();
            builder.HasIndex(decision => decision.OnyxParticipationId).IsUnique();
            builder.HasIndex(decision => new { decision.TenantId, decision.CustomerId });

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(decision => decision.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OnyxLoanAgreement>()
                .WithMany()
                .HasForeignKey(decision => decision.LoanAgreementId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OnyxParticipation>()
                .WithMany()
                .HasForeignKey(decision => decision.OnyxParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
