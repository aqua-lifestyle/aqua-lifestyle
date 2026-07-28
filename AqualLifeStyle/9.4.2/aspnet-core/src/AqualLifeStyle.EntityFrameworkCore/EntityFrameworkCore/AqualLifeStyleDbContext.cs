using Microsoft.EntityFrameworkCore;
using Abp.Zero.EntityFrameworkCore;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Orders;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Products;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.Domain.Savings;
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
        public virtual DbSet<MemberPayment> MemberPayments { get; set; }
        public virtual DbSet<DirectOnyxCheckoutIntent> DirectOnyxCheckoutIntents { get; set; }
        public virtual DbSet<AQGreenJoiningCheckout> AQGreenJoiningCheckouts { get; set; }
        public virtual DbSet<YocoWebhookReceipt> YocoWebhookReceipts { get; set; }
        public virtual DbSet<EntryParticipation> EntryParticipations { get; set; }
        public virtual DbSet<EntryRecruiterCorrection> EntryRecruiterCorrections { get; set; }
        public virtual DbSet<EntryMonthlyObligation> EntryMonthlyObligations { get; set; }
        public virtual DbSet<EntryCommissionPeriod> EntryCommissionPeriods { get; set; }
        public virtual DbSet<EntryWeeklyCommission> EntryWeeklyCommissions { get; set; }
        public virtual DbSet<EntryCommissionComponent> EntryCommissionComponents { get; set; }
        public virtual DbSet<OnyxParticipation> OnyxParticipations { get; set; }
        public virtual DbSet<OnyxRecruiterCorrection> OnyxRecruiterCorrections { get; set; }
        public virtual DbSet<ProgrammeInvitation> ProgrammeInvitations { get; set; }
        public virtual DbSet<OnyxCommissionPeriod> OnyxCommissionPeriods { get; set; }
        public virtual DbSet<OnyxWeeklyCommission> OnyxWeeklyCommissions { get; set; }
        public virtual DbSet<OnyxCommissionComponent> OnyxCommissionComponents { get; set; }
        public virtual DbSet<OnyxTravelBenefitEntitlement> OnyxTravelBenefitEntitlements { get; set; }
        public virtual DbSet<OnyxLoanAgreement> OnyxLoanAgreements { get; set; }
        public virtual DbSet<OnyxLoanWeeklyRequirement> OnyxLoanWeeklyRequirements { get; set; }
        public virtual DbSet<OnyxLoanRepaymentAllocation> OnyxLoanRepaymentAllocations { get; set; }
        public virtual DbSet<SavingsAccount> SavingsAccounts { get; set; }
        public virtual DbSet<SavingsContribution> SavingsContributions { get; set; }

        public AqualLifeStyleDbContext(DbContextOptions<AqualLifeStyleDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AqualLifeStyleDbContext).Assembly);

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasOne<AreaLeader>()
                    .WithMany()
                    .HasForeignKey(tenant => tenant.AreaLeaderId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(user => user.HomeAddress)
                    .HasMaxLength(User.MaxHomeAddressLength);
            });

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
                entity.Property(e => e.UserId).IsRequired();
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.Property(e => e.ClubMemberNumber)
                    .HasMaxLength(Customer.MaxClubMemberNumberLength)
                    .IsRequired();
                entity.HasIndex(e => e.ClubMemberNumber).IsUnique();
                entity.HasOne(e => e.User)
                    .WithOne()
                    .HasForeignKey<Customer>(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
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

        }
    }
}
