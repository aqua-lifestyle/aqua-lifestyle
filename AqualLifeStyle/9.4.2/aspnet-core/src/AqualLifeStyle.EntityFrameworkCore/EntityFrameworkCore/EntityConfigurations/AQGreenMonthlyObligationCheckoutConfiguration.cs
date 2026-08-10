using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenMonthlyObligationCheckoutConfiguration
        : IEntityTypeConfiguration<AQGreenMonthlyObligationCheckout>
    {
        public void Configure(EntityTypeBuilder<AQGreenMonthlyObligationCheckout> builder)
        {
            builder.ToTable("AQGreenMonthlyObligationCheckouts", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGreenMonthlyObligationCheckouts_PeriodYear",
                    "\"PeriodYear\" >= 2000 AND \"PeriodYear\" <= 9999");
                table.HasCheckConstraint(
                    "CK_AQGreenMonthlyObligationCheckouts_PeriodMonth",
                    "\"PeriodMonth\" >= 1 AND \"PeriodMonth\" <= 12");
                table.HasCheckConstraint(
                    "CK_AQGreenMonthlyObligationCheckouts_AllocationStatus",
                    "\"AllocationStatus\" >= 0 AND \"AllocationStatus\" <= 2");
                table.HasCheckConstraint(
                    "CK_AQGreenMonthlyObligationCheckouts_Status",
                    "\"Status\" >= 0 AND \"Status\" <= 5");
                table.HasCheckConstraint(
                    "CK_AQGreenMonthlyObligationCheckouts_AllocationResult",
                    "(\"AllocationStatus\" = 0 AND \"Status\" IN (0, 1, 3, 4, 5) AND \"PaymentId\" IS NULL AND \"AllocationEvidence\" IS NULL) OR " +
                    "(\"AllocationStatus\" = 1 AND \"PaymentId\" IS NOT NULL AND \"Status\" = 2 AND \"AllocationEvidence\" IS NULL) OR " +
                    "(\"AllocationStatus\" = 2 AND \"PaymentId\" IS NOT NULL AND \"Status\" = 2 AND length(trim(\"AllocationEvidence\")) > 0)");
            });
            builder.Property(checkout => checkout.TenantId).IsRequired();
            builder.Property(checkout => checkout.EntryMonthlyObligationId).IsRequired();
            builder.Property(checkout => checkout.EntryParticipationId).IsRequired();
            builder.Property(checkout => checkout.CustomerId).IsRequired();
            builder.Property(checkout => checkout.PeriodYear).IsRequired();
            builder.Property(checkout => checkout.PeriodMonth).IsRequired();
            builder.Property(checkout => checkout.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(checkout => checkout.Currency).HasMaxLength(3).IsRequired();
            builder.Property(checkout => checkout.Status).IsRequired();
            builder.Property(checkout => checkout.AllocationStatus).IsRequired();
            builder.Property(checkout => checkout.AllocationEvidence)
                .HasMaxLength(AQGreenMonthlyObligationCheckout.MaxAllocationEvidenceLength);
            builder.Property(checkout => checkout.ProviderCheckoutId)
                .HasMaxLength(HostedPaymentCheckout.MaxProviderCheckoutIdLength);
            builder.Property(checkout => checkout.CheckoutUrl)
                .HasMaxLength(HostedPaymentCheckout.MaxCheckoutUrlLength);
            builder.Property(checkout => checkout.CreatedAt).IsRequired();
            builder.Property(checkout => checkout.TerminalEvidence).HasMaxLength(1000);

            builder.HasIndex(checkout => checkout.EntryMonthlyObligationId)
                .IsUnique()
                .HasFilter("\"Status\" IN (0, 1, 2)");
            builder.HasIndex(checkout => checkout.ProviderCheckoutId).IsUnique();
            builder.HasIndex(checkout => checkout.PaymentId);
            builder.HasIndex(checkout => new
            {
                checkout.TenantId,
                checkout.CustomerId,
                checkout.Status
            });

            builder.HasOne<EntryMonthlyObligation>()
                .WithMany()
                .HasForeignKey(checkout => checkout.EntryMonthlyObligationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(checkout => checkout.EntryParticipationId)
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
