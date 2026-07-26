using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class DirectOnyxCheckoutIntentConfiguration
        : IEntityTypeConfiguration<DirectOnyxCheckoutIntent>
    {
        public void Configure(EntityTypeBuilder<DirectOnyxCheckoutIntent> builder)
        {
            builder.ToTable("DirectOnyxCheckoutIntents");
            builder.Property(intent => intent.TenantId).IsRequired();
            builder.Property(intent => intent.CustomerId).IsRequired();
            builder.Property(intent => intent.InviteCode)
                .HasMaxLength(DirectOnyxCheckoutIntent.MaxInviteCodeLength);
            builder.Property(intent => intent.OnyxMembershipId).IsRequired();
            builder.Property(intent => intent.TermsVersion).HasMaxLength(32).IsRequired();
            builder.Property(intent => intent.TermsEffectiveFrom).IsRequired();
            builder.Property(intent => intent.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(intent => intent.Currency).HasMaxLength(3).IsRequired();
            builder.Property(intent => intent.Status).IsRequired();
            builder.Property(intent => intent.ProviderCheckoutId)
                .HasMaxLength(DirectOnyxCheckoutIntent.MaxProviderCheckoutIdLength);
            builder.Property(intent => intent.CheckoutUrl)
                .HasMaxLength(DirectOnyxCheckoutIntent.MaxCheckoutUrlLength);
            builder.Property(intent => intent.CreatedAt).IsRequired();

            builder.HasIndex(intent => new { intent.TenantId, intent.CustomerId }).IsUnique();
            builder.HasIndex(intent => intent.ProviderCheckoutId).IsUnique();
            builder.HasIndex(intent => intent.PaymentId).IsUnique();
            builder.HasIndex(intent => intent.ParticipationId).IsUnique();

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(intent => intent.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(intent => intent.RecruiterCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Membership>()
                .WithMany()
                .HasForeignKey(intent => intent.OnyxMembershipId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(intent => intent.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OnyxParticipation>()
                .WithMany()
                .HasForeignKey(intent => intent.ParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
