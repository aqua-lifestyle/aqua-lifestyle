using Microsoft.EntityFrameworkCore;
using Abp.Zero.EntityFrameworkCore;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Memberships;
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
                entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
                entity.OwnsOne(e => e.Email, email =>
                {
                    email.Property(p => p.Value).HasColumnName("Email").IsRequired().HasMaxLength(256);
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
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasMany(e => e.FollowUps)
                    .WithOne()
                    .HasForeignKey(f => f.EnquiryId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Navigation(e => e.FollowUps)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);
            });
        }
    }
}
