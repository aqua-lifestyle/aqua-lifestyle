using System;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class EntryCommissionPeriodConfiguration
        : IEntityTypeConfiguration<EntryCommissionPeriod>
    {
        public void Configure(EntityTypeBuilder<EntryCommissionPeriod> builder)
        {
            builder.ToTable("EntryCommissionPeriods");

            builder.Property(period => period.TenantId).IsRequired();
            builder.Property(period => period.PeriodStart).IsRequired();
            builder.Property(period => period.PeriodEnd).IsRequired();
            builder.Property(period => period.TimeZoneId).HasMaxLength(64).IsRequired();
            builder.Property(period => period.CalculatedAt).IsRequired();
            builder.Property(period => period.RulesVersion).HasMaxLength(32).IsRequired();

            builder.HasIndex(period => new
                {
                    period.TenantId,
                    period.PeriodStart,
                    period.PeriodEnd
                })
                .IsUnique();
        }
    }

    internal sealed class EntryWeeklyCommissionConfiguration
        : IEntityTypeConfiguration<EntryWeeklyCommission>
    {
        public void Configure(EntityTypeBuilder<EntryWeeklyCommission> builder)
        {
            builder.ToTable("EntryWeeklyCommissions");

            builder.Property(commission => commission.TenantId).IsRequired();
            builder.Property(commission => commission.EntryParticipationId).IsRequired();
            builder.Property(commission => commission.CustomerId).IsRequired();
            builder.Property(commission => commission.CommissionPeriodId).IsRequired();
            builder.Property(commission => commission.HighestCompletedLevel).IsRequired();
            builder.Ignore(commission => commission.HighestQualifiedNetworkLevel);
            builder.Ignore(commission => commission.HighestCommissionedLevel);
            builder.Property(commission => commission.TotalAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(commission => commission.Currency).HasMaxLength(3).IsRequired();
            builder.Property(commission => commission.RulesVersion).HasMaxLength(32).IsRequired();
            builder.Property(commission => commission.CalculatedAt).IsRequired();
            builder.Property(commission => commission.PayoutStatus).IsRequired();
            builder.Property(commission => commission.HoldReason).HasMaxLength(500);
            builder.Property(commission => commission.ReleaseReason).HasMaxLength(1000);
            builder.Property(commission => commission.PaymentReference).HasMaxLength(128);

            builder.HasIndex(commission => new
                {
                    commission.EntryParticipationId,
                    commission.CommissionPeriodId
                })
                .IsUnique();
            builder.HasIndex(commission => new
                {
                    commission.TenantId,
                    commission.CustomerId,
                    commission.PayoutStatus
                });

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(commission => commission.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(commission => commission.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<EntryCommissionPeriod>()
                .WithMany()
                .HasForeignKey(commission => commission.CommissionPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(commission => commission.Components)
                .WithOne()
                .HasForeignKey("EntryWeeklyCommissionId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(commission => commission.Components)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class EntryCommissionComponentConfiguration
        : IEntityTypeConfiguration<EntryCommissionComponent>
    {
        public void Configure(EntityTypeBuilder<EntryCommissionComponent> builder)
        {
            builder.ToTable("EntryCommissionComponents");

            builder.Property<Guid>("EntryWeeklyCommissionId").IsRequired();
            builder.Property(component => component.Level).IsRequired();
            builder.Property(component => component.Amount).HasPrecision(18, 2).IsRequired();

            builder.HasIndex("EntryWeeklyCommissionId", nameof(EntryCommissionComponent.Level))
                .IsUnique();
        }
    }
}
