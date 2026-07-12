using AqualLifeStyle.Domain.AreaLeaders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AreaSpaceConfiguration : IEntityTypeConfiguration<AreaSpace>
    {
        public void Configure(EntityTypeBuilder<AreaSpace> builder)
        {
            builder.ToTable("AreaSpaces");

            builder.Property(e => e.TenantId).IsRequired();
            builder.Property(e => e.AreaLeaderId).IsRequired();
            builder.Property(e => e.AddressLine).IsRequired().HasMaxLength(512);
            builder.Property(e => e.Capacity).IsRequired().HasMaxLength(64);
            builder.Property(e => e.InterestedMembers).IsRequired();
            builder.Property(e => e.Status).IsRequired();
            builder.Property(e => e.PresentationsCompleted).IsRequired();
            builder.Property(e => e.StartupOrdersCompleted).IsRequired();

            builder.HasIndex(e => new { e.TenantId, e.AreaLeaderId });
            builder.HasIndex(e => new { e.TenantId, e.Status });

            builder.HasOne<AreaLeader>()
                .WithMany()
                .HasForeignKey(e => e.AreaLeaderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
