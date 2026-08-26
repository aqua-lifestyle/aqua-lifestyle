using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenRecruitmentAttributionConfiguration
        : IEntityTypeConfiguration<AQGreenRecruitmentAttribution>
    {
        public void Configure(EntityTypeBuilder<AQGreenRecruitmentAttribution> builder)
        {
            builder.ToTable("AQGreenRecruitmentAttributions", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_Tenant_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_NoSelfSponsor",
                    "\"CreditedSponsorParticipantId\" IS NULL OR " +
                    "\"ParticipantId\" <> \"CreditedSponsorParticipantId\"");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_Kind",
                    $"\"AttributionKind\" IN (" +
                    $"{(int)AQGreenRecruitmentAttributionKind.SponsoredParticipant}, " +
                    $"{(int)AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot})");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_Source",
                    $"\"AcquisitionSource\" IN ({(int)AQGreenAcquisitionSource.MemberInvitation}, " +
                    $"{(int)AQGreenAcquisitionSource.AuthorisedDirectAdmission})");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_SourceRef",
                    "\"SourceReferenceId\" <> '00000000-0000-0000-0000-000000000000'");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_Actor",
                    "\"AttributedByUserId\" IS NULL OR \"AttributedByUserId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_Rules_NotBlank",
                    "length(trim(\"RulesVersion\")) > 0");
                table.HasCheckConstraint(
                    "CK_AQGRecruitAttr_SourceShape",
                    $"(\"AttributionKind\" = {(int)AQGreenRecruitmentAttributionKind.SponsoredParticipant} " +
                    $"AND \"AcquisitionSource\" = {(int)AQGreenAcquisitionSource.MemberInvitation} " +
                    "AND \"CreditedSponsorParticipantId\" IS NOT NULL " +
                    "AND \"AssignmentReason\" IS NULL) OR " +
                    $"(\"AttributionKind\" = {(int)AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot} " +
                    $"AND \"AcquisitionSource\" = {(int)AQGreenAcquisitionSource.AuthorisedDirectAdmission} " +
                    "AND \"CreditedSponsorParticipantId\" IS NULL " +
                    "AND \"AttributedByUserId\" IS NOT NULL " +
                    "AND \"AssignmentReason\" IS NOT NULL " +
                    "AND length(trim(\"AssignmentReason\")) > 0)");
            });

            builder.Property(attribution => attribution.TenantId).IsRequired();
            builder.Property(attribution => attribution.ParticipantId).IsRequired();
            builder.Property(attribution => attribution.CreditedSponsorParticipantId);
            builder.Property(attribution => attribution.AttributionKind).IsRequired();
            builder.Property(attribution => attribution.AcquisitionSource).IsRequired();
            builder.Property(attribution => attribution.SourceReferenceId).IsRequired();
            builder.Property(attribution => attribution.AttributedAt).IsRequired();
            builder.Property(attribution => attribution.AttributedByUserId);
            builder.Property(attribution => attribution.AssignmentReason)
                .HasMaxLength(AQGreenRecruitmentAttributionRules.MaximumAssignmentReasonLength);
            builder.Property(attribution => attribution.RulesVersion)
                .HasMaxLength(AQGreenRecruitmentAttributionRules.MaximumRulesVersionLength)
                .IsRequired();

            builder.HasAlternateKey(attribution => new
                { attribution.TenantId, attribution.Id })
                .HasName("AK_AQGRecruitAttr_Tenant_Id");
            builder.HasIndex(attribution => new
                { attribution.TenantId, attribution.ParticipantId })
                .IsUnique()
                .HasDatabaseName("UX_AQGRecruitAttr_Tenant_Participant");
            builder.HasIndex(attribution => new
                { attribution.TenantId, attribution.CreditedSponsorParticipantId })
                .HasDatabaseName("IX_AQGRecruitAttr_Tenant_Sponsor");

            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(attribution => new
                    { attribution.TenantId, attribution.ParticipantId })
                .HasPrincipalKey(participation => new
                    { participation.TenantId, participation.Id })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AQGRecruitAttr_Participant");
            builder.HasOne<EntryParticipation>()
                .WithMany()
                .HasForeignKey(attribution => new
                    { attribution.TenantId, attribution.CreditedSponsorParticipantId })
                .HasPrincipalKey(participation => new
                    { participation.TenantId, participation.Id })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AQGRecruitAttr_Sponsor");
            builder.HasOne<ProgrammeInvitation>()
                .WithMany()
                .HasForeignKey(attribution => new
                {
                    attribution.SourceReferenceId,
                    attribution.TenantId,
                    attribution.CreditedSponsorParticipantId
                })
                .HasPrincipalKey(invitation => new
                {
                    invitation.Id,
                    invitation.TenantId,
                    invitation.ProgrammeParticipationId
                })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AQGRecruitAttr_InvitationEvidence");
        }
    }

    internal sealed class AQGreenRecruitmentAttributionConfirmationConfiguration
        : IEntityTypeConfiguration<AQGreenRecruitmentAttributionConfirmation>
    {
        public void Configure(
            EntityTypeBuilder<AQGreenRecruitmentAttributionConfirmation> builder)
        {
            builder.ToTable("AQGreenRecruitmentAttributionConfirmations", table =>
            {
                table.HasCheckConstraint(
                    "CK_AQGRecruitConfirm_Tenant_Positive",
                    "\"TenantId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGRecruitConfirm_Method",
                    $"\"ConfirmationMethod\" IN (" +
                    $"{(int)AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance}, " +
                    $"{(int)AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation})");
                table.HasCheckConstraint(
                    "CK_AQGRecruitConfirm_EvidenceRef",
                    "\"EvidenceReferenceId\" <> '00000000-0000-0000-0000-000000000000'");
                table.HasCheckConstraint(
                    "CK_AQGRecruitConfirm_Actor",
                    "\"ConfirmedByUserId\" IS NULL OR \"ConfirmedByUserId\" > 0");
                table.HasCheckConstraint(
                    "CK_AQGRecruitConfirm_Rules_NotBlank",
                    "length(trim(\"RulesVersion\")) > 0");
            });

            builder.Property(confirmation => confirmation.TenantId).IsRequired();
            builder.Property(confirmation => confirmation.AttributionId).IsRequired();
            builder.Property(confirmation => confirmation.ConfirmedAt).IsRequired();
            builder.Property(confirmation => confirmation.ConfirmedByUserId);
            builder.Property(confirmation => confirmation.ConfirmationMethod).IsRequired();
            builder.Property(confirmation => confirmation.EvidenceReferenceId).IsRequired();
            builder.Property(confirmation => confirmation.RulesVersion)
                .HasMaxLength(AQGreenRecruitmentAttributionRules.MaximumRulesVersionLength)
                .IsRequired();

            builder.HasIndex(confirmation => new
                { confirmation.TenantId, confirmation.AttributionId })
                .IsUnique()
                .HasDatabaseName("UX_AQGRecruitConfirm_Tenant_Attribution");

            builder.HasOne<AQGreenRecruitmentAttribution>()
                .WithMany()
                .HasForeignKey(confirmation => new
                    { confirmation.TenantId, confirmation.AttributionId })
                .HasPrincipalKey(attribution => new
                    { attribution.TenantId, attribution.Id })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AQGRecruitConfirm_Attribution");
        }
    }
}
