using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Authorization.Users;
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
                // Log but don't crash - database may still be initializing
                System.Diagnostics.Debug.WriteLine($"HostDemoDataBuilder.Create failed: {ex.Message}");
            }
        }

        private void CreateMembershipTiers()
        {
            if (_context.Memberships.Any()) return;

            var tiers = new List<Membership>
            {
                Membership.Create(null, "Jasper", "Entry-level membership tier with starter benefits", Domain.Enums.MembershipType.Jasper),
                Membership.Create(null, "Onyx", "Mid-tier membership with greater discounts", Domain.Enums.MembershipType.Onyx),
                Membership.Create(null, "AQGreen", "High-tier membership with profit-sharing benefits", Domain.Enums.MembershipType.AQGreen),
                Membership.Create(null, "Business Premier", "Top-tier business membership with maximum benefits", Domain.Enums.MembershipType.BusinessPremier)
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

            var hostAdminUser = _context.Users.First(u => u.TenantId == null && u.UserName == AbpUserBase.AdminUserName);
            if (_context.Entry(hostAdminUser).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            {
                _context.Users.Attach(hostAdminUser);
            }

            var customer = Customer.Create(null, hostAdminUser.Id, "Alice Johnson", new EmailAddress("alice@example.com"), _context.Memberships.First(m => m.MembershipType == Domain.Enums.MembershipType.Jasper).Id, hostAdminUser);

            _context.Customers.Add(customer);
            _context.SaveChanges();
        }

        private void CreateDemoEnquiries()
        {
            if (_context.Enquiries.Any()) return;

            var defaultCustomer = _context.Customers.First();
            var defaultProduct = _context.Products.First();

            var enquiry = Enquiry.Create(null, defaultCustomer.Id, defaultProduct.Id, "I would like to learn more about the membership product and pricing.");
            enquiry.Respond("Thank you for your question. We can support your order with a starter bundle.");
            _context.Enquiries.Add(enquiry);
            _context.SaveChanges();
        }
    }
}
