using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenJoiningCheckoutConfiguration
        : IEntityTypeConfiguration<AQGreenJoiningCheckout>
    {
        public void Configure(EntityTypeBuilder<AQGreenJoiningCheckout> builder)
        {
            builder.ToTable("AQGreenJoiningCheckouts");
            builder.Property(checkout => checkout.TenantId).IsRequired();
            builder.Property(checkout => checkout.ParticipationId).IsRequired();
            builder.Property(checkout => checkout.CustomerId).IsRequired();
            builder.Property(checkout => checkout.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(checkout => checkout.Currency).HasMaxLength(3).IsRequired();
            builder.Property(checkout => checkout.Status).IsRequired();
            builder.Property(checkout => checkout.Schedule).IsRequired();
            builder.Property(checkout => checkout.Stage).IsRequired();
            builder.Property(checkout => checkout.ProviderCheckoutId)
                .HasMaxLength(HostedPaymentCheckout.MaxProviderCheckoutIdLength);
            builder.Property(checkout => checkout.CheckoutUrl)
                .HasMaxLength(HostedPaymentCheckout.MaxCheckoutUrlLength);
            builder.Property(checkout => checkout.CreatedAt).IsRequired();
            builder.Property(checkout => checkout.TerminalEvidence).HasMaxLength(1000);

            builder.HasIndex(checkout => checkout.ParticipationId)
                .IsUnique()
                .HasFilter("\"Status\" IN (0, 1)");
            builder.HasIndex(checkout => checkout.ProviderCheckoutId).IsUnique();
            builder.HasIndex(checkout => checkout.PaymentId).IsUnique();

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(checkout => checkout.ParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(checkout => checkout.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(checkout => checkout.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
