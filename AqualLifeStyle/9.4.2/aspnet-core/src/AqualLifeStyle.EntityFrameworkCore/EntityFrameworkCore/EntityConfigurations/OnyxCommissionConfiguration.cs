using System;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxCommissionPeriodConfiguration
        : IEntityTypeConfiguration<OnyxCommissionPeriod>
    {
        public void Configure(EntityTypeBuilder<OnyxCommissionPeriod> builder)
        {
            builder.ToTable("OnyxCommissionPeriods");

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

    internal sealed class OnyxWeeklyCommissionConfiguration
        : IEntityTypeConfiguration<OnyxWeeklyCommission>
    {
        public void Configure(EntityTypeBuilder<OnyxWeeklyCommission> builder)
        {
            builder.ToTable("OnyxWeeklyCommissions");

            builder.Property(commission => commission.TenantId).IsRequired();
            builder.Property(commission => commission.OnyxParticipationId).IsRequired();
            builder.Property(commission => commission.CustomerId).IsRequired();
            builder.Property(commission => commission.CommissionPeriodId).IsRequired();
            builder.Property(commission => commission.HighestQualifiedNetworkLevel).IsRequired();
            builder.Property(commission => commission.HighestCommissionedLevel).IsRequired();
            builder.Property(commission => commission.TotalAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(commission => commission.Currency).HasMaxLength(3).IsRequired();
            builder.Property(commission => commission.RulesVersion).HasMaxLength(32).IsRequired();
            builder.Property(commission => commission.CalculatedAt).IsRequired();
            builder.Property(commission => commission.PayoutStatus).IsRequired();
            builder.Property(commission => commission.ReleaseReason).HasMaxLength(1000);
            builder.Property(commission => commission.PaymentReference).HasMaxLength(128);

            builder.HasIndex(commission => new
                {
                    commission.OnyxParticipationId,
                    commission.CommissionPeriodId
                })
                .IsUnique();
            builder.HasIndex(commission => new
                {
                    commission.TenantId,
                    commission.CustomerId,
                    commission.PayoutStatus
                });

            builder.HasOne<OnyxParticipation>()
                .WithMany()
                .HasForeignKey(commission => commission.OnyxParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(commission => commission.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<OnyxCommissionPeriod>()
                .WithMany()
                .HasForeignKey(commission => commission.CommissionPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(commission => commission.Components)
                .WithOne()
                .HasForeignKey("OnyxWeeklyCommissionId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(commission => commission.Components)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class OnyxCommissionComponentConfiguration
        : IEntityTypeConfiguration<OnyxCommissionComponent>
    {
        public void Configure(EntityTypeBuilder<OnyxCommissionComponent> builder)
        {
            builder.ToTable("OnyxCommissionComponents");

            builder.Property<Guid>("OnyxWeeklyCommissionId").IsRequired();
            builder.Property(component => component.Level).IsRequired();
            builder.Property(component => component.Amount).HasPrecision(18, 2).IsRequired();

            builder.HasIndex("OnyxWeeklyCommissionId", nameof(OnyxCommissionComponent.Level))
                .IsUnique();
        }
    }
}
