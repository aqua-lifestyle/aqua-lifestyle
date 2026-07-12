using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class FacilitatorConfiguration : IEntityTypeConfiguration<Facilitator>
    {
        public void Configure(EntityTypeBuilder<Facilitator> builder)
        {
            builder.ToTable("Facilitators");

            builder.Property(e => e.TenantId).IsRequired();
            builder.Property(e => e.CustomerId).IsRequired();
            builder.Property(e => e.AreaLeaderId).IsRequired();
            builder.Property(e => e.Rank).IsRequired();
            builder.Property(e => e.DirectReferrals).IsRequired();
            builder.Property(e => e.IndirectReferrals).IsRequired();
            builder.Property(e => e.AwardBalance).IsRequired();

            builder.HasIndex(e => new { e.TenantId, e.CustomerId }).IsUnique();
            builder.HasIndex(e => new { e.TenantId, e.AreaLeaderId });

            builder.HasOne(e => e.AreaLeader)
                .WithMany()
                .HasForeignKey(e => e.AreaLeaderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
