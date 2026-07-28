using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class YocoWebhookReceiptConfiguration
        : IEntityTypeConfiguration<YocoWebhookReceipt>
    {
        public void Configure(EntityTypeBuilder<YocoWebhookReceipt> builder)
        {
            builder.ToTable("YocoWebhookReceipts");
            builder.Property(receipt => receipt.TenantId).IsRequired();
            builder.Property(receipt => receipt.EventId)
                .HasMaxLength(YocoWebhookReceipt.MaxEventIdLength)
                .IsRequired();
            builder.Property(receipt => receipt.PaymentId)
                .HasMaxLength(YocoWebhookReceipt.MaxPaymentIdLength)
                .IsRequired();
            builder.Property(receipt => receipt.ProviderCheckoutId)
                .HasMaxLength(HostedPaymentCheckout.MaxProviderCheckoutIdLength)
                .IsRequired();
            builder.Property(receipt => receipt.PayloadHash)
                .HasMaxLength(YocoWebhookReceipt.Sha256HexLength)
                .IsFixedLength()
                .IsRequired();
            builder.Property(receipt => receipt.Programme).IsRequired();
            builder.Property(receipt => receipt.CheckoutReferenceId).IsRequired();
            builder.Property(receipt => receipt.ProcessedAt).IsRequired();

            builder.HasIndex(receipt => receipt.EventId).IsUnique();
            builder.HasIndex(receipt => receipt.PaymentId);
            builder.HasIndex(receipt => receipt.ProviderCheckoutId);
        }
    }
}
