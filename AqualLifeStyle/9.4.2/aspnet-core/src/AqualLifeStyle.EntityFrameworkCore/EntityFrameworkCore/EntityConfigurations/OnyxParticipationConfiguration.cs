using System;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxParticipationConfiguration : IEntityTypeConfiguration<OnyxParticipation>
    {
        public void Configure(EntityTypeBuilder<OnyxParticipation> builder)
        {
            builder.ToTable("OnyxParticipations");

            builder.Ignore(participation => participation.JoinedIndependently);

            builder.Property(participation => participation.TenantId).IsRequired();
            builder.Property(participation => participation.CustomerId).IsRequired();
            builder.Property(participation => participation.OnyxMembershipId).IsRequired();
            builder.Property(participation => participation.AdmissionRoute).IsRequired();
            builder.Property(participation => participation.Status).IsRequired();
            builder.Property(participation => participation.StartedAt).IsRequired();
            builder.Property(participation => participation.TermsVersion).HasMaxLength(32).IsRequired();
            builder.Property(participation => participation.TermsEffectiveFrom).IsRequired();
            builder.Property(participation => participation.DirectEntryAmount).HasPrecision(18, 2).IsRequired();
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

            builder.HasOne<Membership>()
                .WithMany()
                .HasForeignKey(participation => participation.OnyxMembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MemberPayment>()
                .WithMany()
                .HasForeignKey(participation => participation.DirectEntryPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(participation => participation.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<OnyxLoanAgreement>()
                .WithMany()
                .HasForeignKey(participation => participation.LoanAgreementId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(participation => participation.RecruiterCorrections)
                .WithOne()
                .HasForeignKey("OnyxParticipationId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(participation => participation.RecruiterCorrections)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class OnyxRecruiterCorrectionConfiguration
        : IEntityTypeConfiguration<OnyxRecruiterCorrection>
    {
        public void Configure(EntityTypeBuilder<OnyxRecruiterCorrection> builder)
        {
            builder.ToTable("OnyxRecruiterCorrections");
            builder.Property<Guid>("OnyxParticipationId").IsRequired();
            builder.Property(correction => correction.AdministratorUserId).IsRequired();
            builder.Property(correction => correction.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(correction => correction.CorrectedAt).IsRequired();
            builder.HasIndex("OnyxParticipationId");
        }
    }
}
