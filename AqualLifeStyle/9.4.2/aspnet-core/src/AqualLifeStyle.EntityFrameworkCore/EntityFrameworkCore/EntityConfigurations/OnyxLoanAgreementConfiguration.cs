using System;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxLoanAgreementConfiguration
        : IEntityTypeConfiguration<OnyxLoanAgreement>
    {
        public void Configure(EntityTypeBuilder<OnyxLoanAgreement> builder)
        {
            builder.ToTable("OnyxLoanAgreements");

            builder.Ignore(agreement => agreement.RequiresPayoutHold);

            builder.Property(agreement => agreement.TenantId).IsRequired();
            builder.Property(agreement => agreement.EntryParticipationId).IsRequired();
            builder.Property(agreement => agreement.CustomerId).IsRequired();
            builder.Property(agreement => agreement.Status).IsRequired();
            builder.Property(agreement => agreement.TermsVersion).HasMaxLength(32).IsRequired();
            builder.Property(agreement => agreement.PrincipalAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(agreement => agreement.InterestRatePercent).HasPrecision(9, 4).IsRequired();
            builder.Property(agreement => agreement.TotalPayableAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(agreement => agreement.OutstandingAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(agreement => agreement.Currency).HasMaxLength(3).IsRequired();
            builder.Property(agreement => agreement.RepaymentPeriodMonths).IsRequired();
            builder.Property(agreement => agreement.InitialWeeklyRequirementCount).IsRequired();
            builder.Property(agreement => agreement.InitialWeeklyMinimumAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(agreement => agreement.OfferedAt).IsRequired();
            builder.Property(agreement => agreement.MemberConfirmation).HasMaxLength(512);

            builder.HasIndex(agreement => agreement.EntryParticipationId).IsUnique();
            builder.HasIndex(agreement => new
                {
                    agreement.TenantId,
                    agreement.CustomerId,
                    agreement.Status
                });

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(agreement => agreement.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(agreement => agreement.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(agreement => agreement.WeeklyRequirements)
                .WithOne()
                .HasForeignKey("OnyxLoanAgreementId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(agreement => agreement.WeeklyRequirements)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(agreement => agreement.Repayments)
                .WithOne()
                .HasForeignKey("OnyxLoanAgreementId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(agreement => agreement.Repayments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class OnyxLoanWeeklyRequirementConfiguration
        : IEntityTypeConfiguration<OnyxLoanWeeklyRequirement>
    {
        public void Configure(EntityTypeBuilder<OnyxLoanWeeklyRequirement> builder)
        {
            builder.ToTable("OnyxLoanWeeklyRequirements");

            builder.Property<Guid>("OnyxLoanAgreementId").IsRequired();
            builder.Property(requirement => requirement.RequirementNumber).IsRequired();
            builder.Property(requirement => requirement.MinimumAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(requirement => requirement.CreditedAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(requirement => requirement.DueAt).IsRequired();
            builder.Property(requirement => requirement.Status).IsRequired();

            builder.HasIndex(
                    "OnyxLoanAgreementId",
                    nameof(OnyxLoanWeeklyRequirement.RequirementNumber))
                .IsUnique();
        }
    }

    internal sealed class OnyxLoanRepaymentAllocationConfiguration
        : IEntityTypeConfiguration<OnyxLoanRepaymentAllocation>
    {
        public void Configure(EntityTypeBuilder<OnyxLoanRepaymentAllocation> builder)
        {
            builder.ToTable("OnyxLoanRepaymentAllocations");

            builder.Property<Guid>("OnyxLoanAgreementId").IsRequired();
            builder.Property(allocation => allocation.PaymentId).IsRequired();
            builder.Property(allocation => allocation.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(allocation => allocation.ReceivedAt).IsRequired();

            builder.HasIndex(allocation => allocation.PaymentId).IsUnique();

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(allocation => allocation.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
