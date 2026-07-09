using System;
using System.Collections.Generic;
using System.Linq;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Products;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Host
{
    public class HostDemoDataBuilder
    {
        private readonly AqualLifeStyleDbContext _context;

        public HostDemoDataBuilder(AqualLifeStyleDbContext context)
        {
            _context = context;
        }

        public void Create()
        {
            try
            {
                // Only seed if tables are empty and database is ready
                if (!_context.Memberships.Any())
                {
                    CreateMembershipTiers();
                    CreateDemoProducts();
                    CreateDemoCustomers();
                    CreateDemoEnquiries();
                }
            }
            catch (Exception ex)
            {
                // Don't crash startup - the database may still be initializing - but
                // surface the failure through the application log so a genuine seeding
                // problem is not silently swallowed (Debug.WriteLine is a no-op in Release).
                Abp.Logging.LogHelper.Logger.Warn("HostDemoDataBuilder.Create failed; demo data was not seeded.", ex);
            }
        }

        private void CreateMembershipTiers()
        {
            if (_context.Memberships.Any()) return;

            var tiers = new List<Membership>
            {
                Membership.Create("Jasper", "Entry-level membership tier with starter benefits", Domain.Enums.MembershipType.Jasper),
                Membership.Create("Onyx", "Mid-tier membership with greater discounts", Domain.Enums.MembershipType.Onyx),
                Membership.Create("AQGreen", "High-tier membership with profit-sharing benefits", Domain.Enums.MembershipType.AQGreen),
                Membership.Create("Business Premier", "Top-tier business membership with maximum benefits", Domain.Enums.MembershipType.BusinessPremier)
            };

            _context.Memberships.AddRange(tiers);
            _context.SaveChanges();
        }

        private void CreateDemoProducts()
        {
            if (_context.Products.Any()) return;

            var productList = new List<Product>
            {
                Product.Create("Starter Pack", 100m, null),
                Product.Create("Jasper Bundle", 90m, _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.Jasper).Id),
                Product.Create("Onyx Bundle", 225m, _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.Onyx).Id),
                Product.Create("AQGreen Bundle", 450m, _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.AQGreen).Id),
                Product.Create("Business Premier Bundle", 700m, _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.BusinessPremier).Id)
            };

            _context.Products.AddRange(productList);
            _context.SaveChanges();
        }

        private void CreateDemoCustomers()
        {
            if (_context.Customers.Any()) return;

            var customers = new List<Customer>
            {
                Customer.Create("Alice Johnson", new EmailAddress("alice@example.com"), _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.Jasper).Id),
                Customer.Create("Brian Okoro", new EmailAddress("brian@example.com"), _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.Onyx).Id),
                Customer.Create("Cynthia Nwosu", new EmailAddress("cynthia@example.com"), _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.AQGreen).Id)
            };

            _context.Customers.AddRange(customers);
            _context.SaveChanges();
        }

        private void CreateDemoEnquiries()
        {
            if (_context.Enquiries.Any()) return;

            var defaultCustomer = _context.Customers.First();
            var defaultProduct = _context.Products.First();

            var enquiry = Enquiry.Create(defaultCustomer.Id, defaultProduct.Id, "I would like to learn more about the membership product and pricing.");
            enquiry.Respond("Thank you for your question. We can support your order with a starter bundle.");
            _context.Enquiries.Add(enquiry);
            _context.SaveChanges();
        }
    }
}
