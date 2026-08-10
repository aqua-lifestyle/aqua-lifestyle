using System;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AreaActivationStateRecordConfiguration
        : IEntityTypeConfiguration<AreaActivationStateRecord>
    {
        public void Configure(EntityTypeBuilder<AreaActivationStateRecord> builder)
        {
            builder.ToTable("AreaActivationStateRecords", table =>
            {
                table.HasCheckConstraint(
                    "CK_AreaActivationStateRecords_Justification_NotBlank",
                    "length(trim(\"Justification\")) > 0");
                table.HasCheckConstraint(
                    "CK_AreaActivationStateRecords_EffectiveAt_RecordedAt",
                    "\"EffectiveAt\" <= \"RecordedAt\"");
                table.HasCheckConstraint(
                    "CK_AreaActivationStateRecords_Kind",
                    "\"Kind\" >= 0 AND \"Kind\" <= 2");
            });

            builder.Property(record => record.EffectiveAt).IsRequired();
            builder.Property(record => record.RecordedAt).IsRequired();
            builder.Property(record => record.Kind).IsRequired();
            builder.Property(record => record.Justification)
                .HasMaxLength(AreaActivationStateRecord.MaxJustificationLength)
                .IsRequired();
            builder.HasIndex(record => new { record.TenantId, record.EffectiveAt })
                .IsUnique();
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(record => record.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
