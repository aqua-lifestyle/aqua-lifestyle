using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenFuneralCoverEntitlementConfiguration
        : IEntityTypeConfiguration<AQGreenFuneralCoverEntitlement>
    {
        public void Configure(EntityTypeBuilder<AQGreenFuneralCoverEntitlement> builder)
        {
            builder.ToTable("AQGreenFuneralCoverEntitlements");

            builder.Property(entitlement => entitlement.TenantId).IsRequired();
            builder.Property(entitlement => entitlement.EntryParticipationId).IsRequired();
            builder.Property(entitlement => entitlement.CustomerId).IsRequired();
            builder.Property(entitlement => entitlement.FuneralCoverAmount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(entitlement => entitlement.Currency)
                .HasMaxLength(3)
                .IsRequired();
            builder.Property(entitlement => entitlement.TermsVersion)
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(entitlement => entitlement.IncludedAt).IsRequired();
            builder.Property(entitlement => entitlement.Status).IsRequired();

            builder.HasIndex(entitlement => entitlement.EntryParticipationId)
                .IsUnique();
            builder.HasIndex(entitlement => new
                {
                    entitlement.TenantId,
                    entitlement.CustomerId,
                    entitlement.Status
                });

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(entitlement => entitlement.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(entitlement => entitlement.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
