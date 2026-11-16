using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenIndependentRootRequestConfiguration : IEntityTypeConfiguration<AQGreenIndependentRootRequest>
    {
        public void Configure(EntityTypeBuilder<AQGreenIndependentRootRequest> builder)
        {
            builder.ToTable("AQGreenIndependentRootRequests", table =>
            {
                table.HasCheckConstraint("CK_AQGIndependentRoot_Tenant", "\"TenantId\" > 0");
                table.HasCheckConstraint("CK_AQGIndependentRoot_Actor", "\"RequestedByUserId\" > 0");
            });
            builder.Property(row => row.TenantId).IsRequired();
            builder.Property(row => row.ParticipantId).IsRequired();
            builder.Property(row => row.RequestedByUserId).IsRequired();
            builder.Property(row => row.RequestedAt).IsRequired();
            builder.HasIndex(row => new { row.TenantId, row.ParticipantId }).IsUnique();
            builder.HasOne<EntryParticipation>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.ParticipantId })
                .HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
