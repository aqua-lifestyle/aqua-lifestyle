using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class ReferralConfiguration : IEntityTypeConfiguration<Referral>
    {
        public void Configure(EntityTypeBuilder<Referral> builder)
        {
            builder.ToTable("Referrals");

            builder.Property(e => e.TenantId).IsRequired();
            builder.Property(e => e.ReferredCustomerId).IsRequired();
            builder.Property(e => e.SourceEnquiryId).IsRequired();
            builder.Property(e => e.Type).IsRequired();
            builder.Property(e => e.AwardAmount).IsRequired();
            builder.Property(e => e.AwardIssued).IsRequired();

            builder.HasIndex(e => new { e.TenantId, e.ReferrerFacilitatorId });
            builder.HasIndex(e => new { e.TenantId, e.ReferrerAreaLeaderId });
            builder.HasIndex(e => new { e.TenantId, e.ReferredCustomerId });
            builder.HasIndex(e => new { e.TenantId, e.SourceEnquiryId });

            builder.HasOne<Facilitator>()
                .WithMany()
                .HasForeignKey(e => e.ReferrerFacilitatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<AreaLeader>()
                .WithMany()
                .HasForeignKey(e => e.ReferrerAreaLeaderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.ReferredCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Enquiry>()
                .WithMany()
                .HasForeignKey(e => e.SourceEnquiryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
