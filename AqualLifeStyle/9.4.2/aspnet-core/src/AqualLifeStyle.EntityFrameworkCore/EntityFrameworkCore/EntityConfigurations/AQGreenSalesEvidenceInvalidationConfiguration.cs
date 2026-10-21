using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenSalesEvidenceInvalidationConfiguration
        : IEntityTypeConfiguration<AQGreenSalesEvidenceInvalidation>
    {
        public void Configure(EntityTypeBuilder<AQGreenSalesEvidenceInvalidation> builder)
        {
            builder.ToTable("AQGreenSalesEvidenceInvalidations", table =>
            {
                table.HasCheckConstraint("CK_AQGSalesInvalidation_Authority", "\"TenantId\" > 0 AND \"RecordedByUserId\" > 0");
                table.HasCheckConstraint("CK_AQGSalesInvalidation_Reason", "length(trim(\"Reason\")) > 0 AND \"Reason\" = trim(\"Reason\")");
            });
            builder.Property(item => item.Reason).IsRequired()
                .HasMaxLength(AQGreenSalesEvidenceInvalidation.MaximumReasonLength);
            builder.HasIndex(item => new { item.TenantId, item.EvidenceId }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.DecisionId });
            // The composite FK prevents substitution across either Tenant or review.
            builder.HasOne<AQGreenWeeklySalesEvidenceReference>().WithMany()
                .HasForeignKey(item => new { item.TenantId, item.DecisionId, item.EvidenceId })
                .HasPrincipalKey(item => new { item.TenantId, item.DecisionId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
