using System.Linq;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Host
{
    /// <summary>
    /// Ensures programme configuration required by live customer journeys exists.
    /// This is operational configuration, not demo data.
    /// </summary>
    public sealed class RequiredProgrammeConfigurationBuilder
    {
        private readonly AqualLifeStyleDbContext _context;

        public RequiredProgrammeConfigurationBuilder(
            AqualLifeStyleDbContext context)
        {
            _context = context;
        }

        public void Create()
        {
            var onyxProgrammeExists = _context.Memberships
                .IgnoreQueryFilters()
                .Any(membership =>
                    membership.MembershipType == MembershipType.Onyx);
            if (onyxProgrammeExists)
            {
                return;
            }

            _context.Memberships.Add(Membership.Create(
                tenantId: null,
                name: "Onyx",
                description: "Onyx programme participation configuration",
                membershipType: MembershipType.Onyx));
            _context.SaveChanges();
        }
    }
}
