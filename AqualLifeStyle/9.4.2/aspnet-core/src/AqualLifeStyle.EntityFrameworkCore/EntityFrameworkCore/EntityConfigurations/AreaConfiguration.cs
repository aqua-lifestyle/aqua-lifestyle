using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
    {
        public void Configure(EntityTypeBuilder<Area> builder)
        {
            builder.ToTable("Areas");
            builder.Property(area => area.TenantId).IsRequired();
            builder.Property(area => area.Code).HasMaxLength(Area.MaxCodeLength).IsRequired();
            builder.Property(area => area.Name).HasMaxLength(Area.MaxNameLength).IsRequired();
            builder.Property(area => area.IsActive).IsRequired();
            builder.HasIndex(area => new { area.TenantId, area.Code }).IsUnique();
            builder.HasAlternateKey(area => new { area.TenantId, area.Id });
        }
    }

    internal sealed class AreaAdminAssignmentConfiguration
        : IEntityTypeConfiguration<AreaAdminAssignment>
    {
        public void Configure(EntityTypeBuilder<AreaAdminAssignment> builder)
        {
            builder.ToTable("AreaAdminAssignments");
            builder.Ignore(assignment => assignment.IsActive);
            builder.Property(assignment => assignment.TenantId).IsRequired();
            builder.Property(assignment => assignment.AreaId).IsRequired();
            builder.Property(assignment => assignment.UserId).IsRequired();
            builder.Property(assignment => assignment.EffectiveFrom).IsRequired();
            builder.HasIndex(assignment => new
                { assignment.TenantId, assignment.AreaId, assignment.UserId })
                .IsUnique()
                .HasFilter("\"RevokedAt\" IS NULL");
            builder.HasOne<Area>()
                .WithMany()
                .HasForeignKey(assignment => new { assignment.TenantId, assignment.AreaId })
                .HasPrincipalKey(area => new { area.TenantId, area.Id })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(assignment => assignment.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal sealed class CustomerAreaAssignmentConfiguration
        : IEntityTypeConfiguration<CustomerAreaAssignment>
    {
        public void Configure(EntityTypeBuilder<CustomerAreaAssignment> builder)
        {
            builder.ToTable("CustomerAreaAssignments");
            builder.Ignore(assignment => assignment.IsCurrent);
            builder.Property(assignment => assignment.TenantId).IsRequired();
            builder.Property(assignment => assignment.CustomerId).IsRequired();
            builder.Property(assignment => assignment.AreaId).IsRequired();
            builder.Property(assignment => assignment.EffectiveFrom).IsRequired();
            builder.Property(assignment => assignment.IsMigrationBaseline).IsRequired();
            builder.Property(assignment => assignment.Reason)
                .HasMaxLength(CustomerAreaAssignment.MaxReasonLength)
                .IsRequired();
            builder.HasIndex(assignment => new { assignment.TenantId, assignment.CustomerId })
                .IsUnique()
                .HasFilter("\"EffectiveTo\" IS NULL");
            builder.HasOne<Area>()
                .WithMany()
                .HasForeignKey(assignment => new { assignment.TenantId, assignment.AreaId })
                .HasPrincipalKey(area => new { area.TenantId, area.Id })
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
