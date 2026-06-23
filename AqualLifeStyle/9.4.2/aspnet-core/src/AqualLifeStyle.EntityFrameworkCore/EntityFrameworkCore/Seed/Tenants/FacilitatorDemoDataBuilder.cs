using System;
using System.Linq;
using AqualLifeStyle.Domain.AreaNetwork;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    /// <summary>Creates a repeatable Default-tenant Facilitator scenario for local demos.</summary>
    public sealed class FacilitatorDemoDataBuilder
    {
        public const string UserName = "facilitator.demo";
        public const string Password = "Facilitator123!";
        public const string Email = "facilitator.demo@aqualifestyle.local";

        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;
        public FacilitatorDemoDataBuilder(AqualLifeStyleDbContext context, int tenantId)
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
                "Lethabo",
                "Mokoena",
                "Facilitator",
                AquaUserRole.Facilitator);

            var facilitator = _context.Facilitators.IgnoreQueryFilters()
                .SingleOrDefault(item => item.TenantId == _tenantId && !item.IsDeleted && item.CustomerId == customer.Id);
            var areaLeader = _context.AreaLeaders.IgnoreQueryFilters()
                .First(item => item.TenantId == _tenantId && !item.IsDeleted);
            if (facilitator == null)
            {
                facilitator = Facilitator.Register(_tenantId, customer.Id, areaLeader.Id);
                _context.Facilitators.Add(facilitator);
                areaLeader.RecordFacilitator();
                _context.SaveChanges();
            }

            CreateReferralActivity(facilitator, areaLeader);
            CreateOrderActivity(facilitator);
        }

        private void CreateReferralActivity(Facilitator facilitator, AreaLeader areaLeader)
        {
            const int targetDirectReferrals = 5;
            var existingDirectReferrals = _context.Referrals.IgnoreQueryFilters()
                .Count(item => item.TenantId == _tenantId && !item.IsDeleted && item.ReferrerFacilitatorId == facilitator.Id);
            var referralsToCreate = targetDirectReferrals - existingDirectReferrals;
            if (referralsToCreate <= 0)
            {
                return;
            }

            var product = _context.Products.IgnoreQueryFilters()
                .First(item => item.IsActive);
            var alreadyReferredCustomerIds = _context.Referrals.IgnoreQueryFilters()
                .Where(item => item.TenantId == _tenantId && item.ReferrerFacilitatorId == facilitator.Id)
                .Select(item => item.ReferredCustomerId)
                .ToHashSet();
            var referredCustomers = _context.Customers.IgnoreQueryFilters()
                .Where(item => item.TenantId == _tenantId && item.Id != facilitator.CustomerId && item.IsActive)
                .OrderByDescending(item => item.Id)
                .ToList()
                .Where(item => !alreadyReferredCustomerIds.Contains(item.Id))
                .Take(referralsToCreate)
                .ToList();

            foreach (var referredCustomer in referredCustomers)
            {
                var enquiry = Enquiry.Create(
                    _tenantId,
                    referredCustomer.Id,
                    product.Id,
                    $"Referral demo enquiry for {referredCustomer.Name}.");
                enquiry.SetReferredByFacilitator(facilitator.Id);
                _context.Enquiries.Add(enquiry);
                _context.SaveChanges();

                enquiry.ConvertToCustomer(facilitator.Id);
                enquiry.DomainEvents.Clear();
                var convertedEvent = new EnquiryConvertedEvent(
                    enquiry.Id,
                    referredCustomer.Id,
                    product.Id,
                    facilitator.Id,
                    enquiry.ConvertedAt ?? DateTime.UtcNow,
                    _tenantId);
                var attribution = new ReferralAttributionService(new CommissionCalculator())
                    .Attribute(convertedEvent, facilitator, areaLeader);
                _context.Referrals.Add(attribution.DirectReferral);
                _context.Referrals.Add(attribution.IndirectReferral);
                _context.SaveChanges();
            }
        }

        private void CreateOrderActivity(Facilitator facilitator)
        {
            var directReferrals = _context.Referrals.IgnoreQueryFilters()
                .Where(item => item.TenantId == _tenantId && !item.IsDeleted && item.ReferrerFacilitatorId == facilitator.Id)
                .OrderBy(item => item.Id)
                .Take(5)
                .ToList();

            for (var index = 0; index < directReferrals.Count; index++)
            {
                var referral = directReferrals[index];
                if (_context.OrderIntents.Any(item => item.EnquiryId == referral.SourceEnquiryId))
                {
                    continue;
                }

                var enquiry = _context.Enquiries.IgnoreQueryFilters()
                    .Single(item => item.TenantId == _tenantId && item.Id == referral.SourceEnquiryId);
                var product = _context.Products.IgnoreQueryFilters().Single(item => item.Id == enquiry.ProductId);
                var orderedAt = DateTime.UtcNow.AddDays(-(index + 1));
                var order = OrderIntent.CreateReserved(
                    referral.ReferredCustomerId,
                    product.Id,
                    enquiry.Id,
                    product.Price,
                    product.Price,
                    orderedAt);

                if (index % 3 == 0)
                {
                    order.Complete(orderedAt.AddHours(4));
                }
                else if (index % 3 == 2)
                {
                    order.Cancel(orderedAt.AddHours(2));
                }

                _context.OrderIntents.Add(order);
            }

            _context.SaveChanges();
        }
    }
}
