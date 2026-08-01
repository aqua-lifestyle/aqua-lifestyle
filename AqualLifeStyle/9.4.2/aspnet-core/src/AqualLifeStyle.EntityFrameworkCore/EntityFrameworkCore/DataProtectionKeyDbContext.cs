using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    /// <summary>
    /// Minimal, non-tenant Data Protection context used outside ABP's Unit of Work.
    /// Schema ownership remains with the AqualLifeStyleDbContext migration stream.
    /// </summary>
    public sealed class DataProtectionKeyDbContext : DbContext, IDataProtectionKeyContext
    {
        public DataProtectionKeyDbContext(DbContextOptions<DataProtectionKeyDbContext> options)
            : base(options)
        {
        }

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DataProtectionKey>(entity =>
            {
                entity.ToTable("DataProtectionKeys", table => table.ExcludeFromMigrations());
                entity.HasKey(key => key.Id);
            });
        }
    }
}
