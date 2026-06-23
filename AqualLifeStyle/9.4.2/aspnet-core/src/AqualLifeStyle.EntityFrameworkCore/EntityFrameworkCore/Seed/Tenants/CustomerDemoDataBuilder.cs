using System.Linq;
using AqualLifeStyle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    /// <summary>Creates a repeatable customer account for exercising membership and ordering flows.</summary>
    public sealed class CustomerDemoDataBuilder
    {
        public const string UserName = "customer.demo";
        public const string Password = "Customer123!";
        public const string Email = "customer.demo@aqualifestyle.local";

        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;
        public CustomerDemoDataBuilder(AqualLifeStyleDbContext context, int tenantId)
        {
            _context = context;
            _tenantId = tenantId;
        }

        public void Create()
        {
            var membership = _context.Memberships.IgnoreQueryFilters()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Id)
                .First();
            new TenantDemoUserBuilder(_context, _tenantId).Create(
                UserName,
                Password,
                Email,
                "Naledi",
                "Khumalo",
                "Member",
                AquaUserRole.Member,
                membership.Id);
        }
    }
}
