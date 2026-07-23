using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class EntryMonthlyObligationConfiguration
        : IEntityTypeConfiguration<EntryMonthlyObligation>
    {
        public void Configure(EntityTypeBuilder<EntryMonthlyObligation> builder)
        {
            builder.ToTable("EntryMonthlyObligations");

            builder.Ignore(obligation => obligation.IsOwnPayoutEligible);

            builder.Property(obligation => obligation.TenantId).IsRequired();
            builder.Property(obligation => obligation.EntryParticipationId).IsRequired();
            builder.Property(obligation => obligation.CustomerId).IsRequired();
            builder.Property(obligation => obligation.PeriodYear).IsRequired();
            builder.Property(obligation => obligation.PeriodMonth).IsRequired();
            builder.Property(obligation => obligation.AmountDue).HasPrecision(18, 2).IsRequired();
            builder.Property(obligation => obligation.OutstandingAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(obligation => obligation.Currency).HasMaxLength(3).IsRequired();
            builder.Property(obligation => obligation.TermsVersion).HasMaxLength(32).IsRequired();
            builder.Property(obligation => obligation.DueAt).IsRequired();
            builder.Property(obligation => obligation.GracePeriodDays).IsRequired();
            builder.Property(obligation => obligation.GracePeriodEndsAt).IsRequired();
            builder.Property(obligation => obligation.Status).IsRequired();

            builder.HasIndex(obligation => new
                {
                    obligation.EntryParticipationId,
                    obligation.PeriodYear,
                    obligation.PeriodMonth
                })
                .IsUnique();
            builder.HasIndex(obligation => new
                {
                    obligation.TenantId,
                    obligation.CustomerId,
                    obligation.Status
                });

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(obligation => obligation.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(obligation => obligation.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(obligation => obligation.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
