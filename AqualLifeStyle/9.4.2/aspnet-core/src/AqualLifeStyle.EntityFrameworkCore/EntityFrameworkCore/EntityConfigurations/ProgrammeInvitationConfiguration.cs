using AqualLifeStyle.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class ProgrammeInvitationConfiguration
        : IEntityTypeConfiguration<ProgrammeInvitation>
    {
        public void Configure(EntityTypeBuilder<ProgrammeInvitation> builder)
        {
            builder.ToTable("ProgrammeInvitations");
            builder.Property(invitation => invitation.TenantId).IsRequired();
            builder.Property(invitation => invitation.ProgrammeKey)
                .HasMaxLength(ProgrammeInvitation.MaxProgrammeKeyLength)
                .IsRequired();
            builder.Property(invitation => invitation.ProgrammeParticipationId).IsRequired();
            builder.Property(invitation => invitation.Code)
                .HasMaxLength(ProgrammeInvitation.CodeLength)
                .IsRequired();

            builder.HasAlternateKey(invitation => new
                {
                    invitation.Id,
                    invitation.TenantId,
                    invitation.ProgrammeParticipationId
                })
                .HasName("AK_ProgrammeInvitations_Id_Tenant_Participation");
            builder.HasIndex(invitation => invitation.Code).IsUnique();
            builder.HasIndex(invitation => new
                {
                    invitation.ProgrammeKey,
                    invitation.ProgrammeParticipationId
                })
                .IsUnique();
        }
    }
}
