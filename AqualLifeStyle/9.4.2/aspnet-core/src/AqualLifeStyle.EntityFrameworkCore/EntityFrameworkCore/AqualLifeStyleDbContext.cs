using Microsoft.EntityFrameworkCore;
using Abp.Zero.EntityFrameworkCore;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Orders;
using AqualLifeStyle.Domain.Products;
using AqualLifeStyle.MultiTenancy;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public class AqualLifeStyleDbContext : AbpZeroDbContext<Tenant, Role, User, AqualLifeStyleDbContext>
    {
        public virtual DbSet<Membership> Memberships { get; set; }
        public virtual DbSet<MembershipBenefit> MembershipBenefits { get; set; }
        public virtual DbSet<Customer> Customers { get; set; }
        public virtual DbSet<Product> Products { get; set; }
        public virtual DbSet<Enquiry> Enquiries { get; set; }
        public virtual DbSet<EnquiryFollowUp> EnquiryFollowUps { get; set; }
        public virtual DbSet<OrderIntent> OrderIntents { get; set; }

        public virtual DbSet<Facilitator> Facilitators { get; set; }
        public virtual DbSet<Referral> Referrals { get; set; }
        public virtual DbSet<AreaLeader> AreaLeaders { get; set; }
        public virtual DbSet<AreaSpace> AreaSpaces { get; set; }

        public AqualLifeStyleDbContext(DbContextOptions<AqualLifeStyleDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Membership>(entity =>
            {
                entity.ToTable("Memberships");
                entity.Property(e => e.TenantId);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Description).HasMaxLength(512);
                entity.Property(e => e.MembershipType).IsRequired();
            });

            modelBuilder.Entity<MembershipBenefit>(entity =>
            {
                entity.ToTable("MembershipBenefits");
                entity.Property(e => e.BenefitName).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Description).HasMaxLength(512);
                entity.Property(e => e.MembershipType).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.Property(e => e.TenantId);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
                entity.OwnsOne(e => e.Email, email =>
                {
                    email.Property(p => p.Value).HasColumnName("Email").IsRequired().HasMaxLength(256);
                    email.HasIndex(p => p.Value).IsUnique();
                });
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Price).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
            });

            modelBuilder.Entity<EnquiryFollowUp>(entity =>
            {
                entity.ToTable("EnquiryFollowUps");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EnquiryId).IsRequired();
                entity.Property(e => e.FollowUpDate).IsRequired();
                entity.Property(e => e.FollowUpByMemberId);
                entity.Property(e => e.FollowUpNotes).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.Outcome).IsRequired();
                entity.Property(e => e.ConversionProbability).IsRequired();
                entity.Property(e => e.IsResolved).IsRequired();
            });

            modelBuilder.Entity<Enquiry>(entity =>
            {
                entity.ToTable("Enquiries");
                entity.Property(e => e.TenantId);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.ReferredByFacilitatorId);

                entity.HasMany(e => e.FollowUps)
                    .WithOne()
                    .HasForeignKey(f => f.EnquiryId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Navigation(e => e.FollowUps)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<OrderIntent>(entity =>
            {
                entity.ToTable("OrderIntents");
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.EnquiryId);
                entity.Property(e => e.UnitPrice).IsRequired();
                entity.Property(e => e.ReservedPrice).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.ReservedAt);
                entity.Property(e => e.CancelledAt);
                entity.Property(e => e.CompletedAt);

                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.EnquiryId);
            });

            modelBuilder.Entity<Facilitator>(entity =>
            {
                entity.ToTable("Facilitators");
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.AreaLeaderId).IsRequired();
                entity.Property(e => e.Rank).IsRequired();
                entity.Property(e => e.DirectReferrals).IsRequired();
                entity.Property(e => e.IndirectReferrals).IsRequired();
                entity.Property(e => e.AwardBalance).IsRequired();
                entity.HasOne(e => e.AreaLeader)
                    .WithMany()
                    .HasForeignKey(e => e.AreaLeaderId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.AreaLeaderId);
                entity.HasIndex(e => e.CustomerId);
            });

            modelBuilder.Entity<Referral>(entity =>
            {
                entity.ToTable("Referrals");
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.ReferredCustomerId).IsRequired();
                entity.Property(e => e.SourceEnquiryId).IsRequired();
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.AwardAmount).IsRequired();
                entity.Property(e => e.AwardIssued).IsRequired();
                entity.HasIndex(e => e.ReferrerFacilitatorId);
                entity.HasIndex(e => e.ReferrerAreaLeaderId);
                entity.HasIndex(e => e.SourceEnquiryId);
            });

            modelBuilder.Entity<AreaLeader>(entity =>
            {
                entity.ToTable("AreaLeaders");
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.LicenseType).IsRequired();
                entity.Property(e => e.LicenseFee).IsRequired();
                entity.Property(e => e.Rank).IsRequired();
                entity.Property(e => e.AreaSpaceId);
                entity.Property(e => e.MonthlySubscription).IsRequired();
                entity.Property(e => e.DirectReferrals).IsRequired();
                entity.Property(e => e.IndirectReferrals).IsRequired();
                entity.Property(e => e.OrderTarget).IsRequired();
                entity.HasIndex(e => e.CustomerId);
            });

            modelBuilder.Entity<AreaSpace>(entity =>
            {
                entity.ToTable("AreaSpaces");
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.AreaLeaderId).IsRequired();
                entity.Property(e => e.AddressLine).IsRequired().HasMaxLength(512);
                entity.Property(e => e.Capacity).IsRequired().HasMaxLength(64);
                entity.Property(e => e.InterestedMembers).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.PresentationsCompleted).IsRequired();
                entity.Property(e => e.StartupOrdersCompleted).IsRequired();
                entity.HasIndex(e => e.AreaLeaderId);
            });
        }
    }
}
