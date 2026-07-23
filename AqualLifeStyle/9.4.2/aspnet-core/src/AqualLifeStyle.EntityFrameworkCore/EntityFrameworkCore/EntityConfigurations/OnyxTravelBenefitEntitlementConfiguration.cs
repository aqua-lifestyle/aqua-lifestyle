using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxTravelBenefitEntitlementConfiguration
        : IEntityTypeConfiguration<OnyxTravelBenefitEntitlement>
    {
        public void Configure(EntityTypeBuilder<OnyxTravelBenefitEntitlement> builder)
        {
            builder.ToTable("OnyxTravelBenefitEntitlements");

            builder.Property(entitlement => entitlement.TenantId).IsRequired();
            builder.Property(entitlement => entitlement.OnyxParticipationId).IsRequired();
            builder.Property(entitlement => entitlement.CustomerId).IsRequired();
            builder.Property(entitlement => entitlement.QualifiedNetworkLevel).IsRequired();
            builder.Property(entitlement => entitlement.RequiredNetworkLevel).IsRequired();
            builder.Property(entitlement => entitlement.EligibleAt).IsRequired();
            builder.Property(entitlement => entitlement.WaitingPeriodEndsAt).IsRequired();
            builder.Property(entitlement => entitlement.Status).IsRequired();
            builder.Property(entitlement => entitlement.WaitingPeriodMonths).IsRequired();
            builder.Property(entitlement => entitlement.MemberTripContributionPercent)
                .HasPrecision(9, 4)
                .IsRequired();
            builder.Property(entitlement => entitlement.TermsVersion)
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(entitlement => entitlement.TermsEffectiveFrom).IsRequired();

            builder.HasIndex(entitlement => entitlement.OnyxParticipationId)
                .IsUnique();
            builder.HasIndex(entitlement => new
                {
                    entitlement.TenantId,
                    entitlement.Status,
                    entitlement.WaitingPeriodEndsAt
                });

            builder.HasOne<OnyxParticipation>()
                .WithMany()
                .HasForeignKey(entitlement => entitlement.OnyxParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(entitlement => entitlement.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
