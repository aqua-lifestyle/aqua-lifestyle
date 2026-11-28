using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class AQGreenHistoricalTreeReviewConfiguration : IEntityTypeConfiguration<AQGreenHistoricalTreeReview>
    {
        public void Configure(EntityTypeBuilder<AQGreenHistoricalTreeReview> b)
        {
            b.ToTable("AQGreenHistoricalTreeReviews", t =>
            {
                t.HasCheckConstraint("CK_AQGHistory_Review",
                "\"ExpectedMembers\" BETWEEN 1 AND 1000 AND \"OpenedBy\" > 0 AND " +
                "((\"ClearedBy\" IS NULL AND \"ClearedAt\" IS NULL) OR " +
                "(\"ClearedBy\" IS NOT NULL AND \"ClearedBy\" > 0 AND \"ClearedAt\" IS NOT NULL AND \"ClearedAt\" >= \"OpenedAt\"))");
                t.HasCheckConstraint("CK_AQGHistory_Cancellation",
                    "(\"PlacementCountAtOpen\" IS NULL OR \"PlacementCountAtOpen\" > 0) AND " +
                    "(\"ProtectedAt\" IS NULL OR \"ProtectedAt\" >= \"OpenedAt\") AND " +
                    "((\"CancelledAt\" IS NULL AND \"CancelledBy\" IS NULL AND \"CancellationReason\" IS NULL AND \"CancellationEvidenceReference\" IS NULL) OR " +
                    "(\"CancelledAt\" IS NOT NULL AND \"CancelledAt\" >= \"OpenedAt\" AND \"CancelledBy\" IS NOT NULL AND \"CancelledBy\" > 0 AND " +
                    "\"ClearedAt\" IS NULL AND \"ProtectedAt\" IS NULL AND \"PlacementCountAtOpen\" IS NOT NULL AND " +
                    "\"CancellationReason\" IS NOT NULL AND length(trim(\"CancellationReason\")) > 0 AND " +
                    "\"CancellationEvidenceReference\" IS NOT NULL AND length(trim(\"CancellationEvidenceReference\")) > 0))");
            });
            b.Property(r => r.EvidenceReference).IsRequired().HasMaxLength(128);
            b.Property(r => r.RosterDigest).HasMaxLength(64);
            b.Property(r => r.CancellationReason).HasMaxLength(500);
            b.Property(r => r.CancellationEvidenceReference).HasMaxLength(128);
            b.HasAlternateKey(r => new { r.TenantId, r.Id });
            b.HasIndex(r => new { r.TenantId, r.ScopeId }).IsUnique().HasFilter("\"CancelledAt\" IS NULL");
            b.HasIndex(r => new { r.TenantId, r.ScopeId, r.EvidenceReference }).IsUnique();
            b.HasOne<AQGreenPlacementTreeScope>().WithMany()
                .HasForeignKey(r => new { r.TenantId, r.ScopeId })
                .HasPrincipalKey(s => new { s.TenantId, s.Id }).OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal sealed class AQGreenHistoricalMemberReferenceConfiguration : IEntityTypeConfiguration<AQGreenHistoricalMemberReference>
    {
        public void Configure(EntityTypeBuilder<AQGreenHistoricalMemberReference> b)
        {
            b.ToTable("AQGreenHistoricalMemberReferences");
            b.Property(r => r.OriginalReference).IsRequired().HasMaxLength(128);
            b.Property(r => r.ReferenceKey).IsRequired().HasMaxLength(128);
            b.HasIndex(r => new { r.TenantId, r.ReferenceKey }).IsUnique();
            b.HasIndex(r => new { r.TenantId, r.ParticipantId }).IsUnique();
            b.HasOne<AQGreenHistoricalTreeReview>().WithMany()
                .HasForeignKey(r => new { r.TenantId, r.ReviewId })
                .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EntryParticipation>().WithMany().HasForeignKey(r => new { r.TenantId, r.ParticipantId })
                .HasPrincipalKey(p => new { p.TenantId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
