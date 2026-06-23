using System.Linq;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    /// <summary>Creates a repeatable Default-tenant Area Leader scenario for local demos.</summary>
    public sealed class AreaLeaderDemoDataBuilder
    {
        public const string UserName = "area.leader.demo";
        public const string Password = "AreaLeader123!";
        public const string Email = "area.leader.demo@aqualifestyle.local";

        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;
        public AreaLeaderDemoDataBuilder(AqualLifeStyleDbContext context, int tenantId)
        {
            _context = context;
            _tenantId = tenantId;
        }

        public void Create()
        {
            var customer = new TenantDemoUserBuilder(_context, _tenantId).Create(
                UserName,
                Password,
                Email,
                "Amahle",
                "Dlamini",
                "AreaLeader",
                AquaUserRole.AreaLeader);

            var leader = _context.AreaLeaders.IgnoreQueryFilters()
                .SingleOrDefault(item => item.TenantId == _tenantId && !item.IsDeleted && item.CustomerId == customer.Id);
            if (leader == null)
            {
                leader = AreaLeader.Apply(_tenantId, customer.Id, LicenseType.AreaIndependentLeader);
                _context.AreaLeaders.Add(leader);
                _context.SaveChanges();
            }

            var areaSpace = _context.AreaSpaces.IgnoreQueryFilters()
                .FirstOrDefault(item => item.TenantId == _tenantId && !item.IsDeleted && item.AreaLeaderId == leader.Id);
            if (areaSpace == null)
            {
                areaSpace = AreaSpace.Apply(
                    _tenantId,
                    leader.Id,
                    new Address("42 Waterfall Avenue", "Midrand", "Gauteng", "1685"),
                    "120 members",
                    45);
                _context.AreaSpaces.Add(areaSpace);
                _context.SaveChanges();
                leader.LinkAreaSpace(areaSpace.Id);
            }

            var existingFacilitatorCount = _context.Facilitators.IgnoreQueryFilters()
                .Count(item => item.TenantId == _tenantId && !item.IsDeleted && item.AreaLeaderId == leader.Id);
            var facilitatorSlots = 3 - existingFacilitatorCount;

            var candidateCustomers = _context.Customers.IgnoreQueryFilters()
                .Where(item => item.TenantId == _tenantId && item.Id != customer.Id && item.IsActive)
                .OrderBy(item => item.Id)
                .ToList()
                .Where(item => !_context.AreaLeaders.IgnoreQueryFilters().Any(leaderItem =>
                    leaderItem.TenantId == _tenantId && !leaderItem.IsDeleted && leaderItem.CustomerId == item.Id))
                .Where(item => !_context.Facilitators.IgnoreQueryFilters().Any(facilitator =>
                    facilitator.TenantId == _tenantId && !facilitator.IsDeleted && facilitator.CustomerId == item.Id))
                .Take(facilitatorSlots > 0 ? facilitatorSlots : 0)
                .ToList();

            foreach (var facilitatorCustomer in candidateCustomers)
            {
                _context.Facilitators.Add(Facilitator.Register(_tenantId, facilitatorCustomer.Id, leader.Id));
                leader.RecordFacilitator();
            }

            _context.SaveChanges();
        }
    }
}
