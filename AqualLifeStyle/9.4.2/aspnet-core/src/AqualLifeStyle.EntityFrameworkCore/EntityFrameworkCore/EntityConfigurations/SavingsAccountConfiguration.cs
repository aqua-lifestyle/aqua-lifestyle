using System;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class SavingsAccountConfiguration
        : IEntityTypeConfiguration<SavingsAccount>
    {
        public void Configure(EntityTypeBuilder<SavingsAccount> builder)
        {
            builder.ToTable("SavingsAccounts");

            builder.Ignore(account => account.ProjectedMaturityAmount);

            builder.Property(account => account.TenantId).IsRequired();
            builder.Property(account => account.CustomerId).IsRequired();
            builder.Property(account => account.OpenedAt).IsRequired();
            builder.Property(account => account.MaturesAt).IsRequired();
            builder.Property(account => account.Status).IsRequired();
            builder.Property(account => account.PrincipalBalance)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(account => account.ProjectedInterestAmount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(account => account.MaturityPrincipalAmount)
                .HasPrecision(18, 2);
            builder.Property(account => account.MaturityInterestAmount)
                .HasPrecision(18, 2);
            builder.Property(account => account.MaturityPayoutAmount)
                .HasPrecision(18, 2);
            builder.Property(account => account.MaturityPeriodMonths).IsRequired();
            builder.Property(account => account.MinimumContributionAmount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(account => account.MaturityInterestRatePercent)
                .HasPrecision(9, 4)
                .IsRequired();
            builder.Property(account => account.ContributionWindowStartDay).IsRequired();
            builder.Property(account => account.ContributionWindowEndDay).IsRequired();
            builder.Property(account => account.Currency).HasMaxLength(3).IsRequired();
            builder.Property(account => account.TermsVersion).HasMaxLength(32).IsRequired();
            builder.Property(account => account.TermsEffectiveFrom).IsRequired();

            builder.HasIndex(account => new
                {
                    account.TenantId,
                    account.CustomerId,
                    account.Status
                });
            builder.HasIndex(account => new
                {
                    account.TenantId,
                    account.Status,
                    account.MaturesAt
                });

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(account => account.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(account => account.Contributions)
                .WithOne()
                .HasForeignKey("SavingsAccountId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(account => account.Contributions)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class SavingsContributionConfiguration
        : IEntityTypeConfiguration<SavingsContribution>
    {
        public void Configure(EntityTypeBuilder<SavingsContribution> builder)
        {
            builder.ToTable("SavingsContributions");

            builder.Property<Guid>("SavingsAccountId").IsRequired();
            builder.Property(contribution => contribution.PaymentId).IsRequired();
            builder.Property(contribution => contribution.Amount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(contribution => contribution.ContributedAt).IsRequired();
            builder.Property(contribution => contribution.InterestRatePercent)
                .HasPrecision(9, 4)
                .IsRequired();
            builder.Property(contribution => contribution.InterestAmount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(contribution => contribution.TermsVersion)
                .HasMaxLength(32)
                .IsRequired();

            builder.HasIndex(contribution => contribution.PaymentId)
                .IsUnique();
            builder.HasIndex("SavingsAccountId", nameof(SavingsContribution.ContributedAt));

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(contribution => contribution.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
