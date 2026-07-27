using System;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class EntryParticipationConfiguration : IEntityTypeConfiguration<EntryParticipation>
    {
        public void Configure(EntityTypeBuilder<EntryParticipation> builder)
        {
            builder.ToTable("EntryParticipations");

            builder.Ignore(participation => participation.JoinedIndependently);
            builder.Ignore(participation => participation.IsQualifiedForNetwork);

            builder.Property(participation => participation.TenantId).IsRequired();
            builder.Property(participation => participation.CustomerId).IsRequired();
            builder.Property(participation => participation.Status).IsRequired();
            builder.Property(participation => participation.StartedAt).IsRequired();
            builder.Property(participation => participation.TermsVersion).HasMaxLength(32).IsRequired();
            builder.Property(participation => participation.TermsEffectiveFrom).IsRequired();
            builder.Property(participation => participation.JoiningPaymentAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(participation => participation.RegistrationPaymentAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(participation => participation.ActivationPaymentAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(participation => participation.MonthlyCommitmentAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(participation => participation.GracePeriodDays).IsRequired();
            builder.Property(participation => participation.Currency).HasMaxLength(3).IsRequired();

            builder.HasIndex(participation => new { participation.TenantId, participation.CustomerId })
                .IsUnique();
            builder.HasIndex(participation => participation.RecruiterCustomerId);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(participation => participation.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(participation => participation.RecruiterCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(participation => participation.JoiningPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(participation => participation.RegistrationPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(participation => participation.ActivationPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(participation => participation.RecruiterCorrections)
                .WithOne()
                .HasForeignKey("EntryParticipationId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(participation => participation.RecruiterCorrections)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class EntryRecruiterCorrectionConfiguration
        : IEntityTypeConfiguration<EntryRecruiterCorrection>
    {
        public void Configure(EntityTypeBuilder<EntryRecruiterCorrection> builder)
        {
            builder.ToTable("EntryRecruiterCorrections");

            builder.Property<Guid>("EntryParticipationId").IsRequired();
            builder.Property(correction => correction.AdministratorUserId).IsRequired();
            builder.Property(correction => correction.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(correction => correction.CorrectedAt).IsRequired();

            builder.HasIndex("EntryParticipationId");
        }
    }
}
