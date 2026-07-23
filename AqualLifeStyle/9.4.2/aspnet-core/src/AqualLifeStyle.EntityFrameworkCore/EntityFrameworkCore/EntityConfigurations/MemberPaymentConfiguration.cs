using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class MemberPaymentConfiguration : IEntityTypeConfiguration<MemberPayment>
    {
        public void Configure(EntityTypeBuilder<MemberPayment> builder)
        {
            builder.ToTable("MemberPayments");

            builder.Property(payment => payment.TenantId).IsRequired();
            builder.Property(payment => payment.CustomerId).IsRequired();
            builder.Property(payment => payment.Purpose).IsRequired();
            builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            builder.Property(payment => payment.Provider).HasMaxLength(64).IsRequired();
            builder.Property(payment => payment.ExternalReference).HasMaxLength(128).IsRequired();
            builder.Property(payment => payment.Status).IsRequired();
            builder.Property(payment => payment.InitiatedAt).IsRequired();

            builder.HasIndex(payment => new { payment.Provider, payment.ExternalReference })
                .IsUnique();
            builder.HasIndex(payment => new { payment.TenantId, payment.CustomerId, payment.Purpose });

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(payment => payment.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
