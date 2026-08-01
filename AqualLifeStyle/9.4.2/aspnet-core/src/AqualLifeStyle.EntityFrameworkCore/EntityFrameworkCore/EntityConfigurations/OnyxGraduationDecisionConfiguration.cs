using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class OnyxGraduationDecisionConfiguration
        : IEntityTypeConfiguration<OnyxGraduationDecision>
    {
        public void Configure(EntityTypeBuilder<OnyxGraduationDecision> builder)
        {
            builder.ToTable("OnyxGraduationDecisions");
            builder.Property(decision => decision.TenantId).IsRequired();
            builder.Property(decision => decision.CustomerId).IsRequired();
            builder.Property(decision => decision.EntryParticipationId).IsRequired();
            builder.Property(decision => decision.LoanAgreementId).IsRequired();
            builder.Property(decision => decision.OnyxParticipationId).IsRequired();
            builder.Property(decision => decision.AdministratorUserId).IsRequired();
            builder.Property(decision => decision.DecidedAt).IsRequired();
            builder.Property(decision => decision.Justification).HasMaxLength(2000).IsRequired();
            builder.Property(decision => decision.EvaluatedNetworkLevel).IsRequired();
            builder.Property(decision => decision.EvaluatedFundingAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(decision => decision.EvaluatedFundingCurrency).HasMaxLength(3).IsRequired();

            builder.HasIndex(decision => decision.EntryParticipationId).IsUnique();
            builder.HasIndex(decision => decision.LoanAgreementId).IsUnique();
            builder.HasIndex(decision => decision.OnyxParticipationId).IsUnique();
            builder.HasIndex(decision => new { decision.TenantId, decision.CustomerId });

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(decision => decision.EntryParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OnyxLoanAgreement>()
                .WithMany()
                .HasForeignKey(decision => decision.LoanAgreementId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OnyxParticipation>()
                .WithMany()
                .HasForeignKey(decision => decision.OnyxParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
