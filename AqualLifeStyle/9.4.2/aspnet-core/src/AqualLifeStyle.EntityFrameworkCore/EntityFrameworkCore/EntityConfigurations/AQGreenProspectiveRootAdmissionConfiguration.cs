using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenProspectiveRootAdmissionConfiguration
        : IEntityTypeConfiguration<AQGreenProspectiveRootAdmission>
    {
        public void Configure(EntityTypeBuilder<AQGreenProspectiveRootAdmission> builder)
        {
            builder.ToTable("AQGreenProspectiveRootAdmissions", table =>
            {
                table.HasCheckConstraint("CK_AQGRootAdmission_Actor", "\"AdministratorUserId\" > 0");
                table.HasCheckConstraint("CK_AQGRootAdmission_Reason", "length(trim(\"Reason\")) > 0");
            });
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
            builder.HasIndex(item => new { item.TenantId, item.ParticipantId }).IsUnique();
            builder.HasOne<EntryParticipation>().WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ParticipantId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.Ignore(item => item.DomainEvents);
        }
    }
}
