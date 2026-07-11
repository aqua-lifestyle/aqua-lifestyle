using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AreaLeaderConfiguration : IEntityTypeConfiguration<AreaLeader>
    {
        public void Configure(EntityTypeBuilder<AreaLeader> builder)
        {
            builder.ToTable("AreaLeaders");

            builder.Property(e => e.TenantId).IsRequired();
            builder.Property(e => e.CustomerId).IsRequired();
            builder.Property(e => e.LicenseType).IsRequired();
            builder.Property(e => e.LicenseFee).IsRequired();
            builder.Property(e => e.Rank).IsRequired();
            builder.Property(e => e.AreaSpaceId);
            builder.Property(e => e.MonthlySubscription).IsRequired();
            builder.Property(e => e.DirectReferrals).IsRequired();
            builder.Property(e => e.IndirectReferrals).IsRequired();
            builder.Property(e => e.OrderTarget).IsRequired();

            builder.HasIndex(e => new { e.TenantId, e.CustomerId });
            builder.HasIndex(e => new { e.TenantId, e.AreaSpaceId });

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<AreaSpace>()
                .WithMany()
                .HasForeignKey(e => e.AreaSpaceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
