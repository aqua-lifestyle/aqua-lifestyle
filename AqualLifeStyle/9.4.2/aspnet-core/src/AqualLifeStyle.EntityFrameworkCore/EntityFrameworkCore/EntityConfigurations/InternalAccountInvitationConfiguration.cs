using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class InternalAccountInvitationConfiguration
        : IEntityTypeConfiguration<InternalAccountInvitation>
    {
        public void Configure(EntityTypeBuilder<InternalAccountInvitation> builder)
        {
            builder.ToTable("InternalAccountInvitations");
            builder.HasKey(invitation => invitation.Id);
            builder.Property(invitation => invitation.TenantId).IsRequired();
            builder.Property(invitation => invitation.UserId).IsRequired();
            builder.Property(invitation => invitation.Role).IsRequired();
            builder.Property(invitation => invitation.InvitedEmailAddress)
                .IsRequired()
                .HasMaxLength(InternalAccountInvitation.MaxEmailAddressLength);
            builder.Property(invitation => invitation.PublicCodeHash)
                .IsRequired()
                .HasMaxLength(InternalAccountInvitation.HashLength)
                .IsFixedLength();
            builder.Property(invitation => invitation.SetupTokenHash)
                .IsRequired()
                .HasMaxLength(InternalAccountInvitation.HashLength)
                .IsFixedLength();
            builder.Property(invitation => invitation.Status).IsRequired();
            builder.Property(invitation => invitation.ExpiresAt).IsRequired();
            builder.Property(invitation => invitation.Version)
                .IsRequired()
                .IsConcurrencyToken();
            builder.Property(invitation => invitation.RevocationReason)
                .HasMaxLength(InternalAccountInvitation.MaxRevocationReasonLength);
            builder.HasIndex(invitation => invitation.PublicCodeHash).IsUnique();
            builder.HasIndex(invitation => new
                {
                    invitation.TenantId,
                    invitation.UserId,
                    invitation.CreationTime
                })
                .IsDescending(false, false, true);
            builder.HasIndex(invitation => new
                {
                    invitation.TenantId,
                    invitation.UserId
                })
                .IsUnique()
                .HasFilter("\"Status\" = 0");
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(invitation => invitation.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<InternalAccountInvitation>()
                .WithMany()
                .HasForeignKey(invitation => invitation.PreviousInvitationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
